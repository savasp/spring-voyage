// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

using System;
using System.Text.Json;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cvoya.Spring.Dapr.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "spring");

            migrationBuilder.CreateTable(
                name: "activity_events",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    details = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    cost = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agent_definitions",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    role = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    definition = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "api_tokens",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    scopes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "connector_definitions",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connector_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    config = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connector_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cost_records",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    agent_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    unit_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Work")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cost_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "secret_registry_entries",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    scope = table.Column<int>(type: "integer", nullable: false),
                    owner_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    store_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    origin = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_secret_registry_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unit_definitions",
                schema: "spring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    definition = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    members = table.Column<JsonElement>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unit_definitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_correlation_id",
                schema: "spring",
                table: "activity_events",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_activity_events_timestamp",
                schema: "spring",
                table: "activity_events",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_agent_definitions_agent_id",
                schema: "spring",
                table: "agent_definitions",
                column: "agent_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_api_tokens_token_hash",
                schema: "spring",
                table: "api_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connector_definitions_connector_id",
                schema: "spring",
                table: "connector_definitions",
                column: "connector_id",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_cost_records_agent_id",
                schema: "spring",
                table: "cost_records",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_cost_records_tenant_id",
                schema: "spring",
                table: "cost_records",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_cost_records_timestamp",
                schema: "spring",
                table: "cost_records",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_cost_records_unit_id",
                schema: "spring",
                table: "cost_records",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_secret_registry_tenant_scope_owner_name",
                schema: "spring",
                table: "secret_registry_entries",
                columns: new[] { "tenant_id", "scope", "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unit_definitions_unit_id",
                schema: "spring",
                table: "unit_definitions",
                column: "unit_id",
                unique: true,
                filter: "deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_events",
                schema: "spring");

            migrationBuilder.DropTable(
                name: "agent_definitions",
                schema: "spring");

            migrationBuilder.DropTable(
                name: "api_tokens",
                schema: "spring");

            migrationBuilder.DropTable(
                name: "connector_definitions",
                schema: "spring");

            migrationBuilder.DropTable(
                name: "cost_records",
                schema: "spring");

            migrationBuilder.DropTable(
                name: "secret_registry_entries",
                schema: "spring");

            migrationBuilder.DropTable(
                name: "unit_definitions",
                schema: "spring");
        }
    }
}