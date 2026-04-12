// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Execution;

using System.Collections.Concurrent;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Units;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="IUnitContainerLifecycle"/> implementation that adapts <see cref="ContainerLifecycleManager"/>
/// into the simpler start/stop surface used by the unit API. Tracks the sidecar and network produced by each
/// start so the corresponding stop can dispatch them to <see cref="ContainerLifecycleManager.TeardownAsync"/>.
/// </summary>
public class UnitContainerLifecycle(
    ContainerLifecycleManager lifecycleManager,
    IOptions<UnitRuntimeOptions> options,
    ILoggerFactory loggerFactory) : IUnitContainerLifecycle
{
    // TODO(#81 follow-up): lifecycle handles are in-memory only. They should be persisted to survive API host restarts.
    private readonly ConcurrentDictionary<string, UnitLifecycleHandle> _handles = new(StringComparer.Ordinal);
    private readonly ILogger<UnitContainerLifecycle> _logger = loggerFactory.CreateLogger<UnitContainerLifecycle>();
    private readonly UnitRuntimeOptions _options = options.Value;

    /// <inheritdoc />
    public async Task StartUnitAsync(string unitId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

        var appId = BuildAppId(unitId);

        var config = new ContainerConfig(
            Image: _options.Image,
            DaprEnabled: true,
            DaprAppId: appId,
            DaprAppPort: _options.AppPort,
            Labels: new Dictionary<string, string>
            {
                ["spring.unit.id"] = unitId,
            });

        _logger.LogInformation(
            "Launching container for unit {UnitId} with app-id {AppId} using image {Image}",
            unitId, appId, _options.Image);

        var result = await lifecycleManager.LaunchWithSidecarAsync(config, ct);

        var handle = new UnitLifecycleHandle(
            result.ContainerResult.ContainerId,
            result.SidecarInfo.SidecarId,
            result.NetworkName);

        _handles[unitId] = handle;
    }

    /// <inheritdoc />
    public async Task StopUnitAsync(string unitId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unitId);

        if (!_handles.TryRemove(unitId, out var handle))
        {
            _logger.LogWarning(
                "No lifecycle handle tracked for unit {UnitId}; issuing teardown with null identifiers.",
                unitId);
            handle = new UnitLifecycleHandle(null, null, null);
        }

        await lifecycleManager.TeardownAsync(
            handle.ContainerId,
            handle.SidecarId,
            handle.NetworkName,
            ct);
    }

    private static string BuildAppId(string unitId)
    {
        var raw = $"spring-unit-{unitId}";
        return raw.Length > 32 ? raw[..32] : raw;
    }

    private sealed record UnitLifecycleHandle(string? ContainerId, string? SidecarId, string? NetworkName);
}