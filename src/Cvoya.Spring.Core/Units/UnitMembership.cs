// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Units;

using Cvoya.Spring.Core.Agents;

/// <summary>
/// Per-membership configuration for an agent belonging to a unit.
/// Replaces the prior 1:N <c>AgentMetadata.ParentUnit</c> pointer with an
/// M:N membership row (see #160). The override fields mirror the shape of
/// <see cref="AgentMetadata"/>: <c>null</c> means "inherit the agent's own
/// value" at dispatch time; a non-<c>null</c> value overrides it for
/// messages flowing through this particular unit.
/// </summary>
/// <param name="UnitId">The unit this membership attaches the agent to (the unit's <c>Address.Path</c>).</param>
/// <param name="AgentAddress">
/// Canonical string form of the agent's address — currently just the
/// agent id (equivalent to <c>new Address("agent", id).Path</c>). Stored
/// as a string so the membership table does not depend on an
/// address-serialization library.
/// </param>
/// <param name="Model">Per-membership model override, or <c>null</c> to inherit the agent's own <c>Model</c>.</param>
/// <param name="Specialty">Per-membership specialty override, or <c>null</c> to inherit.</param>
/// <param name="Enabled">Per-membership enabled flag. Defaults to <c>true</c> on insert. When <c>false</c>, this unit's orchestration strategy skips the agent even if the agent itself is enabled.</param>
/// <param name="ExecutionMode">Per-membership execution-mode override, or <c>null</c> to inherit.</param>
/// <param name="CreatedAt">UTC timestamp when the membership was first created.</param>
/// <param name="UpdatedAt">UTC timestamp when the membership was last updated.</param>
public record UnitMembership(
    string UnitId,
    string AgentAddress,
    string? Model = null,
    string? Specialty = null,
    bool Enabled = true,
    AgentExecutionMode? ExecutionMode = null,
    DateTimeOffset CreatedAt = default,
    DateTimeOffset UpdatedAt = default);