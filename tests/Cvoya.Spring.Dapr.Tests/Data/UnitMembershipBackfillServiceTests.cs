// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Data;

using Cvoya.Spring.Core.Agents;
using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Actors;
using Cvoya.Spring.Dapr.Data;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for the one-shot <see cref="UnitMembershipBackfillService"/>
/// (#160 / C2b-1). Verifies that every agent with a legacy ParentUnit
/// state produces a membership row and that repeat runs are idempotent.
/// </summary>
public class UnitMembershipBackfillServiceTests
{
    [Fact]
    public async Task StartAsync_BackfillDisabled_DoesNothing()
    {
        var ctx = CreateContext();
        var directory = Substitute.For<IDirectoryService>();
        var proxyFactory = Substitute.For<IActorProxyFactory>();
        var service = CreateService(ctx, directory, proxyFactory, enabled: false);

        await service.StartAsync(TestContext.Current.CancellationToken);

        await directory.DidNotReceive().ListAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_CreatesMembershipPerAgentWithParentUnit()
    {
        var ctx = CreateContext();
        var directory = Substitute.For<IDirectoryService>();
        var proxyFactory = Substitute.For<IActorProxyFactory>();

        directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry>
            {
                new(new Address("agent", "ada"), "actor-ada", "ada", "desc", null, DateTimeOffset.UtcNow),
                new(new Address("agent", "hopper"), "actor-hopper", "hopper", "desc", null, DateTimeOffset.UtcNow),
                new(new Address("unit", "engineering"), "actor-eng", "eng", "desc", null, DateTimeOffset.UtcNow),
            });

        StubAgentMetadata(proxyFactory, "actor-ada", new AgentMetadata(ParentUnit: "engineering"));
        StubAgentMetadata(proxyFactory, "actor-hopper", new AgentMetadata(ParentUnit: "marketing"));

        var service = CreateService(ctx, directory, proxyFactory);
        await service.StartAsync(TestContext.Current.CancellationToken);

        var repo = new UnitMembershipRepository(ctx);
        (await repo.GetAsync("engineering", "ada", CancellationToken.None)).ShouldNotBeNull();
        (await repo.GetAsync("marketing", "hopper", CancellationToken.None)).ShouldNotBeNull();
    }

    [Fact]
    public async Task StartAsync_SkipsAgentsWithoutParentUnit()
    {
        var ctx = CreateContext();
        var directory = Substitute.For<IDirectoryService>();
        var proxyFactory = Substitute.For<IActorProxyFactory>();

        directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry>
            {
                new(new Address("agent", "loner"), "actor-loner", "loner", "desc", null, DateTimeOffset.UtcNow),
            });

        StubAgentMetadata(proxyFactory, "actor-loner", new AgentMetadata());

        var service = CreateService(ctx, directory, proxyFactory);
        await service.StartAsync(TestContext.Current.CancellationToken);

        var repo = new UnitMembershipRepository(ctx);
        (await repo.ListByAgentAsync("loner", CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task StartAsync_Idempotent_DoesNotOverwriteExistingRow()
    {
        var ctx = CreateContext();
        var repo = new UnitMembershipRepository(ctx);
        // Pre-seed a membership with a per-membership override that must survive.
        await repo.UpsertAsync(
            new UnitMembership("engineering", "ada",
                Model: "custom-model",
                Specialty: "reviewer",
                Enabled: false),
            CancellationToken.None);

        var directory = Substitute.For<IDirectoryService>();
        var proxyFactory = Substitute.For<IActorProxyFactory>();
        directory.ListAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DirectoryEntry>
            {
                new(new Address("agent", "ada"), "actor-ada", "ada", "desc", null, DateTimeOffset.UtcNow),
            });
        StubAgentMetadata(proxyFactory, "actor-ada", new AgentMetadata(ParentUnit: "engineering"));

        var service = CreateService(ctx, directory, proxyFactory);
        await service.StartAsync(TestContext.Current.CancellationToken);

        var persisted = await repo.GetAsync("engineering", "ada", CancellationToken.None);
        persisted.ShouldNotBeNull();
        persisted!.Model.ShouldBe("custom-model");
        persisted.Specialty.ShouldBe("reviewer");
        persisted.Enabled.ShouldBeFalse();
    }

    private static SpringDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SpringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SpringDbContext(options);
    }

    private static void StubAgentMetadata(
        IActorProxyFactory factory, string actorId, AgentMetadata metadata)
    {
        var proxy = Substitute.For<IAgentActor>();
        proxy.GetMetadataAsync(Arg.Any<CancellationToken>()).Returns(metadata);
        factory.CreateActorProxy<IAgentActor>(
                Arg.Is<ActorId>(a => a.GetId() == actorId),
                Arg.Any<string>())
            .Returns(proxy);
    }

    private static UnitMembershipBackfillService CreateService(
        SpringDbContext ctx,
        IDirectoryService directory,
        IActorProxyFactory proxyFactory,
        bool enabled = true)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitMembershipRepository>(_ => new UnitMembershipRepository(ctx));
        var provider = services.BuildServiceProvider();

        var options = Options.Create(new DatabaseOptions { BackfillMemberships = enabled });
        return new UnitMembershipBackfillService(
            provider, directory, proxyFactory, options,
            Substitute.For<ILogger<UnitMembershipBackfillService>>());
    }
}