/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

/// <summary>
/// Input for the <see cref="CloningLifecycleWorkflow"/>.
/// </summary>
/// <param name="SourceAgentId">The identifier of the agent to clone.</param>
/// <param name="TargetAgentId">The identifier for the new cloned agent.</param>
public record CloningInput(
    string SourceAgentId,
    string TargetAgentId);
