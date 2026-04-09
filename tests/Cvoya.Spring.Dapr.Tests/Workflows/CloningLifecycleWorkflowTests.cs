/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Tests.Workflows;

using Cvoya.Spring.Dapr.Workflows;
using global::Dapr.Workflow;
using FluentAssertions;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CloningLifecycleWorkflow"/>.
/// </summary>
public class CloningLifecycleWorkflowTests
{
    [Fact]
    public async Task RunAsync_ReturnsNotImplemented()
    {
        var workflow = new CloningLifecycleWorkflow();
        var context = Substitute.For<WorkflowContext>();
        var input = new CloningInput("source-agent", "target-agent");

        var result = await workflow.RunAsync(context, input);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not implemented");
    }
}
