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
/// Creates an implementation plan for the work item.
/// </summary>
public class CreatePlanActivity(ILogger<CreatePlanActivity> logger) : WorkflowActivity<PlanInput, Plan>
{
    public override Task<Plan> RunAsync(WorkflowActivityContext context, PlanInput input)
    {
        logger.LogInformation(
            "Creating plan for: {Title} (assigned to {AgentId})",
            input.DevCycleInput.Title,
            input.Assignee.AgentId);

        // Stub: return a placeholder plan
        var plan = new Plan(
            Steps: ["Analyze requirements", "Implement changes", "Write tests", "Create PR"],
            EstimatedEffort: input.TriageResult.Complexity);

        return Task.FromResult(plan);
    }
}
