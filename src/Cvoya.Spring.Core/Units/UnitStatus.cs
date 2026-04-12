// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Units;

/// <summary>
/// Represents the lifecycle status of a unit.
/// </summary>
public enum UnitStatus
{
    /// <summary>The unit has been created but its configuration has not yet been finalized.</summary>
    Draft,

    /// <summary>The unit is configured and idle; its runtime container is not running.</summary>
    Stopped,

    /// <summary>The unit is transitioning from stopped to running; the container is being launched.</summary>
    Starting,

    /// <summary>The unit's runtime container is running and the unit is accepting work.</summary>
    Running,

    /// <summary>The unit is transitioning from running to stopped; the container is being torn down.</summary>
    Stopping,

    /// <summary>The unit encountered an unrecoverable error during a lifecycle transition and requires operator attention.</summary>
    Error,
}