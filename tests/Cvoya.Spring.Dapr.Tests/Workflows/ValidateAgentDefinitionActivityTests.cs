/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Tests.Workflows;

using Cvoya.Spring.Dapr.Workflows;
using Cvoya.Spring.Dapr.Workflows.Activities;
using global::Dapr.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ValidateAgentDefinitionActivity"/>.
/// </summary>
public class ValidateAgentDefinitionActivityTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ValidateAgentDefinitionActivity _activity;

    public ValidateAgentDefinitionActivityTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _activity = new ValidateAgentDefinitionActivity(_loggerFactory);
    }

    [Fact]
    public async Task RunAsync_ValidCreateInput_ReturnsTrue()
    {
        var input = new AgentLifecycleInput(
            LifecycleOperation.Create, "agent-1", AgentName: "Ada", Role: "backend-engineer");
        var context = Substitute.For<WorkflowActivityContext>();

        var result = await _activity.RunAsync(context, input);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_EmptyAgentId_ReturnsFalse()
    {
        var input = new AgentLifecycleInput(
            LifecycleOperation.Create, "", AgentName: "Ada");
        var context = Substitute.For<WorkflowActivityContext>();

        var result = await _activity.RunAsync(context, input);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_CreateWithoutAgentName_ReturnsFalse()
    {
        var input = new AgentLifecycleInput(
            LifecycleOperation.Create, "agent-1");
        var context = Substitute.For<WorkflowActivityContext>();

        var result = await _activity.RunAsync(context, input);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_DeleteWithoutAgentName_ReturnsTrue()
    {
        var input = new AgentLifecycleInput(
            LifecycleOperation.Delete, "agent-1");
        var context = Substitute.For<WorkflowActivityContext>();

        var result = await _activity.RunAsync(context, input);

        result.Should().BeTrue();
    }
}
