// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Execution;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Dapr.Execution;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for <see cref="DbAgentDefinitionProvider.Merge"/> — the
/// field-level precedence rule behind the B-wide execution inheritance
/// model (#601 / #603 / #409).
/// </summary>
public class DbAgentDefinitionProviderMergeTests
{
    [Fact]
    public void Merge_AgentWins_OnEveryField()
    {
        var agent = new AgentExecutionConfig(
            Tool: "claude-code",
            Image: "agent-img",
            Runtime: "docker",
            Hosting: AgentHostingMode.Persistent,
            Provider: "anthropic",
            Model: "claude-sonnet");
        var unit = new UnitExecutionDefaults(
            Image: "unit-img",
            Runtime: "podman",
            Tool: "codex",
            Provider: "openai",
            Model: "gpt-4o");

        var merged = DbAgentDefinitionProvider.Merge(agent, unit);

        merged.ShouldNotBeNull();
        merged!.Tool.ShouldBe("claude-code");
        merged.Image.ShouldBe("agent-img");
        merged.Runtime.ShouldBe("docker");
        merged.Provider.ShouldBe("anthropic");
        merged.Model.ShouldBe("claude-sonnet");
        merged.Hosting.ShouldBe(AgentHostingMode.Persistent);
    }

    [Fact]
    public void Merge_UnitFillsIn_MissingAgentFields()
    {
        var agent = new AgentExecutionConfig(
            Tool: "claude-code",
            Image: null,      // missing
            Runtime: null,    // missing
            Hosting: AgentHostingMode.Ephemeral,
            Provider: null,
            Model: null);
        var unit = new UnitExecutionDefaults(
            Image: "unit-img",
            Runtime: "podman",
            Tool: "codex",    // ignored — agent wins on tool
            Provider: "openai",
            Model: "gpt-4o");

        var merged = DbAgentDefinitionProvider.Merge(agent, unit);

        merged.ShouldNotBeNull();
        merged!.Tool.ShouldBe("claude-code");
        merged.Image.ShouldBe("unit-img");
        merged.Runtime.ShouldBe("podman");
        merged.Provider.ShouldBe("openai");
        merged.Model.ShouldBe("gpt-4o");
    }

    [Fact]
    public void Merge_AgentNull_UnitProvidesTool_UsesUnit()
    {
        var unit = new UnitExecutionDefaults(
            Image: "unit-img",
            Tool: "claude-code");

        var merged = DbAgentDefinitionProvider.Merge(null, unit);

        merged.ShouldNotBeNull();
        merged!.Tool.ShouldBe("claude-code");
        merged.Image.ShouldBe("unit-img");
        merged.Hosting.ShouldBe(AgentHostingMode.Ephemeral);
    }

    [Fact]
    public void Merge_ReturnsNull_WhenNeitherSideProvidesTool()
    {
        var agent = new AgentExecutionConfig(Tool: "", Image: null);
        var unit = new UnitExecutionDefaults(Image: "unit-img");

        var merged = DbAgentDefinitionProvider.Merge(agent, unit);

        merged.ShouldBeNull();
    }

    [Fact]
    public void Merge_HostingIsAgentOwned_UnitNeverChangesIt()
    {
        var agent = new AgentExecutionConfig(
            Tool: "claude-code",
            Image: "x",
            Hosting: AgentHostingMode.Persistent);
        var unit = new UnitExecutionDefaults();

        var merged = DbAgentDefinitionProvider.Merge(agent, unit);

        merged.ShouldNotBeNull();
        merged!.Hosting.ShouldBe(AgentHostingMode.Persistent);
    }
}