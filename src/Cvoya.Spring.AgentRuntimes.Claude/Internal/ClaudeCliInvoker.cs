// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.Internal;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps the locally-installed <c>claude</c> CLI for credential
/// validation. Originally lived in <c>Cvoya.Spring.Dapr.Execution</c>
/// (#660); migrated here as a private implementation detail of the
/// Claude agent runtime (#679 / #668) so the host no longer needs the
/// CLI on its PATH — the runtime's container image bundles it and
/// <see cref="ClaudeAgentRuntime.VerifyContainerBaselineAsync"/> probes
/// it at install time.
/// </summary>
/// <remarks>
/// <para>
/// <b>Credential plumbing.</b> Two env vars are recognised by the CLI:
/// <c>ANTHROPIC_API_KEY</c> for Platform-plan API keys, and
/// <c>CLAUDE_CODE_OAUTH_TOKEN</c> for Claude.ai tokens produced by
/// <c>claude setup-token</c>. We pick based on the credential prefix
/// (<c>sk-ant-oat</c> signals OAuth; anything else falls through to the
/// API-key var, which the CLI then validates). The credential is never
/// echoed on the command line or written to disk.
/// </para>
/// <para>
/// <b>No model enumeration.</b> The <c>claude</c> CLI does not expose
/// a <c>models</c> subcommand today, so this invoker reports
/// <see cref="ClaudeCliValidationResult.Models"/> = null on success;
/// <see cref="ClaudeAgentRuntime"/> falls back to the seed catalog (or,
/// for API keys, follows up with a best-effort
/// <c>GET /v1/models</c> against the Anthropic Platform API).
/// </para>
/// </remarks>
internal sealed class ClaudeCliInvoker
{
    /// <summary>Default executable name — discovered on <c>PATH</c>. Overridable via <see cref="ExecutablePath"/> for tests.</summary>
    public const string DefaultExecutable = "claude";

    /// <summary>Credential prefix that identifies a Claude.ai OAuth token (<c>claude setup-token</c>).</summary>
    public const string OAuthTokenPrefix = "sk-ant-oat";

    /// <summary>Credential prefix that identifies an Anthropic Platform API key.</summary>
    public const string ApiKeyPrefix = "sk-ant-api";

    /// <summary>Hard cap on CLI invocation duration — a hung CLI cannot stall the wizard indefinitely.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;

    public ClaudeCliInvoker(IProcessRunner processRunner, ILogger logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    /// <summary>Executable path / name. Defaults to <see cref="DefaultExecutable"/>.</summary>
    public string ExecutablePath { get; set; } = DefaultExecutable;

    /// <summary>
    /// Returns <c>true</c> when the credential is a Claude.ai OAuth token
    /// (<c>sk-ant-oat…</c>) — the only format the Anthropic Platform REST
    /// API rejects. Used by <see cref="ClaudeAgentRuntime"/> to decide
    /// whether to skip the REST fallback path entirely.
    /// </summary>
    public static bool IsOAuthToken(string credential) =>
        !string.IsNullOrEmpty(credential)
        && credential.StartsWith(OAuthTokenPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Returns <c>true</c> when the credential is an Anthropic Platform
    /// API key (<c>sk-ant-api…</c>).
    /// </summary>
    public static bool IsApiKey(string credential) =>
        !string.IsNullOrEmpty(credential)
        && credential.StartsWith(ApiKeyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Validates the credential by invoking <c>claude --bare -p
    /// --output-format json</c> with the credential plumbed into the
    /// matching env var. Captures CLI-reported provider errors (401 / 403
    /// / 5xx) and translates them into the runtime's
    /// <see cref="ClaudeCliValidationOutcome"/> bucket.
    /// </summary>
    public async Task<ClaudeCliValidationResult> ValidateAsync(
        string credential,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(credential))
        {
            return new ClaudeCliValidationResult(
                ClaudeCliValidationOutcome.MissingCredential,
                ErrorMessage: "Supply an API key or Claude.ai token to validate.");
        }

        var isOAuth = IsOAuthToken(credential);
        var environment = new Dictionary<string, string>
        {
            // Bare mode disables hooks, plugin sync, and auto-memory
            // lookup so the spawn is minimal and does not touch the
            // operator's on-disk Claude config. The CLI reads OAuth
            // tokens from CLAUDE_CODE_OAUTH_TOKEN and API keys from
            // ANTHROPIC_API_KEY — we set exactly one.
            [isOAuth ? "CLAUDE_CODE_OAUTH_TOKEN" : "ANTHROPIC_API_KEY"] = credential,
        };

        ProcessRunResult result;
        try
        {
            result = await _processRunner.RunAsync(
                ExecutablePath,
                new[] { "--bare", "-p", "--output-format", "json", "respond with OK" },
                environment,
                DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            _logger.LogWarning(ex, "claude CLI not found when validating Anthropic credential.");
            return new ClaudeCliValidationResult(
                ClaudeCliValidationOutcome.CliMissing,
                ErrorMessage: "The claude CLI was not found in this container. " +
                    "Verify the runtime image bundles Claude Code and that " +
                    "VerifyContainerBaselineAsync passes before validating credentials.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "claude CLI timed out while validating Anthropic credential.");
            return new ClaudeCliValidationResult(
                ClaudeCliValidationOutcome.NetworkError,
                ErrorMessage: "The claude CLI did not respond within the allowed time. Retry, or use an Anthropic API key instead.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "claude CLI invocation failed.");
            return new ClaudeCliValidationResult(
                ClaudeCliValidationOutcome.ProviderError,
                ErrorMessage: $"The claude CLI failed to run: {ex.Message}");
        }

        ClaudeCliResult? parsed = null;
        var stdoutTrimmed = result.StandardOutput.Trim();
        if (stdoutTrimmed.Length > 0)
        {
            try
            {
                parsed = JsonSerializer.Deserialize(stdoutTrimmed, ClaudeCliJsonContext.Default.ClaudeCliResult);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "claude CLI produced non-JSON output on stdout.");
            }
        }

        if (parsed is null)
        {
            return new ClaudeCliValidationResult(
                ClaudeCliValidationOutcome.ProviderError,
                ErrorMessage: result.ExitCode == 0
                    ? "The claude CLI returned an empty response. Retry, or use an Anthropic API key instead."
                    : $"The claude CLI exited with code {result.ExitCode} and produced no parseable result.");
        }

        if (parsed.IsError == true)
        {
            // Status 401 / 403 = rejected credential. Anything else
            // (5xx, 429, network-layer) is a provider-side problem.
            if (parsed.ApiErrorStatus is 401 or 403)
            {
                return new ClaudeCliValidationResult(
                    ClaudeCliValidationOutcome.Unauthorized,
                    ErrorMessage: string.IsNullOrWhiteSpace(parsed.Result)
                        ? "Anthropic rejected the credential. Check that the API key or Claude.ai token is live."
                        : $"Anthropic rejected the credential: {parsed.Result}");
            }
            return new ClaudeCliValidationResult(
                ClaudeCliValidationOutcome.ProviderError,
                ErrorMessage: string.IsNullOrWhiteSpace(parsed.Result)
                    ? $"Anthropic returned an error (HTTP {parsed.ApiErrorStatus?.ToString() ?? "?"})."
                    : $"Anthropic returned an error: {parsed.Result}");
        }

        return new ClaudeCliValidationResult(ClaudeCliValidationOutcome.Valid, ErrorMessage: null);
    }

    /// <summary>
    /// Probes whether the <c>claude</c> binary is present in the runtime
    /// container by running <c>claude --version</c>. Returns <c>true</c>
    /// when the spawn succeeds and the CLI exits with code 0; any other
    /// outcome (binary not found, non-zero exit, timeout) returns
    /// <c>false</c> alongside an operator-readable error string in
    /// <see cref="ClaudeCliBaselineProbe.ErrorMessage"/>.
    /// </summary>
    public async Task<ClaudeCliBaselineProbe> ProbeBaselineAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var probe = await _processRunner.RunAsync(
                ExecutablePath,
                new[] { "--version" },
                environment: new Dictionary<string, string>(),
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);

            if (probe.ExitCode == 0)
            {
                return new ClaudeCliBaselineProbe(true, ErrorMessage: null);
            }

            return new ClaudeCliBaselineProbe(
                false,
                ErrorMessage: $"`{ExecutablePath} --version` exited with code {probe.ExitCode}. " +
                    "Verify the runtime container's claude CLI installation.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            _logger.LogDebug(ex, "claude CLI not found on PATH (container baseline probe).");
            return new ClaudeCliBaselineProbe(
                false,
                ErrorMessage: $"`{ExecutablePath}` was not found on PATH. " +
                    "The Claude agent runtime requires the claude CLI to be installed in its container image.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "claude CLI did not respond to --version within the baseline timeout.");
            return new ClaudeCliBaselineProbe(
                false,
                ErrorMessage: $"`{ExecutablePath} --version` did not respond within 5 seconds.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "claude CLI baseline probe failed.");
            return new ClaudeCliBaselineProbe(
                false,
                ErrorMessage: $"Probing `{ExecutablePath} --version` failed: {ex.Message}");
        }
    }
}

/// <summary>Coarse-grained verdict from <see cref="ClaudeCliInvoker.ValidateAsync"/>.</summary>
internal enum ClaudeCliValidationOutcome
{
    Valid,
    Unauthorized,
    ProviderError,
    NetworkError,
    MissingCredential,
    CliMissing,
}

/// <summary>Outcome of a single CLI-backed credential validation.</summary>
internal sealed record ClaudeCliValidationResult(
    ClaudeCliValidationOutcome Outcome,
    string? ErrorMessage);

/// <summary>Outcome of the container-baseline probe — whether the CLI is reachable in this process.</summary>
internal sealed record ClaudeCliBaselineProbe(bool Passed, string? ErrorMessage);

/// <summary>
/// Shape of the <c>claude --output-format json</c> response. Only the
/// fields the validator reads are declared; others are ignored.
/// </summary>
internal sealed record ClaudeCliResult(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("subtype")] string? Subtype,
    [property: JsonPropertyName("is_error")] bool? IsError,
    [property: JsonPropertyName("api_error_status")] int? ApiErrorStatus,
    [property: JsonPropertyName("result")] string? Result);

[JsonSerializable(typeof(ClaudeCliResult))]
internal partial class ClaudeCliJsonContext : JsonSerializerContext;