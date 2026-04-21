// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cvoya.Spring.Dapr.Data.Migrations
{
    /// <summary>
    /// Adds the <c>is_primary</c> column to <c>unit_memberships</c>. Part
    /// of the v2 design-system rollout (SVR-membership, umbrella #815):
    /// exactly one membership per agent is the canonical parent surface,
    /// which the Explorer uses to pick which tree position owns the
    /// agent's detail view when the agent has multi-parent membership.
    /// <para>
    /// Column defaults to <c>false</c>; the backfill below seeds
    /// <c>is_primary = true</c> on the oldest membership for each
    /// <c>(tenant_id, agent_address)</c> pair, breaking ties
    /// lexicographically on <c>unit_id</c>. Post-migration the repository
    /// auto-maintains the invariant on insert and delete.
    /// </para>
    /// </summary>
    public partial class AddUnitMembershipIsPrimary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_primary",
                schema: "spring",
                table: "unit_memberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill the primary flag on the oldest membership per
            // (tenant_id, agent_address). Tiebreaker: lexicographic unit_id.
            // `DISTINCT ON` is a PostgreSQL-specific construct — the Npgsql
            // provider is the only one this project targets (see
            // Cvoya.Spring.Dapr.csproj).
            migrationBuilder.Sql(@"
                UPDATE spring.unit_memberships AS um
                SET is_primary = TRUE
                WHERE (um.tenant_id, um.unit_id, um.agent_address) IN (
                    SELECT DISTINCT ON (tenant_id, agent_address)
                        tenant_id, unit_id, agent_address
                    FROM spring.unit_memberships
                    ORDER BY tenant_id, agent_address, created_at ASC, unit_id ASC
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_primary",
                schema: "spring",
                table: "unit_memberships");
        }
    }
}