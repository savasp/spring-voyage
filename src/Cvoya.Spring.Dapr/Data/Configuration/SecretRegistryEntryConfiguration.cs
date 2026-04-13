// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Data.Configuration;

using Cvoya.Spring.Dapr.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for <see cref="SecretRegistryEntry"/>. Applies
/// snake_case naming and a unique index on the structural triple
/// (tenant, scope, owner, name).
/// </summary>
internal class SecretRegistryEntryConfiguration : IEntityTypeConfiguration<SecretRegistryEntry>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SecretRegistryEntry> builder)
    {
        builder.ToTable("secret_registry_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id").IsRequired().HasMaxLength(256);
        builder.Property(e => e.Scope).HasColumnName("scope").IsRequired().HasConversion<int>();
        builder.Property(e => e.OwnerId).HasColumnName("owner_id").IsRequired().HasMaxLength(256);
        builder.Property(e => e.Name).HasColumnName("name").IsRequired().HasMaxLength(256);
        builder.Property(e => e.StoreKey).HasColumnName("store_key").IsRequired().HasMaxLength(512);
        builder.Property(e => e.Origin).HasColumnName("origin").IsRequired().HasConversion<int>();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Scope, e.OwnerId, e.Name })
            .IsUnique()
            .HasDatabaseName("ix_secret_registry_tenant_scope_owner_name");
    }
}