/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

using Cvoya.Spring.Core;
using Cvoya.Spring.Dapr.Workflows.Activities;
using global::Dapr.Workflow;

/// <summary>
/// Dapr Workflow that orchestrates the full lifecycle of an agent, including creation and deletion.
/// </summary>
public class AgentLifecycleWorkflow : Workflow<AgentLifecycleInput, AgentLifecycleOutput>
{
    /// <inheritdoc />
    public override async Task<AgentLifecycleOutput> RunAsync(
        WorkflowContext context, AgentLifecycleInput input)
    {
        return input.Operation switch
        {
            LifecycleOperation.Create => await CreateAgentAsync(context, input),
            LifecycleOperation.Delete => await DeleteAgentAsync(context, input),
            _ => throw new SpringException($"Unknown lifecycle operation: {input.Operation}")
        };
    }

    private static async Task<AgentLifecycleOutput> CreateAgentAsync(
        WorkflowContext context, AgentLifecycleInput input)
    {
        // Step 1: Validate the agent definition.
        var isValid = await context.CallActivityAsync<bool>(
            nameof(ValidateAgentDefinitionActivity), input);

        if (!isValid)
        {
            return new AgentLifecycleOutput(
                Success: false, Error: $"Validation failed for agent '{input.AgentId}'");
        }

        // Step 2: Register the agent in the directory.
        await context.CallActivityAsync<bool>(
            nameof(RegisterAgentActivity), input);

        var agentAddress = $"agent://{input.AgentId}";
        return new AgentLifecycleOutput(Success: true, AgentAddress: agentAddress);
    }

    private static async Task<AgentLifecycleOutput> DeleteAgentAsync(
        WorkflowContext context, AgentLifecycleInput input)
    {
        // Step 1: Unregister the agent from the directory.
        await context.CallActivityAsync<bool>(
            nameof(UnregisterAgentActivity), input);

        return new AgentLifecycleOutput(Success: true);
    }
}
