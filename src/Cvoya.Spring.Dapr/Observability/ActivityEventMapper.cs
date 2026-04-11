// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Observability;

using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Dapr.Data.Entities;

/// <summary>
/// Maps between the Core <see cref="ActivityEvent"/> domain record and the EF
/// <see cref="ActivityEventRecord"/> entity used for persistence.
/// </summary>
public static class ActivityEventMapper
{
    /// <summary>
    /// Converts a domain <see cref="ActivityEvent"/> to a persistence <see cref="ActivityEventRecord"/>.
    /// </summary>
    public static ActivityEventRecord ToRecord(ActivityEvent activityEvent)
    {
        return new ActivityEventRecord
        {
            Id = activityEvent.Id,
            Source = $"{activityEvent.Source.Scheme}:{activityEvent.Source.Path}",
            EventType = activityEvent.EventType.ToString(),
            Severity = activityEvent.Severity.ToString(),
            Summary = activityEvent.Summary,
            Details = activityEvent.Details,
            CorrelationId = activityEvent.CorrelationId,
            Cost = activityEvent.Cost,
            Timestamp = activityEvent.Timestamp,
        };
    }

    /// <summary>
    /// Converts a persistence <see cref="ActivityEventRecord"/> back to a domain <see cref="ActivityEvent"/>.
    /// </summary>
    public static ActivityEvent ToDomain(ActivityEventRecord record)
    {
        var colonIndex = record.Source.IndexOf(':');
        var address = colonIndex >= 0
            ? new Address(record.Source[..colonIndex], record.Source[(colonIndex + 1)..])
            : new Address(record.Source, string.Empty);

        return new ActivityEvent(
            record.Id,
            record.Timestamp,
            address,
            Enum.Parse<ActivityEventType>(record.EventType),
            Enum.Parse<ActivitySeverity>(record.Severity),
            record.Summary,
            record.Details,
            record.CorrelationId,
            record.Cost);
    }
}