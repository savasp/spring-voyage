// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Data.Entities;

using Cvoya.Spring.Core.Secrets;

/// <summary>
/// Persists the structural metadata for a secret (scope + owner + name)
/// and a pointer (<see cref="StoreKey"/>) to the backing store entry that
/// holds the plaintext value. The plaintext itself is never stored on
/// this entity.
/// </summary>
public class SecretRegistryEntry
{
    /// <summary>Unique identifier for the registry entry.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant that owns the entry. Never null or empty.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Ownership scope (unit / tenant / platform).</summary>
    public SecretScope Scope { get; set; }

    /// <summary>Scope-specific owner id — unit name, tenant id, etc.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Secret name (case-sensitive within the triple).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Opaque pointer into the backing <see cref="ISecretStore"/>. Does
    /// not encode tenant or structural information — the registry row
    /// owns the structure.
    /// </summary>
    public string StoreKey { get; set; } = string.Empty;

    /// <summary>
    /// Who owns the storage slot that <see cref="StoreKey"/> points at.
    /// This distinction is critical on delete / rotate paths: the store
    /// layer must only mutate slots the platform owns — see
    /// <see cref="SecretOrigin"/> for the full semantics.
    /// </summary>
    public SecretOrigin Origin { get; set; }

    /// <summary>
    /// Monotonically-increasing version number, bumped by
    /// <see cref="ISecretRegistry.RotateAsync"/>. <c>null</c> for legacy
    /// rows that predate the version column; they transition to version
    /// <c>1</c> on their first rotation. Rows created after the
    /// migration start at version <c>1</c> (<see cref="RegisterAsync"/>
    /// leaves <c>null</c> for an unmodified legacy row but initialises
    /// new inserts).
    /// </summary>
    public int? Version { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last-update timestamp (UTC). Set on creation and refreshed by
    /// every <see cref="ISecretRegistry.RotateAsync"/>. Audit decorators
    /// observe the transition via <see cref="SecretRotation"/>'s version
    /// fields; <see cref="UpdatedAt"/> is the persistent record for
    /// operators browsing the registry directly.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}