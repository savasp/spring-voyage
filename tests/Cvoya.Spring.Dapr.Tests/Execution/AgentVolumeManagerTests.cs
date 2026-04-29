// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Execution;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Dapr.Execution;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for <see cref="AgentVolumeManager"/> (D3c — ADR-0029).
/// Verifies provisioning, reclamation, metrics emission, and mount-string
/// construction without a real container runtime.
/// </summary>
public class AgentVolumeManagerTests
{
    private readonly IContainerRuntime _runtime = Substitute.For<IContainerRuntime>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly AgentVolumeManager _manager;

    public AgentVolumeManagerTests()
    {
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _manager = new AgentVolumeManager(_runtime, _loggerFactory);
    }

    // ── EnsureAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task EnsureAsync_CallsRuntimeEnsureVolume()
    {
        var volumeName = await _manager.EnsureAsync("agent-x", TestContext.Current.CancellationToken);

        await _runtime.Received(1).EnsureVolumeAsync(volumeName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureAsync_ReturnsVolumeNameDerivedFromAgentId()
    {
        var volumeName = await _manager.EnsureAsync("my-agent", TestContext.Current.CancellationToken);

        volumeName.ShouldBe("spring-ws-my-agent");
    }

    [Fact]
    public async Task EnsureAsync_CalledTwiceForSameAgent_ReturnsConsistentName()
    {
        var first = await _manager.EnsureAsync("agent-dup", TestContext.Current.CancellationToken);
        var second = await _manager.EnsureAsync("agent-dup", TestContext.Current.CancellationToken);

        first.ShouldBe(second);
    }

    // ── ReclaimAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReclaimAsync_CallsRuntimeRemoveVolume()
    {
        await _manager.EnsureAsync("agent-reclaim", TestContext.Current.CancellationToken);

        await _manager.ReclaimAsync("agent-reclaim", TestContext.Current.CancellationToken);

        await _runtime.Received(1).RemoveVolumeAsync(
            "spring-ws-agent-reclaim", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReclaimAsync_RuntimeFailure_DoesNotThrow()
    {
        _runtime.RemoveVolumeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("simulated remove failure"));

        await Should.NotThrowAsync(() =>
            _manager.ReclaimAsync("agent-failing", TestContext.Current.CancellationToken));
    }

    // ── RecordVolumeMetricsAsync ───────────────────────────────────────────

    [Fact]
    public async Task RecordVolumeMetricsAsync_NoTrackedVolumes_SkipsRuntimeQuery()
    {
        await _manager.RecordVolumeMetricsAsync(TestContext.Current.CancellationToken);

        await _runtime.DidNotReceive().GetVolumeMetricsAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordVolumeMetricsAsync_AfterEnsure_QueriesMetrics()
    {
        _runtime.GetVolumeMetricsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VolumeMetrics(SizeBytes: 1024L, LastWrite: DateTimeOffset.UtcNow));

        await _manager.EnsureAsync("agent-metrics", TestContext.Current.CancellationToken);
        await _manager.RecordVolumeMetricsAsync(TestContext.Current.CancellationToken);

        await _runtime.Received(1).GetVolumeMetricsAsync(
            "spring-ws-agent-metrics", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordVolumeMetricsAsync_AfterReclaim_NoLongerQueriesReclaimedVolume()
    {
        _runtime.GetVolumeMetricsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new VolumeMetrics(SizeBytes: 512L, LastWrite: null));

        await _manager.EnsureAsync("agent-gone", TestContext.Current.CancellationToken);
        await _manager.ReclaimAsync("agent-gone", TestContext.Current.CancellationToken);
        await _manager.RecordVolumeMetricsAsync(TestContext.Current.CancellationToken);

        // RemoveVolumeAsync was called during ReclaimAsync (mock may fail so ignore)
        await _runtime.DidNotReceive().GetVolumeMetricsAsync(
            "spring-ws-agent-gone", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordVolumeMetricsAsync_RuntimeFailureOnMetrics_DoesNotThrow()
    {
        _runtime.GetVolumeMetricsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("metrics timeout"));

        await _manager.EnsureAsync("agent-metrics-fail", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(() =>
            _manager.RecordVolumeMetricsAsync(TestContext.Current.CancellationToken));
    }

    // ── BuildVolumeMount ──────────────────────────────────────────────────

    [Fact]
    public void BuildVolumeMount_ReturnsColonSeparatedNameAndPath()
    {
        var mount = AgentVolumeManager.BuildVolumeMount("spring-ws-abc");

        mount.ShouldBe($"spring-ws-abc:{AgentVolumeManager.WorkspaceMountPath}");
    }

    [Fact]
    public void BuildVolumeMount_MountPathEndsWithSlash()
    {
        var mount = AgentVolumeManager.BuildVolumeMount("vol");

        mount.ShouldEndWith("/");
    }

    // ── Constants ─────────────────────────────────────────────────────────

    [Fact]
    public void WorkspaceMountPath_IsSpringWorkspace()
    {
        AgentVolumeManager.WorkspaceMountPath.ShouldBe("/spring/workspace/");
    }

    [Fact]
    public void WorkspacePathEnvVar_IsSpringWorkspacePath()
    {
        AgentVolumeManager.WorkspacePathEnvVar.ShouldBe("SPRING_WORKSPACE_PATH");
    }
}