// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Secrets;

using Cvoya.Spring.Core.Secrets;

/// <summary>
/// Default <see cref="ISecretResolver"/> implementation: looks up the
/// opaque store key for a <see cref="SecretRef"/> in the registry and
/// then reads the plaintext from the store. The private cloud repo
/// wraps this with audit-log and RBAC decorators via DI.
/// </summary>
public class ComposedSecretResolver : ISecretResolver
{
    private readonly ISecretRegistry _registry;
    private readonly ISecretStore _store;

    /// <summary>
    /// Creates a new <see cref="ComposedSecretResolver"/>.
    /// </summary>
    public ComposedSecretResolver(ISecretRegistry registry, ISecretStore store)
    {
        _registry = registry;
        _store = store;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveAsync(SecretRef @ref, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@ref);

        var storeKey = await _registry.LookupStoreKeyAsync(@ref, ct);
        if (storeKey is null)
        {
            return null;
        }

        return await _store.ReadAsync(storeKey, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SecretRef>> ListAsync(SecretScope scope, string ownerId, CancellationToken ct)
        => _registry.ListAsync(scope, ownerId, ct);
}