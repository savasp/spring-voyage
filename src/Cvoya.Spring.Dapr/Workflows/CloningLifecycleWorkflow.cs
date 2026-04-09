/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

using global::Dapr.Workflow;

/// <summary>
/// Dapr Workflow stub for agent cloning. Returns a not-implemented result in Phase 1.
/// </summary>
public class CloningLifecycleWorkflow : Workflow<CloningInput, CloningOutput>
{
    /// <inheritdoc />
    public override Task<CloningOutput> RunAsync(WorkflowContext context, CloningInput input)
    {
        return Task.FromResult(new CloningOutput(
            Success: false, Error: "Cloning not implemented in Phase 1"));
    }
}
