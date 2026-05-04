// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Orchestration;

using System.Text.Json;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Orchestration;
using Cvoya.Spring.Dapr.Orchestration;

using Microsoft.Extensions.Logging;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for <see cref="AiOrchestrationStrategy"/>.
/// </summary>
public class AiOrchestrationStrategyTests
{
    private readonly IAiProvider _aiProvider = Substitute.For<IAiProvider>();
    private readonly IAiProviderRegistry _registry = Substitute.For<IAiProviderRegistry>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly IUnitContext _context = Substitute.For<IUnitContext>();
    private readonly AiOrchestrationStrategy _strategy;

    public AiOrchestrationStrategyTests()
    {
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        // Default the substituted IAiProvider's Id so logs / fall-back paths
        // have something deterministic to assert against.
        _aiProvider.Id.Returns("test-default");
        _strategy = new AiOrchestrationStrategy(_registry, _aiProvider, _loggerFactory);

        _context.UnitAddress.Returns(Address.For("unit", TestSlugIds.HexFor("test-unit")));
        // Existing tests don't exercise per-unit provider routing (#1696);
        // leave ProviderId null so the strategy falls through to the
        // injected default provider — matching the pre-#1696 behaviour
        // these tests were written against.
        _context.ProviderId.Returns((string?)null);
    }

    private static Message CreateMessage(
        MessageType type = MessageType.Domain,
        string? threadId = null)
    {
        return new Message(
            Guid.NewGuid(),
            Address.For("agent", TestSlugIds.HexFor("test-sender")),
            Address.For("unit", TestSlugIds.HexFor("test-unit")),
            type,
            threadId ?? Guid.NewGuid().ToString(),
            JsonSerializer.SerializeToElement(new { Task = "process data" }),
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task OrchestrateAsync_CallsAiProviderWithMemberContext()
    {
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        var member2 = Address.For("agent", TestSlugIds.HexFor("agent-2"));
        _context.Members.Returns([member1, member2]);
        _aiProvider.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns($"agent://{TestSlugIds.HexFor("agent-1")}");

        var message = CreateMessage();
        await _strategy.OrchestrateAsync(message, _context, TestContext.Current.CancellationToken);

        await _aiProvider.Received(1).CompleteAsync(
            Arg.Is<string>(p => p.Contains($"agent://{TestSlugIds.HexFor("agent-1")}") && p.Contains($"agent://{TestSlugIds.HexFor("agent-2")}")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrchestrateAsync_ForwardsToMemberReturnedByAi()
    {
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        var member2 = Address.For("agent", TestSlugIds.HexFor("agent-2"));
        _context.Members.Returns([member1, member2]);
        _aiProvider.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns($"agent://{TestSlugIds.HexFor("agent-2")}");

        var expectedResponse = CreateMessage(threadId: "response");
        _context.SendAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var message = CreateMessage();
        var result = await _strategy.OrchestrateAsync(message, _context, TestContext.Current.CancellationToken);

        result.ShouldBe(expectedResponse);
        await _context.Received(1).SendAsync(
            Arg.Is<Message>(m => m.To == member2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrchestrateAsync_ReturnsNull_WhenAiResponseDoesNotMatchAnyMember()
    {
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        _context.Members.Returns([member1]);
        _aiProvider.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("unknown://nonexistent");

        var message = CreateMessage();
        var result = await _strategy.OrchestrateAsync(message, _context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _context.DidNotReceive().SendAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrchestrateAsync_ReturnsNull_WhenNoMembers()
    {
        _context.Members.Returns([]);

        var message = CreateMessage();
        var result = await _strategy.OrchestrateAsync(message, _context, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _aiProvider.DidNotReceive().CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BuildRoutingPrompt_IncludesAllMembers()
    {
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        var member2 = Address.For("unit", TestSlugIds.HexFor("sub-unit"));
        _context.Members.Returns([member1, member2]);

        var message = CreateMessage();
        var prompt = AiOrchestrationStrategy.BuildRoutingPrompt(message, _context);

        prompt.ShouldContain($"agent://{TestSlugIds.HexFor("agent-1")}");
        prompt.ShouldContain($"unit://{TestSlugIds.HexFor("sub-unit")}");
        prompt.ShouldContain("process data");
    }

    [Fact]
    public void ParseRoutingDecision_FindsMatchingMember()
    {
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        var member2 = Address.For("agent", TestSlugIds.HexFor("agent-2"));
        var members = new List<Address> { member1, member2 };

        var result = AiOrchestrationStrategy.ParseRoutingDecision($"agent://{TestSlugIds.HexFor("agent-2")}", members);

        result.ShouldBe(member2);
    }

    [Fact]
    public void ParseRoutingDecision_ReturnsNull_WhenNoMatch()
    {
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        var members = new List<Address> { member1 };

        var result = AiOrchestrationStrategy.ParseRoutingDecision("unknown://foo", members);

        result.ShouldBeNull();
    }

    // ── #1696: per-unit provider routing through IAiProviderRegistry ──

    [Fact]
    public async Task OrchestrateAsync_ResolvesProviderFromContextProviderId()
    {
        // Unit declares execution.provider = "ollama" (surfaced via
        // IUnitContext.ProviderId). The strategy must hit the registry,
        // not the default IAiProvider — otherwise per-unit routing is a
        // no-op and #1696 isn't fixed.
        var ollama = Substitute.For<IAiProvider>();
        ollama.Id.Returns("ollama");
        ollama.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns($"agent://{TestSlugIds.HexFor("agent-1")}");
        _registry.Get("ollama").Returns(ollama);

        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        _context.Members.Returns([member1]);
        _context.ProviderId.Returns("ollama");

        await _strategy.OrchestrateAsync(CreateMessage(), _context, TestContext.Current.CancellationToken);

        await ollama.Received(1).CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _aiProvider.DidNotReceive().CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OrchestrateAsync_FallsBackToDefaultProvider_WhenContextProviderIdIsNull()
    {
        // Unit has no execution.provider declared (legacy units, or units
        // built before the field was first-class). Strategy uses the
        // default IAiProvider — preserving pre-#1696 behaviour for any
        // unit that hasn't been migrated.
        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        _context.Members.Returns([member1]);
        _context.ProviderId.Returns((string?)null);
        _aiProvider.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns($"agent://{TestSlugIds.HexFor("agent-1")}");

        await _strategy.OrchestrateAsync(CreateMessage(), _context, TestContext.Current.CancellationToken);

        await _aiProvider.Received(1).CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        _registry.DidNotReceive().Get(Arg.Any<string>());
    }

    [Fact]
    public async Task OrchestrateAsync_FallsBackToDefaultProvider_WhenContextProviderIdIsUnknown()
    {
        // Unit declares a provider id that no registered IAiProvider
        // claims — typo, retired runtime, etc. Falls back to the default
        // provider with a warning so the operator sees an actionable log
        // line, instead of a silent dispatch to the wrong endpoint.
        _registry.Get("phantom").Returns((IAiProvider?)null);

        var member1 = Address.For("agent", TestSlugIds.HexFor("agent-1"));
        _context.Members.Returns([member1]);
        _context.ProviderId.Returns("phantom");
        _aiProvider.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns($"agent://{TestSlugIds.HexFor("agent-1")}");

        await _strategy.OrchestrateAsync(CreateMessage(), _context, TestContext.Current.CancellationToken);

        await _aiProvider.Received(1).CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}