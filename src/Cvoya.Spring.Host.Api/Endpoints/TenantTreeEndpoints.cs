// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using System.Globalization;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Tenancy;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Host.Api.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        CancellationToken cancellationToken)
    {
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

        var unitNodes = unitEntries
            .Select(u => BuildUnitNode(u, membershipsByUnit, agentEntries, primaryByAgent))
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

        return new TenantTreeNode(
            Id: unitPath,
            Name: string.IsNullOrWhiteSpace(unit.DisplayName) ? unitPath : unit.DisplayName,
            Kind: "Unit",
            Status: "running",
            Desc: string.IsNullOrWhiteSpace(unit.Description) ? null : unit.Description,
            Children: agentNodes);
    }

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