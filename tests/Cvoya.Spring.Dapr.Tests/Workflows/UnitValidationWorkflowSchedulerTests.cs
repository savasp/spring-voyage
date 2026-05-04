// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Workflows;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Dapr.Workflows;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for <see cref="UnitValidationWorkflowScheduler"/>'s agent-runtime
/// id resolution (#1683). The fix routes the agent-runtime registry id
/// through <see cref="UnitExecutionDefaults.Agent"/> (sourced from the
/// manifest's <c>ai.agent</c> field) and only falls back to
/// <see cref="UnitExecutionDefaults.Runtime"/> /
/// <see cref="UnitExecutionDefaults.Provider"/> for back-compat with
/// units persisted before the slot existed.
/// </summary>
public class UnitValidationWorkflowSchedulerTests
{
    [Fact]
    public void ResolveAgentRuntimeId_PrefersAgent_OverRuntime()
    {
        var defaults = new UnitExecutionDefaults(
            Runtime: "podman",
            Agent: "claude");

        var runtimeId = UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(defaults);

        runtimeId.ShouldBe("claude");
    }

    [Fact]
    public void ResolveAgentRuntimeId_FallsBackToRuntime_WhenAgentNull()
    {
        // Back-compat: a unit persisted before #1683 lacks the `agent`
        // slot and had an agent-runtime id (e.g. "ollama") in `runtime`.
        var defaults = new UnitExecutionDefaults(
            Runtime: "ollama",
            Agent: null);

        var runtimeId = UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(defaults);

        runtimeId.ShouldBe("ollama");
    }

    [Fact]
    public void ResolveAgentRuntimeId_SkipsContainerRuntimeSelectors_InRuntimeSlot()
    {
        // "podman" and "docker" are container-runtime selectors, not agent-runtime
        // ids. A unit created from scratch without a manifest sets Runtime="podman"
        // and Agent=null; the resolver must skip the Runtime slot and fall through
        // to Provider (or return null) rather than returning "podman" and causing
        // every RunContainerProbeActivity to fail with ProbeInternalError.
        var podmanDefaults = new UnitExecutionDefaults(Runtime: "podman");
        var dockerDefaults = new UnitExecutionDefaults(Runtime: "docker");

        UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(podmanDefaults).ShouldBeNull();
        UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(dockerDefaults).ShouldBeNull();
    }

    [Fact]
    public void ResolveAgentRuntimeId_SkipsContainerRuntimeSelector_FallsToProvider()
    {
        // When Runtime is a container-runtime selector and Provider is set,
        // Provider wins — this is the path for units with Provider="ollama" but
        // no Agent field (e.g. units created via the API before Agent was exposed).
        var defaults = new UnitExecutionDefaults(
            Runtime: "podman",
            Provider: "ollama");

        var runtimeId = UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(defaults);

        runtimeId.ShouldBe("ollama");
    }

    [Fact]
    public void ResolveAgentRuntimeId_FallsBackToProvider_WhenAgentAndRuntimeNull()
    {
        // Last-ditch: dapr-agent-style runtimes carry the same string in
        // their `provider` and `id` slots so a unit declaring only
        // `provider: openai` still resolves cleanly.
        var defaults = new UnitExecutionDefaults(
            Provider: "openai");

        var runtimeId = UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(defaults);

        runtimeId.ShouldBe("openai");
    }

    [Fact]
    public void ResolveAgentRuntimeId_ReturnsNull_WhenAllThreeSlotsEmpty()
    {
        var defaults = new UnitExecutionDefaults();

        var runtimeId = UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(defaults);

        runtimeId.ShouldBeNull();
    }

    [Fact]
    public void ResolveAgentRuntimeId_TreatsWhitespaceAgent_AsUnset_FallsToRuntime()
    {
        // Whitespace Agent is treated as unset; Runtime is used as back-compat
        // fallback when it holds an agent-runtime id (non-container-runtime value).
        var defaults = new UnitExecutionDefaults(
            Runtime: "ollama",
            Agent: "   ");

        var runtimeId = UnitValidationWorkflowScheduler.ResolveAgentRuntimeId(defaults);

        runtimeId.ShouldBe("ollama");
    }
}