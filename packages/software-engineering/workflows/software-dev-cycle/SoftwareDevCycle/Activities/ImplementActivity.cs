/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Packages.SoftwareEngineering.Workflows.Activities;

using Cvoya.Spring.Packages.SoftwareEngineering.Workflows.Models;
using Dapr.Workflow;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches implementation work to the assigned agent and collects the resulting PR.
/// </summary>
public class ImplementActivity(ILogger<ImplementActivity> logger) : WorkflowActivity<ImplInput, PrResult>
{
    public override Task<PrResult> RunAsync(WorkflowActivityContext context, ImplInput input)
    {
        logger.LogInformation(
            "Implementing: {Title} (agent: {AgentId}, steps: {StepCount})",
            input.DevCycleInput.Title,
            input.Assignee.AgentId,
            input.Plan.Steps.Count);

        // Stub: return a placeholder PR result
        var result = new PrResult(
            PrUrl: $"{input.DevCycleInput.SourceUrl}/pull/0",
            Branch: "feature/placeholder");

        return Task.FromResult(result);
    }
}
