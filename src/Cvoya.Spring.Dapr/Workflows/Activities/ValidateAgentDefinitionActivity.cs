/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows.Activities;

using global::Dapr.Workflow;
using Microsoft.Extensions.Logging;

/// <summary>
/// Validates that an agent lifecycle input contains a well-formed agent definition.
/// Returns <c>true</c> when the definition is valid; <c>false</c> otherwise.
/// </summary>
public class ValidateAgentDefinitionActivity(ILoggerFactory loggerFactory)
    : WorkflowActivity<AgentLifecycleInput, bool>
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ValidateAgentDefinitionActivity>();

    /// <inheritdoc />
    public override Task<bool> RunAsync(WorkflowActivityContext context, AgentLifecycleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.AgentId))
        {
            _logger.LogWarning("Agent definition validation failed: AgentId is empty");
            return Task.FromResult(false);
        }

        if (input.Operation == LifecycleOperation.Create &&
            string.IsNullOrWhiteSpace(input.AgentName))
        {
            _logger.LogWarning("Agent definition validation failed: AgentName is required for creation");
            return Task.FromResult(false);
        }

        _logger.LogInformation("Agent definition validated for agent {AgentId}", input.AgentId);
        return Task.FromResult(true);
    }
}
