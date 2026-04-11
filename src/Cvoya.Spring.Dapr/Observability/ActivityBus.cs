// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Observability;

using System.Reactive.Subjects;

using Cvoya.Spring.Core.Capabilities;

/// <summary>
/// Platform-wide activity event bus that provides a hot observable stream.
/// Dapr pub/sub subscription handlers call <see cref="Publish"/> to push events
/// to all subscribers.
/// </summary>
public class ActivityBus : IActivityObservable, IDisposable
{
    private readonly Subject<ActivityEvent> _subject = new();

    /// <inheritdoc />
    public IObservable<ActivityEvent> ActivityStream => _subject;

    /// <summary>
    /// Publishes an activity event to all subscribers.
    /// </summary>
    /// <param name="activityEvent">The event to publish.</param>
    public void Publish(ActivityEvent activityEvent) => _subject.OnNext(activityEvent);

    /// <inheritdoc />
    public void Dispose() => _subject.Dispose();
}