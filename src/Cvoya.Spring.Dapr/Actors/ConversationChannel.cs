/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Actors;

using Cvoya.Spring.Core.Messaging;

/// <summary>
/// Represents a conversation channel within an agent's partitioned mailbox.
/// Each channel holds messages for a single conversation, identified by <see cref="ConversationId"/>.
/// </summary>
public class ConversationChannel
{
    /// <summary>
    /// Gets the unique identifier for this conversation.
    /// </summary>
    public string ConversationId { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the queue of messages awaiting processing in this conversation.
    /// </summary>
    public List<Message> Messages { get; set; } = [];

    /// <summary>
    /// Gets or sets the timestamp when this conversation was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
