// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Observability;

using System.Reactive.Linq;
using System.Text.Json;

using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Dapr.Observability;

using FluentAssertions;

using Xunit;

public class ActivityEventBusTests
{
    private static ActivityEvent CreateEvent(
        ActivityEventType type = ActivityEventType.MessageReceived,
        ActivitySeverity severity = ActivitySeverity.Info,
        string summary = "test event")
    {
        return new ActivityEvent(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new Address("agent", "test-agent"),
            type,
            severity,
            summary);
    }

    [Fact]
    public void Publish_WithSubscriber_DeliversEvent()
    {
        using var bus = new ActivityEventBus();
        ActivityEvent? received = null;
        using var sub = bus.Events.Subscribe(e => received = e);

        var published = CreateEvent();
        bus.Publish(published);

        received.Should().Be(published);
    }

    [Fact]
    public void Publish_MultipleSubscribers_AllReceiveEvent()
    {
        using var bus = new ActivityEventBus();
        ActivityEvent? received1 = null;
        ActivityEvent? received2 = null;
        using var sub1 = bus.Events.Subscribe(e => received1 = e);
        using var sub2 = bus.Events.Subscribe(e => received2 = e);

        var published = CreateEvent();
        bus.Publish(published);

        received1.Should().Be(published);
        received2.Should().Be(published);
    }

    [Fact]
    public void Publish_NoSubscribers_DoesNotThrow()
    {
        using var bus = new ActivityEventBus();

        var act = () => bus.Publish(CreateEvent());

        act.Should().NotThrow();
    }

    [Fact]
    public void Publish_MultipleEvents_AllDeliveredInOrder()
    {
        using var bus = new ActivityEventBus();
        var received = new List<ActivityEvent>();
        using var sub = bus.Events.Subscribe(e => received.Add(e));

        var event1 = CreateEvent(summary: "first");
        var event2 = CreateEvent(summary: "second");
        var event3 = CreateEvent(summary: "third");
        bus.Publish(event1);
        bus.Publish(event2);
        bus.Publish(event3);

        received.Should().HaveCount(3);
        received[0].Summary.Should().Be("first");
        received[1].Summary.Should().Be("second");
        received[2].Summary.Should().Be("third");
    }

    [Fact]
    public void Subscribe_FilterBySeverity_OnlyReceivesMatching()
    {
        using var bus = new ActivityEventBus();
        var warnings = new List<ActivityEvent>();
        using var sub = bus.Events
            .Where(e => e.Severity >= ActivitySeverity.Warning)
            .Subscribe(e => warnings.Add(e));

        bus.Publish(CreateEvent(severity: ActivitySeverity.Debug));
        bus.Publish(CreateEvent(severity: ActivitySeverity.Info));
        bus.Publish(CreateEvent(severity: ActivitySeverity.Warning));
        bus.Publish(CreateEvent(severity: ActivitySeverity.Error));

        warnings.Should().HaveCount(2);
        warnings.Should().OnlyContain(e => e.Severity >= ActivitySeverity.Warning);
    }

    [Fact]
    public void Dispose_AfterDispose_SubscribersCompleted()
    {
        var bus = new ActivityEventBus();
        var completed = false;
        using var sub = bus.Events.Subscribe(_ => { }, () => completed = true);

        bus.Dispose();

        completed.Should().BeTrue();
    }
}