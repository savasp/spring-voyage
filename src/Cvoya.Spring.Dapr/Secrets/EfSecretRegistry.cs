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
    public async Task RegisterAsync(SecretRef @ref, string storeKey, CancellationToken ct)
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
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.StoreKey = storeKey;
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<string?> LookupStoreKeyAsync(SecretRef @ref, CancellationToken ct)
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

        return entry?.StoreKey;
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