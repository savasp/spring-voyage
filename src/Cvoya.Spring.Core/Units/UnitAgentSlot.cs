// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Units;

/// <summary>
/// Per-unit configuration for an agent. Independent from the unit's member
/// list: a slot records unit-scoped settings (model override, specialty,
/// enable flag, execution mode) without implying the agent is wired into
/// the unit's orchestration scope. Callers that want the agent to receive
/// routed messages must add it as a member via the existing members API.
/// </summary>
/// <param name="AgentId">The agent identifier (matches the directory address <c>agent://{AgentId}</c>).</param>
/// <param name="Model">Optional model override applied when this agent runs within this unit. <c>null</c> means "inherit from the agent's own definition."</param>
/// <param name="Specialty">Optional free-form label describing this agent's role inside the unit (e.g., "reviewer", "implementer"). Used by orchestration strategies that pick agents by specialty.</param>
/// <param name="Enabled">When <c>false</c>, orchestration strategies skip this agent without removing the slot. Re-enabling is cheap.</param>
/// <param name="ExecutionMode">How this agent participates in message dispatch. See <see cref="AgentExecutionMode"/>.</param>
public record UnitAgentSlot(
    string AgentId,
    string? Model,
    string? Specialty,
    bool Enabled,
    AgentExecutionMode ExecutionMode);