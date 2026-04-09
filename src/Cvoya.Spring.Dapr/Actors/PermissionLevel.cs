/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Actors;

/// <summary>
/// Represents the permission level of a human actor within the platform.
/// </summary>
public enum PermissionLevel
{
    /// <summary>
    /// The human can only view information and cannot send domain messages.
    /// </summary>
    Viewer,

    /// <summary>
    /// The human can view and interact with the platform by sending domain messages.
    /// </summary>
    Operator,

    /// <summary>
    /// The human has full administrative access to the platform.
    /// </summary>
    Owner
}
