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
/// Merges the approved pull request.
/// </summary>
public class MergeActivity(ILogger<MergeActivity> logger) : WorkflowActivity<PrResult, bool>
{
    public override Task<bool> RunAsync(WorkflowActivityContext context, PrResult input)
    {
        logger.LogInformation("Merging PR: {PrUrl} (branch: {Branch})", input.PrUrl, input.Branch);

        // Stub: return success
        return Task.FromResult(true);
    }
}
