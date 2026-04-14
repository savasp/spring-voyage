// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Secrets;

using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Core.Tenancy;
using Cvoya.Spring.Dapr.Data;
using Cvoya.Spring.Dapr.Data.Entities;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core-backed <see cref="ISecretRegistry"/> that stores the
/// (tenant, scope, owner, name) → storeKey mapping in the
/// <c>secret_registry_entries</c> table. Every query is filtered by the
/// current tenant resolved from <see cref="ITenantContext"/> so a
/// caller in tenant A can never observe or modify entries owned by
/// tenant B.
/// </summary>
public class EfSecretRegistry : ISecretRegistry
{
    private readonly SpringDbContext _db;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Creates a new <see cref="EfSecretRegistry"/>.
    /// </summary>
    public EfSecretRegistry(SpringDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async Task RegisterAsync(SecretRef @ref, string storeKey, SecretOrigin origin, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@ref);
        ArgumentException.ThrowIfNullOrWhiteSpace(storeKey);

        var tenant = _tenantContext.CurrentTenantId;

        var existing = await _db.SecretRegistryEntries
            .FirstOrDefaultAsync(
                e => e.TenantId == tenant
                  && e.Scope == @ref.Scope
                  && e.OwnerId == @ref.OwnerId
                  && e.Name == @ref.Name,
                ct);

        if (existing is null)
        {
            _db.SecretRegistryEntries.Add(new SecretRegistryEntry
            {
                Id = Guid.NewGuid(),
                TenantId = tenant,
                Scope = @ref.Scope,
                OwnerId = @ref.OwnerId,
                Name = @ref.Name,
                StoreKey = storeKey,
                Origin = origin,
                // New rows start at version 1 so the audit path has a
                // stable "initial version" signal. Legacy rows left at
                // null remain null until a rotate transitions them.
                Version = 1,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.StoreKey = storeKey;
            existing.Origin = origin;
            // Register is explicitly a replacement primitive, not a
            // rotation. Callers that want to track version transitions
            // must go through RotateAsync, which bumps the version.
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<SecretPointer?> LookupAsync(SecretRef @ref, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@ref);

        var tenant = _tenantContext.CurrentTenantId;

        var entry = await _db.SecretRegistryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenant
                  && e.Scope == @ref.Scope
                  && e.OwnerId == @ref.OwnerId
                  && e.Name == @ref.Name,
                ct);

        return entry is null ? null : new SecretPointer(entry.StoreKey, entry.Origin);
    }

    /// <inheritdoc />
    public async Task<string?> LookupStoreKeyAsync(SecretRef @ref, CancellationToken ct)
    {
        var pointer = await LookupAsync(@ref, ct);
        return pointer?.StoreKey;
    }

    /// <inheritdoc />
    public async Task<(SecretPointer Pointer, int? Version)?> LookupWithVersionAsync(SecretRef @ref, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@ref);

        var tenant = _tenantContext.CurrentTenantId;

        var entry = await _db.SecretRegistryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.TenantId == tenant
                  && e.Scope == @ref.Scope
                  && e.OwnerId == @ref.OwnerId
                  && e.Name == @ref.Name,
                ct);

        if (entry is null)
        {
            return null;
        }

        return (new SecretPointer(entry.StoreKey, entry.Origin), entry.Version);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecretRef>> ListAsync(SecretScope scope, string ownerId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var tenant = _tenantContext.CurrentTenantId;

        var entries = await _db.SecretRegistryEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenant && e.Scope == scope && e.OwnerId == ownerId)
            .ToListAsync(ct);

        return entries
            .Select(e => new SecretRef(e.Scope, e.OwnerId, e.Name))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<SecretRotation> RotateAsync(
        SecretRef @ref,
        string newStoreKey,
        SecretOrigin newOrigin,
        Func<string, CancellationToken, Task>? deletePreviousStoreKeyAsync,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@ref);
        ArgumentException.ThrowIfNullOrWhiteSpace(newStoreKey);

        var tenant = _tenantContext.CurrentTenantId;

        var existing = await _db.SecretRegistryEntries
            .FirstOrDefaultAsync(
                e => e.TenantId == tenant
                  && e.Scope == @ref.Scope
                  && e.OwnerId == @ref.OwnerId
                  && e.Name == @ref.Name,
                ct);

        if (existing is null)
        {
            // Rotation requires an existing entry; callers that intend
            // to create go through RegisterAsync / POST. Surfacing this
            // as InvalidOperationException lets the endpoint layer map
            // it to 404 without parsing error strings.
            throw new InvalidOperationException(
                $"Cannot rotate secret '{@ref.Name}' for {@ref.Scope} '{@ref.OwnerId}': no registry entry exists in tenant '{tenant}'.");
        }

        var previousPointer = new SecretPointer(existing.StoreKey, existing.Origin);
        var fromVersion = existing.Version;
        // Legacy rows (null version) transition to version 1 on their
        // first rotation. Post-migration rows increment from their
        // current value. Audit decorators see both transitions via
        // SecretRotation.FromVersion / ToVersion.
        var toVersion = (fromVersion ?? 0) + 1;

        existing.StoreKey = newStoreKey;
        existing.Origin = newOrigin;
        existing.Version = toVersion;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Immediate-delete policy for the old platform-owned slot. The
        // rotation-specific decision documented in
        // docs/developer/secret-store.md: once the registry points at
        // the new key, no in-flight reader can reach the old slot, so
        // any retention would only leak plaintext. External references
        // are never touched — the customer owns that slot.
        var deleted = false;
        if (previousPointer.Origin == SecretOrigin.PlatformOwned && deletePreviousStoreKeyAsync is not null)
        {
            // Best-effort: if the store-delete fails we still return a
            // successful rotation (the registry is the source of
            // truth). Callers with stricter guarantees can wrap this
            // with retry / compensating logic — see the follow-up
            // issue for orphan reconciliation.
            await deletePreviousStoreKeyAsync(previousPointer.StoreKey, ct);
            deleted = true;
        }

        return new SecretRotation(
            @ref,
            fromVersion,
            toVersion,
            previousPointer,
            new SecretPointer(newStoreKey, newOrigin),
            deleted);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(SecretRef @ref, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(@ref);

        var tenant = _tenantContext.CurrentTenantId;

        var existing = await _db.SecretRegistryEntries
            .FirstOrDefaultAsync(
                e => e.TenantId == tenant
                  && e.Scope == @ref.Scope
                  && e.OwnerId == @ref.OwnerId
                  && e.Name == @ref.Name,
                ct);

        if (existing is null)
        {
            return;
        }

        _db.SecretRegistryEntries.Remove(existing);
        await _db.SaveChangesAsync(ct);
    }
}