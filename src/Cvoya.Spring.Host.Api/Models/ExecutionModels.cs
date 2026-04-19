// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Models;

/// <summary>
/// Wire-level representation of a unit's manifest-persisted
/// <c>execution:</c> block (#601 / #603 / #409 B-wide). Mirrors the
/// manifest's <see cref="Cvoya.Spring.Manifest.ExecutionManifest"/>
/// shape so the same YAML fragment authored in a unit manifest
/// round-trips through the dedicated
/// <c>GET/PUT/DELETE /api/v1/units/{id}/execution</c> endpoint without
/// renaming.
/// </summary>
/// <remarks>
/// Every field is independently nullable: a unit can declare any
/// subset. Resolution chain (see <c>docs/architecture/units.md</c>):
/// agent.X → unit.X → fail-clean at dispatch / save time.
/// <see cref="Provider"/> and <see cref="Model"/> are meaningful only
/// when <see cref="Tool"/> = <c>dapr-agent</c> — the portal hides them
/// for other tool selections (#598 gating).
/// </remarks>
/// <param name="Image">Default container image reference.</param>
/// <param name="Runtime">Default container runtime (<c>docker</c> / <c>podman</c>).</param>
/// <param name="Tool">Default external agent tool identifier (<c>claude-code</c>, <c>codex</c>, <c>gemini</c>, <c>dapr-agent</c>).</param>
/// <param name="Provider">Default LLM provider (Dapr-Agent-tool-specific).</param>
/// <param name="Model">Default model identifier (Dapr-Agent-tool-specific).</param>
public record UnitExecutionResponse(
    string? Image = null,
    string? Runtime = null,
    string? Tool = null,
    string? Provider = null,
    string? Model = null);

/// <summary>
/// Wire-level representation of an agent's <c>execution:</c> block on
/// the <c>AgentDefinitions.Definition</c> document (#601 / #603 / #409
/// B-wide). Mirrors <see cref="UnitExecutionResponse"/> plus the
/// agent-owned <c>hosting</c> field (<c>ephemeral</c> or
/// <c>persistent</c>).
/// </summary>
/// <remarks>
/// The response shape represents the agent's <b>own declared</b>
/// execution block on disk. It does NOT include inherited values from
/// the parent unit — consult the portal / CLI "effective" surface for
/// that post-merge view. When a field is <c>null</c> here it is either
/// unset on the agent or will inherit from the unit at dispatch time.
/// </remarks>
public record AgentExecutionResponse(
    string? Image = null,
    string? Runtime = null,
    string? Tool = null,
    string? Provider = null,
    string? Model = null,
    string? Hosting = null);