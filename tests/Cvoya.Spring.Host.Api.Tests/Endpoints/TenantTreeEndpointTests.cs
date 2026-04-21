// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Host.Api.Models;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for <c>GET /api/v1/tenant/tree</c> (SVR-tenant-tree,
/// umbrella #815). Covers the synthesized root, unit-agent nesting,
/// multi-parent alias edges, and the <c>primaryParentId</c> flag.
/// </summary>
public class TenantTreeEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TenantTreeEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTenantTree_EmptyTenant_ReturnsJustTheTenantRoot()
    {
        var ct = TestContext.Current.CancellationToken;
        ClearMemberships();
        ArrangeDirectoryEntries();

        var response = await _client.GetAsync("/api/v1/tenant/tree", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TenantTreeResponse>(JsonOptions, ct);
        body.ShouldNotBeNull();
        body!.Tree.Kind.ShouldBe("Tenant");
        body.Tree.Id.ShouldStartWith("tenant://");
        (body.Tree.Children ?? []).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantTree_SetsCacheControlHeader()
    {
        var ct = TestContext.Current.CancellationToken;
        ClearMemberships();
        ArrangeDirectoryEntries();

        var response = await _client.GetAsync("/api/v1/tenant/tree", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl!.Private.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task GetTenantTree_NestsAgentsUnderEveryParentUnit()
    {
        var ct = TestContext.Current.CancellationToken;
        ClearMemberships();
        ArrangeDirectoryEntries(
            units: [("engineering", "Engineering"), ("marketing", "Marketing")],
            agents: [("ada", "Ada Lovelace", "reviewer")]);

        // Ada is a multi-parent agent — belongs to both engineering (primary
        // by virtue of being the first insert) and marketing.
        await UpsertMembershipAsync("engineering", "ada");
        await Task.Delay(10, ct);
        await UpsertMembershipAsync("marketing", "ada");

        var body = await FetchTreeAsync(ct);
        var tenant = body!.Tree;
        tenant.Children!.Count.ShouldBe(2);

        var engineering = tenant.Children!.Single(u => u.Id == "engineering");
        var marketing = tenant.Children!.Single(u => u.Id == "marketing");

        engineering.Children!.Single(a => a.Id == "ada").PrimaryParentId.ShouldBe("engineering");
        marketing.Children!.Single(a => a.Id == "ada").PrimaryParentId.ShouldBe("engineering");
    }

    [Fact]
    public async Task GetTenantTree_OmitsDisabledMemberships()
    {
        var ct = TestContext.Current.CancellationToken;
        ClearMemberships();
        ArrangeDirectoryEntries(
            units: [("engineering", "Engineering")],
            agents: [("ada", "Ada", null), ("hopper", "Grace", null)]);

        await UpsertMembershipAsync("engineering", "ada");
        await UpsertMembershipAsync("engineering", "hopper", enabled: false);

        var body = await FetchTreeAsync(ct);
        var engineering = body!.Tree.Children!.Single(u => u.Id == "engineering");
        engineering.Children!.Select(a => a.Id).ShouldBe(["ada"]);
    }

    [Fact]
    public async Task GetTenantTree_AgentWithNoDirectoryEntry_IsOmitted()
    {
        // Transient state during registration: the membership row lands
        // before the directory entry. The endpoint must skip rather than
        // emit a half-formed node.
        var ct = TestContext.Current.CancellationToken;
        ClearMemberships();
        ArrangeDirectoryEntries(units: [("engineering", "Engineering")]);

        await UpsertMembershipAsync("engineering", "ghost-agent");

        var body = await FetchTreeAsync(ct);
        var engineering = body!.Tree.Children!.Single(u => u.Id == "engineering");
        (engineering.Children ?? []).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTenantTree_SurfacesAgentRoleFromDirectoryEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        ClearMemberships();
        ArrangeDirectoryEntries(
            units: [("engineering", "Engineering")],
            agents: [("ada", "Ada Lovelace", "reviewer")]);
        await UpsertMembershipAsync("engineering", "ada");

        var body = await FetchTreeAsync(ct);
        var engineering = body!.Tree.Children!.Single(u => u.Id == "engineering");
        var ada = engineering.Children!.Single(a => a.Id == "ada");
        ada.Role.ShouldBe("reviewer");
        ada.Name.ShouldBe("Ada Lovelace");
    }

    private async Task<TenantTreeResponse?> FetchTreeAsync(CancellationToken ct)
    {
        var response = await _client.GetAsync("/api/v1/tenant/tree", ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<TenantTreeResponse>(JsonOptions, ct);
    }

    private void ClearMemberships()
    {
        _factory.DirectoryService.ClearReceivedCalls();
        _factory.DirectoryService
            .ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DirectoryEntry>());

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<Cvoya.Spring.Dapr.Data.SpringDbContext>();
        ctx.UnitMemberships.RemoveRange(ctx.UnitMemberships.ToList());
        ctx.SaveChanges();
    }

    private void ArrangeDirectoryEntries(
        (string Path, string DisplayName)[]? units = null,
        (string Path, string DisplayName, string? Role)[]? agents = null)
    {
        var list = new List<DirectoryEntry>();
        foreach (var (path, displayName) in units ?? Array.Empty<(string, string)>())
        {
            list.Add(new DirectoryEntry(
                Address: new Address("unit", path),
                ActorId: $"actor-{path}",
                DisplayName: displayName,
                Description: string.Empty,
                Role: null,
                RegisteredAt: DateTimeOffset.UtcNow));
        }
        foreach (var (path, displayName, role) in agents ?? Array.Empty<(string, string, string?)>())
        {
            list.Add(new DirectoryEntry(
                Address: new Address("agent", path),
                ActorId: $"actor-{path}",
                DisplayName: displayName,
                Description: string.Empty,
                Role: role,
                RegisteredAt: DateTimeOffset.UtcNow));
        }

        _factory.DirectoryService
            .ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(list);
    }

    private async Task UpsertMembershipAsync(string unitId, string agentAddress, bool enabled = true)
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IUnitMembershipRepository>();
        await repo.UpsertAsync(
            new UnitMembership(unitId, agentAddress, Enabled: enabled),
            CancellationToken.None);
    }
}