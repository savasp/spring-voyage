/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Prompts;

using System.Text.Json;
using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Skills;

/// <summary>
/// Holds all input data needed for prompt assembly across the four layers.
/// </summary>
/// <param name="Members">The addresses of peer agents in the unit.</param>
/// <param name="Policies">Optional unit policies as a JSON element.</param>
/// <param name="Skills">Optional skills available to the agent.</param>
/// <param name="PriorMessages">Prior messages in the conversation.</param>
/// <param name="LastCheckpoint">Optional last checkpoint state.</param>
/// <param name="AgentInstructions">Optional agent-specific instructions (Layer 4).</param>
/// <param name="Mode">The execution mode (Hosted or Delegated).</param>
public record PromptAssemblyContext(
    IReadOnlyList<Address> Members,
    JsonElement? Policies,
    IReadOnlyList<Skill>? Skills,
    IReadOnlyList<Message> PriorMessages,
    string? LastCheckpoint,
    string? AgentInstructions,
    ExecutionMode Mode);
