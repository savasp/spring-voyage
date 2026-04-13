// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Secrets;

/// <summary>
/// Structural metadata layer for secrets. The registry maps
/// <see cref="SecretRef"/> (scope + owner + name) to the opaque store key
/// produced by <see cref="ISecretStore"/>. All structural information
/// (tenant, scope, owner, name) lives in the registry; the store key is
/// never decoded or parsed.
///
/// Implementations enforce tenant isolation using
/// <see cref="Cvoya.Spring.Core.Tenancy.ITenantContext"/>: every operation
/// is scoped to the current tenant.
/// </summary>
public interface ISecretRegistry
{
    /// <summary>
    /// Registers a structural reference pointing at an existing store key.
    /// If a registration for (tenant, scope, owner, name) already exists,
    /// it is replaced.
    /// </summary>
    /// <param name="ref">The structural reference.</param>
    /// <param name="storeKey">The opaque store key returned by <see cref="ISecretStore"/>.</param>
    /// <param name="origin">
    /// Who owns the underlying storage slot — see
    /// <see cref="SecretOrigin"/>. A <see cref="SecretOrigin.PlatformOwned"/>
    /// value means the platform wrote the plaintext via
    /// <see cref="ISecretStore.WriteAsync"/>; a
    /// <see cref="SecretOrigin.ExternalReference"/> value means the caller
    /// supplied an externally-managed key and the platform must never
    /// mutate the backing store slot.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task RegisterAsync(SecretRef @ref, string storeKey, SecretOrigin origin, CancellationToken ct);

    /// <summary>
    /// Returns the <see cref="SecretPointer"/> (store key + origin)
    /// recorded for the given structural reference, or <c>null</c> if no
    /// such reference exists in the current tenant.
    /// </summary>
    /// <param name="ref">The structural reference.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<SecretPointer?> LookupAsync(SecretRef @ref, CancellationToken ct);

    /// <summary>
    /// Returns the store key recorded for the given structural reference,
    /// or <c>null</c> if no such reference exists in the current tenant.
    /// Prefer <see cref="LookupAsync"/> — this overload is kept for
    /// resolver paths that only need the opaque pointer and never touch
    /// the store-delete path.
    /// </summary>
    /// <param name="ref">The structural reference.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string?> LookupStoreKeyAsync(SecretRef @ref, CancellationToken ct);

    /// <summary>
    /// Lists the structural references registered in the current tenant
    /// for the given scope and owner. Order is unspecified; callers that
    /// render lists should sort client-side.
    /// </summary>
    /// <param name="scope">The scope to list.</param>
    /// <param name="ownerId">The scope-specific owner id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<SecretRef>> ListAsync(SecretScope scope, string ownerId, CancellationToken ct);

    /// <summary>
    /// Removes the structural reference for the given triple from the
    /// current tenant. A missing reference is not an error. The caller
    /// is responsible for separately deleting the underlying value from
    /// <see cref="ISecretStore"/> — and must first check the
    /// <see cref="SecretPointer.Origin"/> returned by
    /// <see cref="LookupAsync"/>: only <see cref="SecretOrigin.PlatformOwned"/>
    /// pointers may be deleted through <see cref="ISecretStore.DeleteAsync"/>.
    /// </summary>
    /// <param name="ref">The structural reference.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(SecretRef @ref, CancellationToken ct);
}