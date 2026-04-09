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
/// Dapr actor interface for connector actors.
/// Connectors bridge external systems (e.g., GitHub, Slack) into the Spring Voyage platform.
/// </summary>
public interface IConnectorActor : IActor
{
    /// <summary>
    /// Receives and processes a message, optionally returning a response.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An optional response message, or <c>null</c> if no response is needed.</returns>
    Task<Message?> ReceiveAsync(Message message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current connection status of the connector.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The current <see cref="ConnectionStatus"/>.</returns>
    Task<ConnectionStatus> GetConnectionStatusAsync(CancellationToken cancellationToken = default);
}
