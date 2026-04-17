// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Observability;

using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Observability;
using Cvoya.Spring.Dapr.Data;
using Cvoya.Spring.Dapr.Data.Entities;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Default <see cref="IConversationQueryService"/>. Conversations are
/// materialised from the existing <see cref="SpringDbContext.ActivityEvents"/>
/// table: each activity event persisted with a non-null
/// <see cref="ActivityEventRecord.CorrelationId"/> carries the conversation id
/// assigned by the messaging layer. Grouping those events by correlation id
/// reconstructs the thread without a separate message store.
/// </summary>
/// <remarks>
/// This is an observability projection — it intentionally reads only from the
/// activity table, which is the single place the platform already persists
/// conversation-correlated events. A future PR can add a dedicated message
/// table (see #410) and swap this service out; every call site depends on the
/// interface only.
/// </remarks>
public class ConversationQueryService(SpringDbContext dbContext) : IConversationQueryService
{
    private static readonly string[] TerminalEventTypes =
    {
        nameof(ActivityEventType.ConversationCompleted),
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationSummary>> ListAsync(
        ConversationQueryFilters filters,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ActivityEvents
            .Where(e => e.CorrelationId != null)
            .Select(e => new ConversationEventRow(
                e.CorrelationId!, e.Id, e.Source, e.EventType, e.Severity, e.Summary, e.Timestamp))
            .ToListAsync(cancellationToken);

        var summaries = BuildSummaries(rows);
        summaries = ApplyFilters(summaries, filters);

        var limit = filters.Limit is > 0 ? filters.Limit.Value : 50;
        return summaries
            .OrderByDescending(s => s.LastActivity)
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ConversationDetail?> GetAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return null;
        }

        var rows = await dbContext.ActivityEvents
            .Where(e => e.CorrelationId == conversationId)
            .OrderBy(e => e.Timestamp)
            .Select(e => new ConversationEventRow(
                e.CorrelationId!, e.Id, e.Source, e.EventType, e.Severity, e.Summary, e.Timestamp))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        var summary = BuildSummaryForConversation(conversationId, rows);
        var events = rows
            .Select(r => new ConversationEvent(
                r.Id, r.Timestamp, NormaliseSource(r.Source), r.EventType, r.Severity, r.Summary))
            .ToList();

        return new ConversationDetail(summary, events);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InboxItem>> ListInboxAsync(
        string humanAddress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(humanAddress))
        {
            return [];
        }

        // Normalise the caller's address — accept either "human://savasp" or
        // the persistence-layer shape "human:savasp" that activity events use.
        var humanSourceCanonical = ToPersistenceSource(humanAddress);
        var humanSourceDisplay = NormaliseSource(humanSourceCanonical);

        var rows = await dbContext.ActivityEvents
            .Where(e => e.CorrelationId != null)
            .Select(e => new ConversationEventRow(
                e.CorrelationId!, e.Id, e.Source, e.EventType, e.Severity, e.Summary, e.Timestamp))
            .ToListAsync(cancellationToken);

        var inbox = new List<InboxItem>();

        foreach (var group in rows.GroupBy(r => r.ConversationId))
        {
            // A conversation is "in my inbox" when the most recent event on it
            // is a MessageReceived emitted by MY human address and no later
            // event from the same human cleared it.
            var ordered = group.OrderBy(r => r.Timestamp).ToList();
            var last = ordered[^1];

            if (!string.Equals(last.EventType, nameof(ActivityEventType.MessageReceived), StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(last.Source, humanSourceCanonical, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Find the "from" — the most recent non-human event before the ask.
            var from = ordered
                .TakeWhile(r => r != last)
                .Where(r => !r.Source.StartsWith("human:", StringComparison.OrdinalIgnoreCase))
                .Select(r => NormaliseSource(r.Source))
                .LastOrDefault() ?? NormaliseSource(last.Source);

            inbox.Add(new InboxItem(
                ConversationId: group.Key,
                From: from,
                Human: humanSourceDisplay,
                PendingSince: last.Timestamp,
                Summary: last.Summary));
        }

        return inbox
            .OrderByDescending(i => i.PendingSince)
            .ToList();
    }

    private static IReadOnlyList<ConversationSummary> BuildSummaries(List<ConversationEventRow> rows)
    {
        var summaries = new List<ConversationSummary>();
        foreach (var group in rows.GroupBy(r => r.ConversationId))
        {
            summaries.Add(BuildSummaryForConversation(group.Key, group.OrderBy(r => r.Timestamp).ToList()));
        }
        return summaries;
    }

    private static ConversationSummary BuildSummaryForConversation(
        string conversationId,
        IReadOnlyList<ConversationEventRow> ordered)
    {
        var first = ordered[0];
        var last = ordered[^1];
        var isCompleted = ordered.Any(r =>
            TerminalEventTypes.Contains(r.EventType));

        var participants = ordered
            .Select(r => NormaliseSource(r.Source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ConversationSummary(
            Id: conversationId,
            Participants: participants,
            Status: isCompleted ? "completed" : "active",
            LastActivity: last.Timestamp,
            CreatedAt: first.Timestamp,
            EventCount: ordered.Count,
            Origin: NormaliseSource(first.Source),
            Summary: first.Summary);
    }

    private static IReadOnlyList<ConversationSummary> ApplyFilters(
        IReadOnlyList<ConversationSummary> summaries,
        ConversationQueryFilters filters)
    {
        IEnumerable<ConversationSummary> query = summaries;

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            query = query.Where(s =>
                string.Equals(s.Status, filters.Status, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filters.Unit))
        {
            var needle = $"unit://{filters.Unit}";
            query = query.Where(s =>
                s.Participants.Any(p => string.Equals(p, needle, StringComparison.OrdinalIgnoreCase))
                || string.Equals(s.Origin, needle, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filters.Agent))
        {
            var needle = $"agent://{filters.Agent}";
            query = query.Where(s =>
                s.Participants.Any(p => string.Equals(p, needle, StringComparison.OrdinalIgnoreCase))
                || string.Equals(s.Origin, needle, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filters.Participant))
        {
            var needle = filters.Participant;
            query = query.Where(s =>
                s.Participants.Any(p => string.Equals(p, needle, StringComparison.OrdinalIgnoreCase)));
        }

        return query.ToList();
    }

    /// <summary>
    /// Turns the persistence-layer source format (<c>scheme:path</c>) into the
    /// wire-friendly <c>scheme://path</c> form. The activity mapper stores
    /// sources with a single colon to keep the column compact; the CLI and
    /// portal render the full URI.
    /// </summary>
    internal static string NormaliseSource(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        var colon = source.IndexOf(':');
        if (colon < 0)
        {
            return source;
        }

        // Already in "scheme://path" form.
        if (source.AsSpan(colon + 1).StartsWith("//"))
        {
            return source;
        }

        var scheme = source[..colon];
        var path = source[(colon + 1)..];
        return $"{scheme}://{path}";
    }

    /// <summary>
    /// Accepts either <c>scheme://path</c> or <c>scheme:path</c> and returns
    /// the persistence-layer form (<c>scheme:path</c>) so we can compare
    /// against <see cref="ActivityEventRecord.Source"/> values.
    /// </summary>
    internal static string ToPersistenceSource(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            return address;
        }

        var split = address.IndexOf("://", StringComparison.Ordinal);
        if (split < 0)
        {
            return address;
        }

        return string.Concat(address.AsSpan(0, split), ":", address.AsSpan(split + 3));
    }

    private sealed record ConversationEventRow(
        string ConversationId,
        Guid Id,
        string Source,
        string EventType,
        string Severity,
        string Summary,
        DateTimeOffset Timestamp);
}