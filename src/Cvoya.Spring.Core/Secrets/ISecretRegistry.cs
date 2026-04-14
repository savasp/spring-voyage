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
///
/// <para>
/// <b>DI decoration.</b> The interface is designed for the same
/// decorate-via-DI pattern used by <see cref="ISecretResolver"/>:
/// downstream consumers (most importantly the private cloud audit-log
/// layer) register a wrapper implementation AFTER
/// <c>AddCvoyaSpringDapr</c> and re-point the <see cref="ISecretRegistry"/>
/// registration at the wrapper; the wrapper forwards to the original
/// scoped instance for the real work. <see cref="RotateAsync"/>'s
/// <see cref="SecretRotation"/> return shape is deliberately rich so
/// the decorator can emit a complete audit event from the result alone
/// — no state on the registry, no side-channel required. See
/// <c>docs/developer/secret-audit.md</c> for a worked example.
/// </para>
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
    /// Returns the <see cref="SecretPointer"/> plus the entry's current
    /// <c>Version</c> (or <c>null</c> for legacy rows that predate the
    /// version column). Resolver paths that need to surface
    /// <see cref="SecretResolution.Version"/> call this variant; the
    /// rotation / delete paths do not need it and should keep using
    /// <see cref="LookupAsync"/>.
    /// </summary>
    /// <param name="ref">The structural reference.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(SecretPointer Pointer, int? Version)?> LookupWithVersionAsync(SecretRef @ref, CancellationToken ct);

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
    /// Rotates an existing registry entry to a new store key / origin.
    /// Atomically (within the registry's unit of work):
    /// <list type="bullet">
    ///   <item><description>Replaces the entry's <c>StoreKey</c> with <paramref name="newStoreKey"/>.</description></item>
    ///   <item><description>Replaces the entry's <c>Origin</c> with <paramref name="newOrigin"/>.</description></item>
    ///   <item><description>Increments the entry's <c>Version</c>. Legacy rows (null version) become version <c>1</c>.</description></item>
    ///   <item><description>Updates the <c>UpdatedAt</c> timestamp.</description></item>
    /// </list>
    /// If the previous pointer was <see cref="SecretOrigin.PlatformOwned"/>
    /// the old backing store slot is scheduled for deletion via
    /// <paramref name="deletePreviousStoreKeyAsync"/>. The delete policy is
    /// <b>immediate</b>: once the registry row points at
    /// <paramref name="newStoreKey"/>, no in-flight reader can reach the
    /// old slot, so retaining it would only leak plaintext. Callers
    /// already holding the old plaintext in memory are unaffected.
    /// External-reference pointers are never touched — the old external
    /// key is left where it lives and the implementation must not call
    /// <paramref name="deletePreviousStoreKeyAsync"/>.
    ///
    /// <para>
    /// The <paramref name="deletePreviousStoreKeyAsync"/> delegate is
    /// injected by the caller (typically the endpoint handler, which
    /// already has an <see cref="ISecretStore"/> reference) so that the
    /// <c>Cvoya.Spring.Core</c> abstraction stays dependency-free. The
    /// registry invokes it only after its own write has committed.
    /// </para>
    ///
    /// <para>
    /// Returns a <see cref="SecretRotation"/> summarising the transition.
    /// The result is the sole input an audit-log decorator wrapping
    /// <see cref="ISecretRegistry"/> needs to emit a complete rotation
    /// event (ref, from/to versions, pointer transition, whether the
    /// old slot was reclaimed).
    /// </para>
    /// </summary>
    /// <param name="ref">The structural reference to rotate. Must already exist in the current tenant.</param>
    /// <param name="newStoreKey">The replacement store key (platform-written key for pass-through; external pointer for external-reference).</param>
    /// <param name="newOrigin">The origin of the replacement pointer. May differ from the previous origin (e.g. rotating a platform-owned entry onto an external reference, or vice versa).</param>
    /// <param name="deletePreviousStoreKeyAsync">
    /// Async delegate invoked with the old store key if and only if the
    /// previous origin was <see cref="SecretOrigin.PlatformOwned"/>. A
    /// <c>null</c> delegate disables cleanup — useful for tests that want
    /// to assert pointer transitions without a real <see cref="ISecretStore"/>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SecretRotation"/> describing the transition.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// The reference does not exist in the current tenant. Rotation
    /// requires an existing entry — use <see cref="RegisterAsync"/> to
    /// create one.
    /// </exception>
    Task<SecretRotation> RotateAsync(
        SecretRef @ref,
        string newStoreKey,
        SecretOrigin newOrigin,
        Func<string, CancellationToken, Task>? deletePreviousStoreKeyAsync,
        CancellationToken ct);

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