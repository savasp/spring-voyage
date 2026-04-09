/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Workflows;

/// <summary>
/// Output from the <see cref="CloningLifecycleWorkflow"/>.
/// </summary>
/// <param name="Success">Whether the cloning operation completed successfully.</param>
/// <param name="Error">An error message when the operation fails.</param>
public record CloningOutput(
    bool Success,
    string? Error = null);
