// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Actors;

using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Orchestration;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Actors;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;
using global::Dapr.Actors.Runtime;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for <see cref="UnitActor.TransitionAsync"/> orchestration
/// wiring introduced in T-05: every transition into
/// <see cref="UnitStatus.Validating"/> must schedule the
/// <c>UnitValidationWorkflow</c> and persist the returned instance id to
/// <c>LastValidationRunId</c>. On the revalidate paths
/// (<see cref="UnitStatus.Error"/> → <see cref="UnitStatus.Validating"/>
/// and <see cref="UnitStatus.Stopped"/> → <see cref="UnitStatus.Validating"/>)
/// the tracker's <c>BeginRunAsync</c> also clears any stale
/// <c>LastValidationErrorJson</c> so observers see "clean slate + fresh
/// run id" rather than "new run id + stale error."
/// </summary>
public class UnitActorValidationSchedulingTests
{
    private const string TestUnitActorId = "test-unit";
    private const string UnitName = "eng-team";

    private readonly IActorStateManager _stateManager = Substitute.For<IActorStateManager>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly IOrchestrationStrategy _strategy = Substitute.For<IOrchestrationStrategy>();
    private readonly IActivityEventBus _activityEventBus = Substitute.For<IActivityEventBus>();
    private readonly IDirectoryService _directoryService = Substitute.For<IDirectoryService>();
    private readonly IActorProxyFactory _actorProxyFactory = Substitute.For<IActorProxyFactory>();
    private readonly IUnitValidationWorkflowScheduler _scheduler = Substitute.For<IUnitValidationWorkflowScheduler>();
    private readonly IUnitValidationTracker _validationTracker = Substitute.For<IUnitValidationTracker>();
    private readonly UnitActor _actor;

    public UnitActorValidationSchedulingTests()
    {
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());

        var host = ActorHost.CreateForTest<UnitActor>(new ActorTestOptions
        {
            ActorId = new ActorId(TestUnitActorId),
        });
        _actor = new UnitActor(
            host,
            _loggerFactory,
            _strategy,
            _activityEventBus,
            _directoryService,
            _actorProxyFactory,
            validationWorkflowScheduler: _scheduler,
            validationTracker: _validationTracker);
        SetStateManager(_actor, _stateManager);

        _scheduler
            .ScheduleAsync(TestUnitActorId, Arg.Any<CancellationToken>())
            .Returns(new UnitValidationSchedule("run-42", UnitName));
    }

    private static void SetStateManager(Actor actor, IActorStateManager stateManager)
    {
        var field = typeof(Actor).GetField("<StateManager>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field is not null)
        {
            field.SetValue(actor, stateManager);
        }
        else
        {
            var prop = typeof(Actor).GetProperty("StateManager");
            prop?.SetValue(actor, stateManager);
        }
    }

    private void WithCurrentStatus(UnitStatus current)
    {
        _stateManager.TryGetStateAsync<UnitStatus>(StateKeys.UnitStatus, Arg.Any<CancellationToken>())
            .Returns(new ConditionalValue<UnitStatus>(true, current));
    }

    [Fact]
    public async Task DraftToValidating_SchedulesWorkflow_PersistsRunId()
    {
        WithCurrentStatus(UnitStatus.Draft);

        var result = await _actor.TransitionAsync(
            UnitStatus.Validating, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.CurrentStatus.ShouldBe(UnitStatus.Validating);

        await _scheduler.Received(1).ScheduleAsync(
            TestUnitActorId, Arg.Any<CancellationToken>());
        await _validationTracker.Received(1).BeginRunAsync(
            TestUnitActorId, "run-42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ErrorToValidating_ClearsFailureBlob_SchedulesWorkflow()
    {
        WithCurrentStatus(UnitStatus.Error);

        var result = await _actor.TransitionAsync(
            UnitStatus.Validating, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        // BeginRunAsync's contract is to clear LastValidationErrorJson
        // atomically with writing the new run id — we verify the call
        // order (scheduler first, tracker second on the same actor-side
        // turn) and that both happened.
        await _scheduler.Received(1).ScheduleAsync(
            TestUnitActorId, Arg.Any<CancellationToken>());
        await _validationTracker.Received(1).BeginRunAsync(
            TestUnitActorId, "run-42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StoppedToValidating_SchedulesWorkflow_PersistsRunId()
    {
        WithCurrentStatus(UnitStatus.Stopped);

        var result = await _actor.TransitionAsync(
            UnitStatus.Validating, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        await _scheduler.Received(1).ScheduleAsync(
            TestUnitActorId, Arg.Any<CancellationToken>());
        await _validationTracker.Received(1).BeginRunAsync(
            TestUnitActorId, "run-42", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransitionToNonValidating_DoesNotScheduleWorkflow()
    {
        WithCurrentStatus(UnitStatus.Running);

        var result = await _actor.TransitionAsync(
            UnitStatus.Stopping, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        await _scheduler.DidNotReceive().ScheduleAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _validationTracker.DidNotReceive().BeginRunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisallowedTransitionToValidating_DoesNotScheduleWorkflow()
    {
        // Running -> Validating is not allowed per the state machine.
        WithCurrentStatus(UnitStatus.Running);

        var result = await _actor.TransitionAsync(
            UnitStatus.Validating, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        await _scheduler.DidNotReceive().ScheduleAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SchedulerThrows_TransitionStillApplied_NoRunIdPersisted()
    {
        // The state transition has to succeed even if the scheduler fails
        // — the unit ends up in Validating with no workflow actually
        // running, which an operator recovers by calling /revalidate.
        WithCurrentStatus(UnitStatus.Draft);
        _scheduler
            .ScheduleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<UnitValidationSchedule>(
                new InvalidOperationException("dapr down")));

        var result = await _actor.TransitionAsync(
            UnitStatus.Validating, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.CurrentStatus.ShouldBe(UnitStatus.Validating);

        await _validationTracker.DidNotReceive().BeginRunAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}