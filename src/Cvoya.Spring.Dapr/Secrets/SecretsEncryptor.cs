// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Secrets;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Cvoya.Spring.Dapr.Tenancy;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default AES-GCM-256 implementation of <see cref="ISecretsEncryptor"/>.
/// The key is loaded once at construction from (in priority order):
/// the <c>SPRING_SECRETS_AES_KEY</c> environment variable (base64), the
/// <see cref="SecretsOptions.AesKeyFile"/> path (base64 contents), or —
/// only if <see cref="SecretsOptions.AllowEphemeralDevKey"/> is set —
/// a random 32-byte key generated in memory.
///
/// <para>
/// Envelope layout (version 1, AES-GCM-256):
/// <c>[0x01][nonce(12)][ciphertext(N)][tag(16)]</c>. The whole byte
/// string is then base64-encoded for storage as a plain string value.
/// Associated data is <c>"{tenantId}:{storeKey}"</c> — rebinding a
/// ciphertext to a different tuple makes authentication fail.
/// </para>
/// </summary>
public partial class SecretsEncryptor : ISecretsEncryptor
{
    /// <summary>
    /// Environment variable name that carries the base64-encoded AES key.
    /// </summary>
    public const string KeyEnvironmentVariable = "SPRING_SECRETS_AES_KEY";

    /// <summary>
    /// AES-256 key size in bytes.
    /// </summary>
    public const int KeySize = 32;

    /// <summary>
    /// AES-GCM nonce size in bytes.
    /// </summary>
    public const int NonceSize = 12;

    /// <summary>
    /// AES-GCM authentication tag size in bytes.
    /// </summary>
    public const int TagSize = 16;

    /// <summary>
    /// Envelope version byte for AES-GCM-256 with (tenantId, storeKey) AAD.
    /// </summary>
    public const byte Version1 = 0x01;

    private readonly byte[] _key;
    private readonly ILogger<SecretsEncryptor> _logger;

    /// <summary>
    /// Creates a new <see cref="SecretsEncryptor"/>. Fails fast if no key
    /// source is configured and the dev-key fallback is disabled, or if
    /// the configured key is obviously weak.
    /// </summary>
    public SecretsEncryptor(
        IOptions<SecretsOptions> options,
        ILogger<SecretsEncryptor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _key = LoadKey(options.Value, logger);
    }

    /// <inheritdoc />
    public string Encrypt(string plaintext, string tenantId, string storeKey)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var aad = BuildAad(tenantId, storeKey);

        // [version][nonce(12)][ciphertext(N)][tag(16)]
        var envelope = new byte[1 + NonceSize + plaintextBytes.Length + TagSize];
        envelope[0] = Version1;
        var nonceSpan = envelope.AsSpan(1, NonceSize);
        RandomNumberGenerator.Fill(nonceSpan);

        var ciphertextSpan = envelope.AsSpan(1 + NonceSize, plaintextBytes.Length);
        var tagSpan = envelope.AsSpan(1 + NonceSize + plaintextBytes.Length, TagSize);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonceSpan, plaintextBytes, ciphertextSpan, tagSpan, aad);

        return Convert.ToBase64String(envelope);
    }

    /// <inheritdoc />
    public string Decrypt(string value, string tenantId, string storeKey, out bool wasEnveloped)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey);

        // Legacy path: if the value is not a valid base64 envelope with
        // the version prefix, treat it as pre-encryption plaintext.
        if (!TryDecodeEnvelope(value, out var envelope))
        {
            wasEnveloped = false;
            return value;
        }

        var version = envelope[0];
        if (version != Version1)
        {
            throw new InvalidOperationException(
                $"Unsupported secrets envelope version 0x{version:X2}. " +
                "This runtime only knows version 0x01 (AES-GCM-256).");
        }

        if (envelope.Length < 1 + NonceSize + TagSize)
        {
            throw new InvalidOperationException(
                "Secrets envelope too short to contain nonce, ciphertext, and tag.");
        }

        var nonce = envelope.AsSpan(1, NonceSize);
        var cipherLength = envelope.Length - 1 - NonceSize - TagSize;
        var ciphertext = envelope.AsSpan(1 + NonceSize, cipherLength);
        var tag = envelope.AsSpan(1 + NonceSize + cipherLength, TagSize);

        var aad = BuildAad(tenantId, storeKey);
        var plaintext = new byte[cipherLength];

        using var aes = new AesGcm(_key, TagSize);
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        }
        catch (CryptographicException ex)
        {
            // Don't leak which AAD mismatched — caller only needs to
            // know the envelope didn't authenticate.
            throw new CryptographicException(
                "Failed to authenticate secrets envelope. " +
                "Either the key material changed, the ciphertext was transplanted " +
                "across a different (tenantId, storeKey) pair, or the envelope is corrupted.",
                ex);
        }

        wasEnveloped = true;
        return Encoding.UTF8.GetString(plaintext);
    }

    private static byte[] BuildAad(string tenantId, string storeKey)
        => Encoding.UTF8.GetBytes($"{tenantId}:{storeKey}");

    private static bool TryDecodeEnvelope(string value, out byte[] envelope)
    {
        envelope = Array.Empty<byte>();

        // A legacy plaintext value is very likely either not valid base64
        // or, if it happens to be, its first byte won't match our version
        // marker. Either way we fall back to the legacy interpretation.
        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length < 1 + NonceSize + TagSize)
        {
            return false;
        }

        if (decoded[0] != Version1)
        {
            return false;
        }

        envelope = decoded;
        return true;
    }

    private static byte[] LoadKey(SecretsOptions options, ILogger logger)
    {
        // 1) SPRING_SECRETS_AES_KEY environment variable takes priority.
        var envValue = Environment.GetEnvironmentVariable(KeyEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            var key = DecodeKeyMaterial(envValue, $"environment variable {KeyEnvironmentVariable}");
            ValidateKey(key, $"environment variable {KeyEnvironmentVariable}");
            Log.UsingEnvKey(logger);
            return key;
        }

        // 2) Optional key file path from Secrets:AesKeyFile.
        if (!string.IsNullOrWhiteSpace(options.AesKeyFile))
        {
            var path = options.AesKeyFile!;
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Secrets:AesKeyFile is configured ({path}) but the file does not exist. " +
                    BuildKeySourceHelp());
            }

            var contents = File.ReadAllText(path).Trim();
            var key = DecodeKeyMaterial(contents, $"file '{path}'");
            ValidateKey(key, $"file '{path}'");
            Log.UsingFileKey(logger, path);
            return key;
        }

        // 3) Ephemeral dev key — only if explicitly allowed.
        if (options.AllowEphemeralDevKey)
        {
            var key = RandomNumberGenerator.GetBytes(KeySize);
            Log.EphemeralDevKey(logger);
            return key;
        }

        throw new InvalidOperationException(
            "Spring secrets at-rest encryption is required but no key is configured. " +
            BuildKeySourceHelp());
    }

    private static byte[] DecodeKeyMaterial(string encoded, string source)
    {
        try
        {
            return Convert.FromBase64String(encoded);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"Spring secrets AES key from {source} is not valid base64. " +
                "Provide a base64-encoded 32-byte (256-bit) key.",
                ex);
        }
    }

    private static void ValidateKey(byte[] key, string source)
    {
        if (key.Length != KeySize)
        {
            throw new InvalidOperationException(
                $"Spring secrets AES key from {source} is {key.Length} bytes; must be {KeySize} bytes (AES-256). " +
                BuildKeySourceHelp());
        }

        if (IsAllSameByte(key, 0x00))
        {
            throw new InvalidOperationException(
                $"Spring secrets AES key from {source} is all zeros. Refusing to start.");
        }

        if (IsAllSameByte(key, 0xFF))
        {
            throw new InvalidOperationException(
                $"Spring secrets AES key from {source} is all 0xFF. Refusing to start.");
        }

        // Sentinel / test patterns: 0x01020304... repeated, or "test"/"changeme" ASCII.
        if (IsSentinelPattern(key))
        {
            throw new InvalidOperationException(
                $"Spring secrets AES key from {source} matches a known sentinel/test pattern. Refusing to start.");
        }
    }

    private static bool IsAllSameByte(byte[] bytes, byte target)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != target)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsSentinelPattern(byte[] key)
    {
        // 0x00, 0x01, 0x02 ... ascending — commonly seen in copy-pasted examples.
        var ascending = true;
        for (var i = 0; i < key.Length; i++)
        {
            if (key[i] != (byte)i)
            {
                ascending = false;
                break;
            }
        }
        if (ascending)
        {
            return true;
        }

        // "changeme" / "test" repeated ASCII.
        var ascii = Encoding.ASCII.GetString(key);
        if (ascii.StartsWith("changeme", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (ascii.StartsWith("testtest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        // All-ASCII-space or all-"A" is also obviously weak.
        if (IsAllSameByte(key, 0x20) || IsAllSameByte(key, (byte)'A'))
        {
            return true;
        }
        return false;
    }

    private static string BuildKeySourceHelp() =>
        "Configure one of: " +
        $"(1) the {KeyEnvironmentVariable} environment variable with a base64-encoded 32-byte key, " +
        "(2) Secrets:AesKeyFile pointing to a file whose contents are a base64-encoded 32-byte key, " +
        "or (3) Secrets:AllowEphemeralDevKey=true for dev-only in-memory key generation (not persisted; " +
        "restart loses all encrypted values). Production deployments should externalize key material " +
        "via a KMS-backed ISecretStore implementation rather than relying on this at-rest layer alone.";

    private static partial class Log
    {
        [LoggerMessage(
            EventId = 2410,
            Level = LogLevel.Information,
            Message = "Loaded Spring secrets AES key from environment variable SPRING_SECRETS_AES_KEY.")]
        public static partial void UsingEnvKey(ILogger logger);

        [LoggerMessage(
            EventId = 2411,
            Level = LogLevel.Information,
            Message = "Loaded Spring secrets AES key from file '{Path}'.")]
        public static partial void UsingFileKey(ILogger logger, string path);

        [LoggerMessage(
            EventId = 2412,
            Level = LogLevel.Warning,
            Message = "Spring secrets AES key was not configured and AllowEphemeralDevKey=true; generated a random in-memory key. DO NOT use this configuration in production: secrets become unreadable across restarts.")]
        public static partial void EphemeralDevKey(ILogger logger);
    }
}