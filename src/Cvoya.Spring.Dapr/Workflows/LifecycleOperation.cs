/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

/// <summary>
/// Specifies the lifecycle operation to perform on an agent.
/// </summary>
public enum LifecycleOperation
{
    /// <summary>
    /// Create and register a new agent.
    /// </summary>
    Create,

    /// <summary>
    /// Deactivate, unregister, and clean up an existing agent.
    /// </summary>
    Delete
}
