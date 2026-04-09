/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Actors;

/// <summary>
/// Represents the connection status of a connector actor.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// The connector is not connected to any external system.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The connector is actively connected and operational.
    /// </summary>
    Connected,

    /// <summary>
    /// The connector encountered an error and is in a failed state.
    /// </summary>
    Error
}
