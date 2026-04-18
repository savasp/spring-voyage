// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Skills;

using System.Text.Json;

using Cvoya.Spring.Core;
using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Skills;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Skills;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for <see cref="AgentAsSkillRegistry"/> covering the extension-seam
/// contract from #359: each registered agent is advertised as a skill, tool
/// metadata is carried through, invocation routes through the shared
/// <see cref="IMessageRouter"/>, and agents hidden by a unit's outside-the-
/// boundary view (#413) are not advertised.
/// </summary>
public class AgentAsSkillRegistryTests
{
    private readonly IDirectoryService _directory = Substitute.For<IDirectoryService>();
    private readonly IMessageRouter _router = Substitute.For<IMessageRouter>();
    private readonly IUnitMembershipRepository _memberships = Substitute.For<IUnitMembershipRepository>();
    private readonly IExpertiseAggregator _aggregator = Substitute.For<IExpertiseAggregator>();
    private readonly TimeProvider _time = TimeProvider.System;

    private AgentAsSkillRegistry CreateRegistry() => new(
        _directory,
        _router,
        new DirectMembershipLookup(_memberships),
        _aggregator,
        _time,
        NullLoggerFactory.Instance);

    /// <summary>
    /// Test-only adapter that exposes a pre-built
    /// <see cref="IUnitMembershipRepository"/> through the registry's
    /// internal lookup seam, bypassing the DI-scope factory used in
    /// production.
    /// </summary>
    private sealed class DirectMembershipLookup(IUnitMembershipRepository repository) : AgentAsSkillRegistry.IMembershipLookup
    {
        public Task<IReadOnlyList<UnitMembership>> ListByAgentAsync(string agentAddress, CancellationToken cancellationToken)
            => repository.ListByAgentAsync(agentAddress, cancellationToken);
    }

    [Fact]
    public void Name_ReturnsStableIdentifier()
    {
        CreateRegistry().Name.ShouldBe("agents");
    }

    [Fact]
    public async Task GetToolDefinitions_AdvertisesOneToolPerAgent()
    {
        var ada = NewAgentEntry("ada", "Ada", "Backend engineer", role: "backend-engineer");
        var kay = NewAgentEntry("kay", "Kay", "Frontend engineer", role: null);

        _directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry> { ada, kay });
        _memberships.ListByAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UnitMembership>());

        var registry = CreateRegistry();
        var tools = await registry.GetToolDefinitionsAsync(CancellationToken.None);

        tools.Select(t => t.Name).ShouldBe(new[] { "agent_ada", "agent_kay" });
        var adaTool = tools[0];
        adaTool.Description.ShouldContain("Backend engineer");
        adaTool.Description.ShouldContain("backend-engineer");
    }

    [Fact]
    public async Task GetToolDefinitions_SkipsNonAgentDirectoryEntries()
    {
        var ada = NewAgentEntry("ada", "Ada", "desc", role: null);
        var unit = new DirectoryEntry(
            new Address("unit", "engineering-team"),
            ActorId: "engineering-team",
            DisplayName: "Engineering Team",
            Description: "desc",
            Role: null,
            RegisteredAt: DateTimeOffset.UtcNow);

        _directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry> { ada, unit });
        _memberships.ListByAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UnitMembership>());

        var tools = await CreateRegistry().GetToolDefinitionsAsync(CancellationToken.None);

        tools.Count.ShouldBe(1);
        tools[0].Name.ShouldBe("agent_ada");
    }

    [Fact]
    public async Task GetToolDefinitions_HidesAgentWhenBoundaryStripsEveryContribution()
    {
        var ada = NewAgentEntry("ada", "Ada", "desc", role: null);
        _directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry> { ada });
        _memberships.ListByAgentAsync("ada", Arg.Any<CancellationToken>())
            .Returns(new List<UnitMembership>
            {
                new UnitMembership("engineering-team", "ada"),
            });

        var unitAddress = new Address("unit", "engineering-team");
        var insideSnapshot = new AggregatedExpertise(
            unitAddress,
            new List<ExpertiseEntry>
            {
                new ExpertiseEntry(
                    new ExpertiseDomain("csharp", string.Empty, ExpertiseLevel.Advanced),
                    new Address("agent", "ada"),
                    new[] { unitAddress, new Address("agent", "ada") }),
            },
            Depth: 1,
            ComputedAt: DateTimeOffset.UtcNow);
        var externalSnapshot = insideSnapshot with
        {
            Entries = Array.Empty<ExpertiseEntry>(),
        };

        _aggregator
            .GetAsync(unitAddress, BoundaryViewContext.InsideUnit, Arg.Any<CancellationToken>())
            .Returns(insideSnapshot);
        _aggregator
            .GetAsync(unitAddress, BoundaryViewContext.External, Arg.Any<CancellationToken>())
            .Returns(externalSnapshot);

        var tools = await CreateRegistry().GetToolDefinitionsAsync(CancellationToken.None);

        tools.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetToolDefinitions_AdvertisesAgentWhenBoundaryLeavesAnyContribution()
    {
        var ada = NewAgentEntry("ada", "Ada", "desc", role: null);
        _directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry> { ada });
        _memberships.ListByAgentAsync("ada", Arg.Any<CancellationToken>())
            .Returns(new List<UnitMembership>
            {
                new UnitMembership("engineering-team", "ada"),
            });

        var unitAddress = new Address("unit", "engineering-team");
        var agentAddress = new Address("agent", "ada");
        var sharedEntry = new ExpertiseEntry(
            new ExpertiseDomain("csharp", string.Empty, ExpertiseLevel.Advanced),
            agentAddress,
            new[] { unitAddress, agentAddress });

        var snapshot = new AggregatedExpertise(
            unitAddress,
            new List<ExpertiseEntry> { sharedEntry },
            Depth: 1,
            ComputedAt: DateTimeOffset.UtcNow);

        _aggregator
            .GetAsync(unitAddress, BoundaryViewContext.InsideUnit, Arg.Any<CancellationToken>())
            .Returns(snapshot);
        _aggregator
            .GetAsync(unitAddress, BoundaryViewContext.External, Arg.Any<CancellationToken>())
            .Returns(snapshot);

        var tools = await CreateRegistry().GetToolDefinitionsAsync(CancellationToken.None);

        tools.Select(t => t.Name).ShouldBe(new[] { "agent_ada" });
    }

    [Fact]
    public async Task GetToolDefinitions_AdvertisesAgentWhenAggregatorThrows()
    {
        var ada = NewAgentEntry("ada", "Ada", "desc", role: null);
        _directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry> { ada });
        _memberships.ListByAgentAsync("ada", Arg.Any<CancellationToken>())
            .Returns(new List<UnitMembership>
            {
                new UnitMembership("engineering-team", "ada"),
            });

        _aggregator
            .GetAsync(Arg.Any<Address>(), Arg.Any<BoundaryViewContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("boom"));

        var tools = await CreateRegistry().GetToolDefinitionsAsync(CancellationToken.None);

        tools.Select(t => t.Name).ShouldBe(new[] { "agent_ada" });
    }

    [Fact]
    public async Task InvokeAsync_RoutesMessageToAgentAndReturnsPayload()
    {
        var registry = CreateRegistry();
        _memberships.ListByAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UnitMembership>());

        var responsePayload = JsonSerializer.SerializeToElement(new { ok = true, echo = "hello" });
        Message? captured = null;
        _router.RouteAsync(Arg.Do<Message>(m => captured = m), Arg.Any<CancellationToken>())
            .Returns(Result<Message?, RoutingError>.Success(
                new Message(
                    Guid.NewGuid(),
                    new Address("agent", "ada"),
                    new Address("platform", "agent-as-skill"),
                    MessageType.Domain,
                    ConversationId: "conv-1",
                    Payload: responsePayload,
                    Timestamp: DateTimeOffset.UtcNow)));

        var args = JsonSerializer.SerializeToElement(new
        {
            message = "hello",
            conversationId = "conv-1",
        });
        var result = await registry.InvokeAsync("agent_ada", args, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured!.To.Scheme.ShouldBe("agent");
        captured.To.Path.ShouldBe("ada");
        captured.ConversationId.ShouldBe("conv-1");
        captured.Type.ShouldBe(MessageType.Domain);

        result.GetProperty("ok").GetBoolean().ShouldBeTrue();
        result.GetProperty("echo").GetString().ShouldBe("hello");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsErrorPayloadWhenRoutingFails()
    {
        var registry = CreateRegistry();
        _memberships.ListByAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UnitMembership>());

        var error = RoutingError.AddressNotFound(new Address("agent", "ghost"));
        _router.RouteAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
            .Returns(Result<Message?, RoutingError>.Failure(error));

        var args = JsonSerializer.SerializeToElement(new { message = "x" });
        var result = await registry.InvokeAsync("agent_ghost", args, CancellationToken.None);

        var errorText = result.GetProperty("error").GetString();
        errorText.ShouldNotBeNull();
        errorText.ShouldContain("ADDRESS_NOT_FOUND");
    }

    [Fact]
    public async Task InvokeAsync_UnknownToolPrefix_Throws()
    {
        var registry = CreateRegistry();
        await Should.ThrowAsync<SkillNotFoundException>(() =>
            registry.InvokeAsync("github_list_issues", JsonDocument.Parse("{}").RootElement, CancellationToken.None));
    }

    [Fact]
    public async Task InvokeAsync_DeniedWhenBoundaryHidesAgent()
    {
        var registry = CreateRegistry();

        _memberships.ListByAgentAsync("ada", Arg.Any<CancellationToken>())
            .Returns(new List<UnitMembership>
            {
                new UnitMembership("engineering-team", "ada"),
            });

        var unitAddress = new Address("unit", "engineering-team");
        var agentAddress = new Address("agent", "ada");
        var insideSnapshot = new AggregatedExpertise(
            unitAddress,
            new List<ExpertiseEntry>
            {
                new ExpertiseEntry(
                    new ExpertiseDomain("csharp", string.Empty, ExpertiseLevel.Advanced),
                    agentAddress,
                    new[] { unitAddress, agentAddress }),
            },
            Depth: 1,
            ComputedAt: DateTimeOffset.UtcNow);
        var externalSnapshot = insideSnapshot with
        {
            Entries = Array.Empty<ExpertiseEntry>(),
        };

        _aggregator
            .GetAsync(unitAddress, BoundaryViewContext.InsideUnit, Arg.Any<CancellationToken>())
            .Returns(insideSnapshot);
        _aggregator
            .GetAsync(unitAddress, BoundaryViewContext.External, Arg.Any<CancellationToken>())
            .Returns(externalSnapshot);

        var args = JsonSerializer.SerializeToElement(new { message = "x" });
        await Should.ThrowAsync<SkillNotFoundException>(() =>
            registry.InvokeAsync("agent_ada", args, CancellationToken.None));

        await _router.DidNotReceiveWithAnyArgs().RouteAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void NormaliseAndDenormaliseAgentPath_AreInverses()
    {
        AgentAsSkillRegistry.NormaliseAgentPath("backend-team/ada").ShouldBe("backend-team__ada");
        AgentAsSkillRegistry.DenormaliseAgentPath("backend-team__ada").ShouldBe("backend-team/ada");
    }

    private static DirectoryEntry NewAgentEntry(string id, string displayName, string description, string? role)
    {
        return new DirectoryEntry(
            new Address("agent", id),
            ActorId: id,
            DisplayName: displayName,
            Description: description,
            Role: role,
            RegisteredAt: DateTimeOffset.UtcNow);
    }
}