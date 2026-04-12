// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Units;

/// <summary>
/// Result of a unit lifecycle transition attempt.
/// </summary>
/// <param name="Success">True if the transition was permitted and applied; false if it was rejected.</param>
/// <param name="CurrentStatus">The unit's status after the attempt. On rejection, this is the unchanged prior status.</param>
/// <param name="RejectionReason">Human-readable reason when <paramref name="Success"/> is false; <c>null</c> on success.</param>
public record TransitionResult(
    bool Success,
    UnitStatus CurrentStatus,
    string? RejectionReason);