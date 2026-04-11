// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Costs;

/// <summary>
/// Provides aggregated cost queries per agent, unit, and tenant.
/// </summary>
public interface ICostQueryService
{
    /// <summary>
    /// Gets the aggregated cost summary for a specific agent within a time range.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="from">The start of the time range.</param>
    /// <param name="to">The end of the time range.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cost summary for the agent.</returns>
    Task<CostSummary> GetAgentCostAsync(string agentId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the aggregated cost summary for a specific unit within a time range.
    /// </summary>
    /// <param name="unitId">The unit identifier.</param>
    /// <param name="from">The start of the time range.</param>
    /// <param name="to">The end of the time range.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cost summary for the unit.</returns>
    Task<CostSummary> GetUnitCostAsync(string unitId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the aggregated cost summary for a specific tenant within a time range.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="from">The start of the time range.</param>
    /// <param name="to">The end of the time range.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cost summary for the tenant.</returns>
    Task<CostSummary> GetTenantCostAsync(string tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
}