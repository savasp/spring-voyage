// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Secrets;

using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Dapr.Tenancy;

using global::Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// OSS <see cref="ISecretStore"/> implementation backed by the Dapr
/// state-management building block (component name defaults to
/// <c>statestore</c>). Plaintext values are written under an opaque
/// GUID key; the structural metadata (tenant / scope / owner / name) is
/// kept in <see cref="ISecretRegistry"/>, which is the sole authority
/// for tenant-to-secret correlation.
///
/// <para>
/// <b>OSS — dev only.</b> Values are persisted without app-layer
/// at-rest encryption: the security of the persisted plaintext is only
/// as strong as whatever the Dapr state store component provides
/// (Redis in local dev is effectively plaintext). Production deployments
/// use the private cloud implementation, which routes writes to Azure
/// Key Vault via Dapr's secret-store building block. At-rest encryption
/// for the OSS implementation is tracked separately — see the follow-up
/// issue referenced in the PR that introduced this type.
/// </para>
/// </summary>
public class DaprStateBackedSecretStore : ISecretStore
{
    private readonly DaprClient _daprClient;
    private readonly IOptions<SecretsOptions> _options;
    private readonly ILogger<DaprStateBackedSecretStore> _logger;

    /// <summary>
    /// Creates a new <see cref="DaprStateBackedSecretStore"/>.
    /// </summary>
    public DaprStateBackedSecretStore(
        DaprClient daprClient,
        IOptions<SecretsOptions> options,
        ILogger<DaprStateBackedSecretStore> logger)
    {
        _daprClient = daprClient;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> WriteAsync(string plaintext, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var storeKey = Guid.NewGuid().ToString("N");
        var backendKey = BuildBackendKey(storeKey);

        _logger.LogDebug(new EventId(2400, "SecretWriteStarted"),
            "Writing secret to store {StoreName} under backend key {BackendKey}",
            _options.Value.StoreComponent, backendKey);

        await _daprClient.SaveStateAsync(
            _options.Value.StoreComponent,
            backendKey,
            plaintext,
            cancellationToken: ct);

        _logger.LogDebug(new EventId(2401, "SecretWriteCompleted"),
            "Wrote secret under backend key {BackendKey}", backendKey);

        return storeKey;
    }

    /// <inheritdoc />
    public async Task<string?> ReadAsync(string storeKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey);

        var backendKey = BuildBackendKey(storeKey);

        _logger.LogDebug(new EventId(2402, "SecretReadStarted"),
            "Reading secret from store {StoreName} under backend key {BackendKey}",
            _options.Value.StoreComponent, backendKey);

        var value = await _daprClient.GetStateAsync<string?>(
            _options.Value.StoreComponent,
            backendKey,
            cancellationToken: ct);

        _logger.LogDebug(new EventId(2403, "SecretReadCompleted"),
            "Read secret under backend key {BackendKey}; found: {Found}",
            backendKey, value is not null);

        return value;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string storeKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey);

        var backendKey = BuildBackendKey(storeKey);

        _logger.LogDebug(new EventId(2404, "SecretDeleteStarted"),
            "Deleting secret from store {StoreName} under backend key {BackendKey}",
            _options.Value.StoreComponent, backendKey);

        await _daprClient.DeleteStateAsync(
            _options.Value.StoreComponent,
            backendKey,
            cancellationToken: ct);

        _logger.LogDebug(new EventId(2405, "SecretDeleteCompleted"),
            "Deleted secret under backend key {BackendKey}", backendKey);
    }

    // The backend key is just the KeyPrefix + the opaque storeKey.
    // Tenant correlation lives in the registry (ISecretRegistry), which
    // is the sole authority. Embedding the tenant here would be redundant
    // at best and a cross-tenant-leak vector at worst (a bug in prefix
    // construction would have no corresponding guard).
    private string BuildBackendKey(string storeKey) =>
        $"{_options.Value.KeyPrefix}{storeKey}";
}