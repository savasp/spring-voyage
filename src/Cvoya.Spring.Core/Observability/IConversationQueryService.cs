// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Observability;

/// <summary>
/// Materialises conversation views from the observability layer. Conversations
/// are not stored as a separate aggregate — they are derived from activity
/// events whose <c>CorrelationId</c> carries the conversation id assigned by
/// the messaging layer. The service groups those events into the shapes the
/// CLI's <c>spring conversation</c> and <c>spring inbox</c> verbs (plus the
/// matching portal surfaces) need.
/// </summary>
public interface IConversationQueryService
{
    /// <summary>
    /// Lists conversation summaries matching the supplied filters, ordered by
    /// most-recent activity first. Returns an empty list when no activity
    /// events carry a non-null correlation id.
    /// </summary>
    /// <param name="filters">Optional filters; omitted fields match all conversations.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<ConversationSummary>> ListAsync(
        ConversationQueryFilters filters,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the full thread for a single conversation — the summary row plus
    /// every activity event correlated to the conversation id, ordered oldest
    /// first. Returns <c>null</c> when no events are found for the supplied id.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<ConversationDetail?> GetAsync(
        string conversationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists inbox rows for the supplied human address — conversations whose
    /// most recent event is a <c>MessageReceived</c> on the human and where no
    /// later event has been emitted by the same human. Rows drop off as soon
    /// as the human acts or the agent retracts.
    /// </summary>
    /// <param name="humanAddress">The <c>scheme://path</c> of the human whose inbox to load.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<IReadOnlyList<InboxItem>> ListInboxAsync(
        string humanAddress,
        CancellationToken cancellationToken);
}