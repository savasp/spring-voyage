// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cvoya.Spring.Dapr.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitMembership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "unit_memberships",
                schema: "spring",
                columns: table => new
                {
                    unit_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    agent_address = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    model = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    specialty = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    execution_mode = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_memberships", x => new { x.unit_id, x.agent_address });
                });

            migrationBuilder.CreateIndex(
                name: "ix_unit_memberships_agent_address",
                schema: "spring",
                table: "unit_memberships",
                column: "agent_address");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unit_memberships",
                schema: "spring");
        }
    }
}