// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Data.Configuration;

using Cvoya.Spring.Dapr.Data.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// EF Core configuration for <see cref="UnitMembershipEntity"/>. Composite
/// primary key on (unit_id, agent_address); secondary indexes cover the
/// two list access paths (list-by-unit, list-by-agent).
/// </summary>
internal class UnitMembershipEntityConfiguration : IEntityTypeConfiguration<UnitMembershipEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UnitMembershipEntity> builder)
    {
        builder.ToTable("unit_memberships");

        builder.HasKey(e => new { e.UnitId, e.AgentAddress });

        builder.Property(e => e.UnitId).HasColumnName("unit_id").IsRequired().HasMaxLength(256);
        builder.Property(e => e.AgentAddress).HasColumnName("agent_address").IsRequired().HasMaxLength(256);
        builder.Property(e => e.Model).HasColumnName("model").HasMaxLength(256);
        builder.Property(e => e.Specialty).HasColumnName("specialty").HasMaxLength(256);
        builder.Property(e => e.Enabled).HasColumnName("enabled").IsRequired().HasDefaultValue(true);
        builder.Property(e => e.ExecutionMode).HasColumnName("execution_mode").HasConversion<int?>();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => e.AgentAddress).HasDatabaseName("ix_unit_memberships_agent_address");
        // unit_id is the first key column, so list-by-unit already has a
        // covering index via the primary key.
    }
}