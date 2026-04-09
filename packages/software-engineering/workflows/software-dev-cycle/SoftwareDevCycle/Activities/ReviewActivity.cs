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
/// Initiates a code review on the pull request and collects the review result.
/// </summary>
public class ReviewActivity(ILogger<ReviewActivity> logger) : WorkflowActivity<PrResult, ReviewResult>
{
    public override Task<ReviewResult> RunAsync(WorkflowActivityContext context, PrResult input)
    {
        logger.LogInformation("Reviewing PR: {PrUrl}", input.PrUrl);

        // Stub: return a placeholder approval
        var result = new ReviewResult(Decision: "approve", Comments: "Looks good.");

        return Task.FromResult(result);
    }
}
