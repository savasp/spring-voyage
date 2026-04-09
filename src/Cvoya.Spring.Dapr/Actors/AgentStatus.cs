/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Actors;

/// <summary>
/// Represents the current status of an agent actor.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// The agent has no active conversations and is waiting for work.
    /// </summary>
    Idle,

    /// <summary>
    /// The agent has an active conversation being processed.
    /// </summary>
    Active,

    /// <summary>
    /// The agent's active conversation has been suspended (e.g., due to a higher-priority conversation).
    /// </summary>
    Suspended
}
