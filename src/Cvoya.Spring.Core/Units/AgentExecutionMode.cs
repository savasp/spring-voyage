// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Units;

/// <summary>
/// How an agent slotted into a unit participates in message dispatch.
/// </summary>
public enum AgentExecutionMode
{
    /// <summary>
    /// The unit's orchestration strategy may route domain messages to this
    /// agent without explicit addressing. Default for newly assigned agents.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// The agent receives messages only when an inbound message addresses
    /// it explicitly. Useful for specialists that should not be woken by
    /// generic unit traffic.
    /// </summary>
    OnDemand = 1,
}