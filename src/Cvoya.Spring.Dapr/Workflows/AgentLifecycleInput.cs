/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

using System.Text.Json;

/// <summary>
/// Input for the <see cref="AgentLifecycleWorkflow"/> describing which operation to perform
/// and the agent metadata required.
/// </summary>
/// <param name="Operation">The lifecycle operation to execute.</param>
/// <param name="AgentId">The unique identifier of the agent.</param>
/// <param name="AgentName">The human-readable display name of the agent (used during creation).</param>
/// <param name="Role">An optional role identifier for directory registration (e.g., "backend-engineer").</param>
/// <param name="Definition">An optional JSON payload describing the full agent definition.</param>
public record AgentLifecycleInput(
    LifecycleOperation Operation,
    string AgentId,
    string? AgentName = null,
    string? Role = null,
    JsonElement? Definition = null);
