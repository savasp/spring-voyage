// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using System.Globalization;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Tenancy;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Actors;
using Cvoya.Spring.Host.Api.Models;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Maps the tenant-tree API surface introduced in SVR-tenant-tree (plan
/// §3 / §5). Exposes <c>GET /api/v1/tenant/tree</c> — a single-payload
/// snapshot of the tenant's units, agents, and multi-parent alias edges
/// that drives the canonical <c>/units</c> Explorer surface on the
/// frontend. Size budget is documented in <c>#815</c> §3 (≤500 nodes).
/// </summary>
public static class TenantTreeEndpoints
{
    /// <summary>Cache-Control window for the tree payload. Short enough to
    /// absorb Cmd-K + dashboard fanout, long enough to ride out the typical
    /// operator navigation bounce between Explorer tabs without re-fetching.</summary>
    private const int CacheMaxAgeSeconds = 15;

    /// <summary>
    /// Registers the tenant-tree endpoint. Call from <c>Program.cs</c>
    /// alongside <c>MapDashboardEndpoints</c>. Returns a single
    /// <see cref="RouteGroupBuilder"/> so callers can apply
    /// <c>RequireAuthorization()</c> uniformly.
    /// </summary>
    public static RouteGroupBuilder MapTenantTreeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty);

        group.MapGet("/api/v1/tenant/tree", GetTenantTreeAsync)
            .WithTags("Tenant")
            .WithName("GetTenantTree")
            .WithSummary("Synthesized tenant → units → agents tree with multi-parent alias edges")
            .Produces<TenantTreeResponse>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> GetTenantTreeAsync(
        HttpContext httpContext,
        [FromServices] IDirectoryService directoryService,
        [FromServices] IUnitMembershipRepository memberships,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IActorProxyFactory actorProxyFactory,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Cvoya.Spring.Host.Api.Endpoints.TenantTreeEndpoints");
        var tenantId = tenantContext.CurrentTenantId;
        var entries = await directoryService.ListAllAsync(cancellationToken);

        var unitEntries = entries
            .Where(e => string.Equals(e.Address.Scheme, "unit", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Address.Path, StringComparer.Ordinal)
            .ToList();

        var agentEntries = entries
            .Where(e => string.Equals(e.Address.Scheme, "agent", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(e => e.Address.Path, StringComparer.Ordinal);

        var allMemberships = await memberships.ListAllAsync(cancellationToken);

        // Primary-parent lookup keyed on agent address. There is exactly one
        // row with IsPrimary = true per agent post-migration (see
        // SVR-membership), so the dictionary collapses cleanly.
        var primaryByAgent = allMemberships
            .Where(m => m.IsPrimary)
            .ToDictionary(m => m.AgentAddress, m => m.UnitId, StringComparer.Ordinal);

        // Memberships grouped by unit. For multi-parent agents the same
        // agent address appears in several buckets — that's the desired
        // aliasing; the frontend disambiguates via PrimaryParentId.
        var membershipsByUnit = allMemberships
            .GroupBy(m => m.UnitId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        // #1032: look up the real lifecycle status for each unit via its
        // actor. Previously every unit was pinned to "running", which left
        // operators looking at a green dot and the badge text "Running"
        // even for Draft units that can't accept dispatches. Dashboard
        // endpoints already pay this per-unit actor round-trip (see
        // DashboardEndpoints.GetUnitsSummaryAsync) and the cache-control
        // window on this endpoint (15s) absorbs the fanout.
        var unitStatuses = new Dictionary<string, UnitStatus>(StringComparer.Ordinal);
        foreach (var unit in unitEntries)
        {
            unitStatuses[unit.Address.Path] =
                await TryGetUnitStatusAsync(actorProxyFactory, unit.ActorId, logger, unit.Address.Path, cancellationToken);
        }

        var unitNodes = unitEntries
            .Select(u => BuildUnitNode(u, unitStatuses, membershipsByUnit, agentEntries, primaryByAgent))
            .ToList();

        var tenantNode = new TenantTreeNode(
            Id: $"tenant://{tenantId}",
            Name: tenantId,
            Kind: "Tenant",
            Status: "running",
            Children: unitNodes);

        httpContext.Response.Headers.CacheControl =
            $"private, max-age={CacheMaxAgeSeconds.ToString(CultureInfo.InvariantCulture)}";

        return Results.Ok(new TenantTreeResponse(tenantNode));
    }

    private static TenantTreeNode BuildUnitNode(
        DirectoryEntry unit,
        IReadOnlyDictionary<string, UnitStatus> unitStatuses,
        IReadOnlyDictionary<string, List<UnitMembership>> membershipsByUnit,
        IReadOnlyDictionary<string, DirectoryEntry> agentEntries,
        IReadOnlyDictionary<string, string> primaryByAgent)
    {
        var unitPath = unit.Address.Path;
        var rows = membershipsByUnit.TryGetValue(unitPath, out var list)
            ? list
            : new List<UnitMembership>();

        var agentNodes = rows
            .Where(m => m.Enabled)
            .Select(m => BuildAgentNode(m, agentEntries, primaryByAgent))
            .Where(n => n is not null)
            .Cast<TenantTreeNode>()
            .ToList();

        var status = unitStatuses.TryGetValue(unitPath, out var persisted)
            ? persisted
            : UnitStatus.Draft;

        return new TenantTreeNode(
            Id: unitPath,
            Name: string.IsNullOrWhiteSpace(unit.DisplayName) ? unitPath : unit.DisplayName,
            Kind: "Unit",
            Status: ToWireStatus(status),
            Desc: string.IsNullOrWhiteSpace(unit.Description) ? null : unit.Description,
            Children: agentNodes);
    }

    /// <summary>
    /// Read a unit's persisted status from its actor. Mirrors the fallback
    /// policy in <see cref="DashboardEndpoints.GetUnitsSummaryAsync"/>: a
    /// missing or unreachable actor collapses to <see cref="UnitStatus.Draft"/>
    /// so the tree still renders rather than failing the whole fetch.
    /// </summary>
    private static async Task<UnitStatus> TryGetUnitStatusAsync(
        IActorProxyFactory actorProxyFactory,
        string actorId,
        ILogger logger,
        string unitPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var proxy = actorProxyFactory.CreateActorProxy<IUnitActor>(
                new ActorId(actorId), nameof(UnitActor));
            return await proxy.GetStatusAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to read persisted status for unit {UnitPath}; reporting Draft in tenant tree.",
                unitPath);
            return UnitStatus.Draft;
        }
    }

    /// <summary>
    /// Maps the <see cref="UnitStatus"/> lifecycle enum to the lowercase
    /// wire vocabulary consumed by <c>src/lib/api/validate-tenant-tree.ts</c>
    /// on the portal. Kept next to the unit-node builder so a new enum
    /// value fails the wire-status switch fast instead of silently leaking
    /// into the tree as <c>stopped</c>.
    /// </summary>
    private static string ToWireStatus(UnitStatus status) => status switch
    {
        UnitStatus.Draft => "draft",
        UnitStatus.Stopped => "stopped",
        UnitStatus.Starting => "starting",
        UnitStatus.Running => "running",
        UnitStatus.Stopping => "stopping",
        UnitStatus.Error => "error",
        UnitStatus.Validating => "validating",
        _ => "stopped",
    };

    private static TenantTreeNode? BuildAgentNode(
        UnitMembership membership,
        IReadOnlyDictionary<string, DirectoryEntry> agentEntries,
        IReadOnlyDictionary<string, string> primaryByAgent)
    {
        // An agent might have a membership row but no directory entry
        // (transient during registration). Skip it rather than render a
        // half-typed node; the next fetch (15 s later) will pick it up.
        if (!agentEntries.TryGetValue(membership.AgentAddress, out var agent))
        {
            return null;
        }

        primaryByAgent.TryGetValue(membership.AgentAddress, out var primary);

        return new TenantTreeNode(
            Id: agent.Address.Path,
            Name: string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.Address.Path : agent.DisplayName,
            Kind: "Agent",
            Status: "running",
            Desc: string.IsNullOrWhiteSpace(agent.Description) ? null : agent.Description,
            Role: agent.Role,
            PrimaryParentId: primary);
    }
}