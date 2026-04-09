/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Core.Execution;

/// <summary>
/// Provides the platform-level prompt layer (Layer 1) containing safety constraints,
/// tool descriptions, and behavioral guidance.
/// </summary>
public interface IPlatformPromptProvider
{
    /// <summary>
    /// Returns the platform prompt text.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The platform prompt string.</returns>
    Task<string> GetPlatformPromptAsync(CancellationToken cancellationToken = default);
}
