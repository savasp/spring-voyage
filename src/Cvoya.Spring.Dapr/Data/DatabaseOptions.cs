// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Data;

/// <summary>
/// Configuration for the <see cref="SpringDbContext"/> startup behavior.
/// Bound from the <c>Database</c> configuration section.
/// </summary>
public class DatabaseOptions
{
    /// <summary>Configuration section name under which these options are bound.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Whether the host should automatically apply pending EF Core
    /// migrations on startup. Defaults to <c>true</c> so that fresh
    /// deployments and development databases come up with an
    /// up-to-date schema without operator intervention.
    /// </summary>
    /// <remarks>
    /// Operators running migrations out-of-band (CI/CD pipelines,
    /// scripted SQL dumps, or the <c>dotnet ef database update</c>
    /// command) should set this to <c>false</c> so the host does not
    /// race with the external migration run or require the DB user
    /// to have DDL privileges.
    /// </remarks>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// Whether the host should, on startup, scan every registered agent
    /// actor for a legacy <c>Agent:ParentUnit</c> cached pointer and
    /// upsert the corresponding row in the new unit-membership table
    /// (see #160 / C2b-1). Defaults to <c>true</c> so first-time
    /// deployments pick up the M:N model without manual intervention;
    /// operators who have already run the backfill, or who are on a
    /// fresh install, can flip it to <c>false</c> to skip the scan.
    /// </summary>
    /// <remarks>
    /// Idempotent — the backfill uses <c>UpsertAsync</c> and skips
    /// agents that already have a membership row, so repeat runs are
    /// harmless. The backfill runs once per host start and blocks
    /// startup until it completes.
    /// </remarks>
    public bool BackfillMemberships { get; set; } = true;
}