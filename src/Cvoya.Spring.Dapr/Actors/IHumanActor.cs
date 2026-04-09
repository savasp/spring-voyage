/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Actors;

using Cvoya.Spring.Core.Messaging;
using global::Dapr.Actors;

/// <summary>
/// Dapr actor interface for human actors.
/// Humans represent platform users with identity, permissions, and notification preferences.
/// </summary>
public interface IHumanActor : IActor
{
    /// <summary>
    /// Receives and processes a message, optionally returning a response.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An optional response message, or <c>null</c> if no response is needed.</returns>
    Task<Message?> ReceiveAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current permission level of the human actor.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current <see cref="PermissionLevel"/>.</returns>
    Task<PermissionLevel> GetPermissionAsync(CancellationToken cancellationToken = default);
}
