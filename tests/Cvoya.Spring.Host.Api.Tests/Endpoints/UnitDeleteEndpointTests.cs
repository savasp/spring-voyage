// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Tests.Endpoints;

using System.Net;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Actors;

using FluentAssertions;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;

using NSubstitute;

using Xunit;

/// <summary>
/// Integration tests for <c>DELETE /api/v1/units/{id}</c>. The endpoint must
/// refuse deletion while the unit is Running/Starting/Stopping/Error (#116) so
/// the container, sidecar, and network are never orphaned.
/// </summary>
public class UnitDeleteEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string UnitName = "engineering";
    private const string ActorId = "actor-engineering";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UnitDeleteEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DeleteUnit_Stopped_Returns204AndUnregisters()
    {
        var ct = TestContext.Current.CancellationToken;
        ArrangeUnit(UnitStatus.Stopped);

        var response = await _client.DeleteAsync($"/api/v1/units/{UnitName}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.DirectoryService.Received(1).UnregisterAsync(
            Arg.Is<Address>(a => a.Scheme == "unit" && a.Path == UnitName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUnit_Draft_Returns204AndUnregisters()
    {
        var ct = TestContext.Current.CancellationToken;
        ArrangeUnit(UnitStatus.Draft);

        var response = await _client.DeleteAsync($"/api/v1/units/{UnitName}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await _factory.DirectoryService.Received(1).UnregisterAsync(
            Arg.Is<Address>(a => a.Scheme == "unit" && a.Path == UnitName),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UnitStatus.Running)]
    [InlineData(UnitStatus.Starting)]
    [InlineData(UnitStatus.Stopping)]
    [InlineData(UnitStatus.Error)]
    public async Task DeleteUnit_NotStopped_Returns409AndDoesNotUnregister(UnitStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        ArrangeUnit(status);

        var response = await _client.DeleteAsync($"/api/v1/units/{UnitName}", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _factory.DirectoryService.DidNotReceive().UnregisterAsync(
            Arg.Any<Address>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUnit_Unknown_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        _factory.DirectoryService.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
        _factory.DirectoryService
            .ResolveAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns((DirectoryEntry?)null);

        var response = await _client.DeleteAsync("/api/v1/units/does-not-exist", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private void ArrangeUnit(UnitStatus status)
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
        proxy.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(status);

        _factory.ActorProxyFactory
            .CreateActorProxy<IUnitActor>(Arg.Is<ActorId>(a => a.GetId() == ActorId), Arg.Any<string>())
            .Returns(proxy);
    }
}