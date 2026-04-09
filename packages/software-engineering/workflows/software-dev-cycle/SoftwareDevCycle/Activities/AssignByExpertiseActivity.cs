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
/// Selects the best-fit agent for the work item based on required expertise.
/// </summary>
public class AssignByExpertiseActivity(ILogger<AssignByExpertiseActivity> logger) : WorkflowActivity<TriageResult, AgentRef>
{
    public override Task<AgentRef> RunAsync(WorkflowActivityContext context, TriageResult input)
    {
        logger.LogInformation(
            "Assigning {ItemType} work item requiring expertise: {Expertise}",
            input.ItemType,
            string.Join(", ", input.RequiredExpertise));

        // Stub: return a placeholder agent reference
        var agent = new AgentRef(AgentId: "backend-engineer-1", Role: "backend-engineer");

        return Task.FromResult(agent);
    }
}
