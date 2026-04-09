/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

/// <summary>
/// Output from the <see cref="AgentLifecycleWorkflow"/> indicating whether the operation succeeded.
/// </summary>
/// <param name="Success">Whether the lifecycle operation completed successfully.</param>
/// <param name="AgentAddress">The address of the agent, returned on successful creation.</param>
/// <param name="Error">An error message when the operation fails.</param>
public record AgentLifecycleOutput(
    bool Success,
    string? AgentAddress = null,
    string? Error = null);
