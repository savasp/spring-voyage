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
using Cvoya.Spring.Dapr.Actors;
using Cvoya.Spring.Host.Api.Models;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for the unit-scoped agent slot endpoints
/// (<c>GET/POST/PATCH/DELETE /api/v1/units/{id}/agents[/{agentId}]</c>).
/// </summary>
public class UnitAgentsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string UnitName = "engineering";
    private const string ActorId = "actor-engineering";

    // Server uses JsonStringEnumConverter (see Program.cs#134), so tests must
    // serialise / deserialise enums as strings too — default System.Text.Json
    // treats them as ordinals.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UnitAgentsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListUnitAgents_UnknownUnit_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        ArrangeMissingUnit();

        var response = await _client.GetAsync($"/api/v1/units/{UnitName}/agents", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListUnitAgents_ReturnsSlots()
    {
        var ct = TestContext.Current.CancellationToken;
        var proxy = ArrangeUnit();
        proxy.GetAgentSlotsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UnitAgentSlot>
            {
                new("ada", "claude-opus", "reviewer", Enabled: true, ExecutionMode: AgentExecutionMode.Auto),
                new("bob", null, null, Enabled: false, ExecutionMode: AgentExecutionMode.OnDemand),
            });

        var response = await _client.GetAsync($"/api/v1/units/{UnitName}/agents", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var slots = await response.Content.ReadFromJsonAsync<List<UnitAgentSlotResponse>>(JsonOptions, ct);
        slots.ShouldNotBeNull();
        slots!.Count.ShouldBe(2);
        slots.ShouldContain(s => s.AgentId == "ada" && s.Model == "claude-opus" && s.Enabled);
        slots.ShouldContain(s => s.AgentId == "bob" && !s.Enabled && s.ExecutionMode == AgentExecutionMode.OnDemand);
    }

    [Fact]
    public async Task AssignUnitAgent_DefaultBody_CreatesSlotWithDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        var proxy = ArrangeUnit();

        var response = await _client.PostAsync($"/api/v1/units/{UnitName}/agents/ada",
            JsonContent.Create(new { }), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await proxy.Received(1).AssignAgentAsync(
            Arg.Is<UnitAgentSlot>(s =>
                s.AgentId == "ada" &&
                s.Enabled == true &&
                s.ExecutionMode == AgentExecutionMode.Auto &&
                s.Model == null &&
                s.Specialty == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignUnitAgent_FullBody_CreatesSlotWithProvidedFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var proxy = ArrangeUnit();

        var body = new AssignAgentRequest(
            Model: "claude-opus",
            Specialty: "reviewer",
            Enabled: false,
            ExecutionMode: AgentExecutionMode.OnDemand);

        var response = await _client.PostAsync($"/api/v1/units/{UnitName}/agents/ada",
            JsonContent.Create(body, options: JsonOptions), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await proxy.Received(1).AssignAgentAsync(
            Arg.Is<UnitAgentSlot>(s =>
                s.AgentId == "ada" &&
                s.Model == "claude-opus" &&
                s.Specialty == "reviewer" &&
                s.Enabled == false &&
                s.ExecutionMode == AgentExecutionMode.OnDemand),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUnitAgent_MergesPartialPatch()
    {
        var ct = TestContext.Current.CancellationToken;
        var proxy = ArrangeUnit();

        // Existing slot: enabled=true, mode=Auto, specialty=reviewer, model=claude-opus.
        proxy.GetAgentSlotsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UnitAgentSlot>
            {
                new("ada", "claude-opus", "reviewer", Enabled: true, ExecutionMode: AgentExecutionMode.Auto),
            });

        // Patch flips enabled only; everything else should be inherited.
        var patch = new UpdateAgentSlotRequest(Enabled: false);
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/units/{UnitName}/agents/ada")
        {
            Content = JsonContent.Create(patch, options: JsonOptions),
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await proxy.Received(1).AssignAgentAsync(
            Arg.Is<UnitAgentSlot>(s =>
                s.AgentId == "ada" &&
                s.Enabled == false &&
                s.Model == "claude-opus" &&
                s.Specialty == "reviewer" &&
                s.ExecutionMode == AgentExecutionMode.Auto),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUnitAgent_UnknownSlot_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        var proxy = ArrangeUnit();
        proxy.GetAgentSlotsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<UnitAgentSlot>());

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/units/{UnitName}/agents/ghost")
        {
            Content = JsonContent.Create(new UpdateAgentSlotRequest(Enabled: false)),
        };

        var response = await _client.SendAsync(request, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await proxy.DidNotReceive().AssignAgentAsync(
            Arg.Any<UnitAgentSlot>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnassignUnitAgent_ReturnsNoContentAndCallsActor()
    {
        var ct = TestContext.Current.CancellationToken;
        var proxy = ArrangeUnit();

        var response = await _client.DeleteAsync($"/api/v1/units/{UnitName}/agents/ada", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await proxy.Received(1).UnassignAgentAsync("ada", Arg.Any<CancellationToken>());
    }

    private IUnitActor ArrangeUnit()
    {
        _factory.DirectoryService.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();

        var entry = new DirectoryEntry(
            new Address("unit", UnitName),
            ActorId,
            "Engineering",
            "Engineering unit",
            null,
            DateTimeOffset.UtcNow);

        _factory.DirectoryService
            .ResolveAsync(Arg.Is<Address>(a => a.Scheme == "unit" && a.Path == UnitName), Arg.Any<CancellationToken>())
            .Returns(entry);

        var proxy = Substitute.For<IUnitActor>();
        _factory.ActorProxyFactory
            .CreateActorProxy<IUnitActor>(Arg.Is<ActorId>(a => a.GetId() == ActorId), Arg.Any<string>())
            .Returns(proxy);

        return proxy;
    }

    private void ArrangeMissingUnit()
    {
        _factory.DirectoryService.ClearReceivedCalls();
        _factory.DirectoryService
            .ResolveAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns((DirectoryEntry?)null);
    }
}