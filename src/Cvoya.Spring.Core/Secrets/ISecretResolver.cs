// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Secrets;

/// <summary>
/// Server-side read surface for secrets. <see cref="ResolveAsync"/> is
/// the ONLY entry point through which a plaintext value may leave the
/// system: the HTTP API never returns plaintext, and every agent or
/// connector that needs a secret at runtime goes through this interface.
///
/// Implementations compose <see cref="ISecretRegistry"/> (for the
/// structural lookup) with <see cref="ISecretStore"/> (for the value
/// fetch). The private cloud repo layers audit-log decoration and RBAC
/// checks onto this interface via DI without touching call sites.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Resolves the plaintext value for the given structural reference
    /// in the current tenant, or <c>null</c> if no such reference exists
    /// (or the value is missing from the underlying store).
    /// </summary>
    /// <param name="ref">The structural reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The plaintext value, or <c>null</c>.</returns>
    Task<string?> ResolveAsync(SecretRef @ref, CancellationToken ct);

    /// <summary>
    /// Lists the structural references visible to the current tenant for
    /// the given scope and owner. This is a thin wrapper over
    /// <see cref="ISecretRegistry.ListAsync"/> but is the public
    /// "metadata" surface callers should prefer so audit / policy
    /// decorators see every metadata read.
    /// </summary>
    /// <param name="scope">The scope to list.</param>
    /// <param name="ownerId">The scope-specific owner id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<SecretRef>> ListAsync(SecretScope scope, string ownerId, CancellationToken ct);
}