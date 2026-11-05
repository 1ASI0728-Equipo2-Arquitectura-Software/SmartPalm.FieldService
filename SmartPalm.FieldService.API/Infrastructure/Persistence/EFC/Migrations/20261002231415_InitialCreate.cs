using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SmartPalm.FieldService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "field");

            migrationBuilder.CreateTable(
                name: "agronomist_affiliation_projections",
                schema: "field",
                columns: table => new
                {
                    affiliation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agronomist_id = table.Column<long>(type: "bigint", nullable: false),
                    plantation_id = table.Column<long>(type: "bigint", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agronomist_affiliation_projections", x => x.affiliation_id);
                });

            migrationBuilder.CreateTable(
                name: "alert_projections",
                schema: "field",
                columns: table => new
                {
                    alert_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantation_id = table.Column<long>(type: "bigint", nullable: true),
                    sector_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alert_projections", x => x.alert_id);
                });

            migrationBuilder.CreateTable(
                name: "field_visits",
                schema: "field",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantation_id = table.Column<long>(type: "bigint", nullable: false),
                    agronomist_id = table.Column<long>(type: "bigint", nullable: false),
                    scheduled_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    objectives = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_visits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "field",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "field",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_on_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plantation_projections",
                schema: "field",
                columns: table => new
                {
                    plantation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    palm_grower_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_plantation_projections", x => x.plantation_id);
                });

            migrationBuilder.CreateTable(
                name: "recommendation_projections",
                schema: "field",
                columns: table => new
                {
                    recommendation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    agronomist_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<long>(type: "bigint", nullable: true),
                    content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    published = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recommendation_projections", x => x.recommendation_id);
                });

            migrationBuilder.CreateTable(
                name: "sector_projections",
                schema: "field",
                columns: table => new
                {
                    sector_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantation_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sector_projections", x => x.sector_id);
                });

            migrationBuilder.CreateTable(
                name: "user_projections",
                schema: "field",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    has_active_subscription = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_projections", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "field_inspections",
                schema: "field",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_reference = table.Column<Guid>(type: "uuid", nullable: false),
                    visit_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<long>(type: "bigint", nullable: false),
                    plantation_id = table.Column<long>(type: "bigint", nullable: false),
                    agronomist_id = table.Column<long>(type: "bigint", nullable: false),
                    inspected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sync_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_inspections", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_inspections_field_visits_visit_id",
                        column: x => x.visit_id,
                        principalSchema: "field",
                        principalTable: "field_visits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agronomic_interventions",
                schema: "field",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    plantation_id = table.Column<long>(type: "bigint", nullable: false),
                    sector_id = table.Column<long>(type: "bigint", nullable: true),
                    registered_by = table.Column<long>(type: "bigint", nullable: false),
                    performed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    origin_recommendation_id = table.Column<long>(type: "bigint", nullable: true),
                    origin_inspection_id = table.Column<long>(type: "bigint", nullable: true),
                    executed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    verification_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    verified_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agronomic_interventions", x => x.id);
                    table.ForeignKey(
                        name: "fk_agronomic_interventions_field_inspections_origin_inspection",
                        column: x => x.origin_inspection_id,
                        principalSchema: "field",
                        principalTable: "field_inspections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "field_observations",
                schema: "field",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    inspection_id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    photo_references_json = table.Column<string>(type: "jsonb", nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_field_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_field_observations_field_inspections_inspection_id",
                        column: x => x.inspection_id,
                        principalSchema: "field",
                        principalTable: "field_inspections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inspection_alert_links",
                schema: "field",
                columns: table => new
                {
                    inspection_id = table.Column<long>(type: "bigint", nullable: false),
                    alert_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inspection_alert_links", x => new { x.inspection_id, x.alert_id });
                    table.ForeignKey(
                        name: "fk_inspection_alert_links_field_inspections_inspection_id",
                        column: x => x.inspection_id,
                        principalSchema: "field",
                        principalTable: "field_inspections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agronomic_interventions_origin_inspection_id",
                schema: "field",
                table: "agronomic_interventions",
                column: "origin_inspection_id");

            migrationBuilder.CreateIndex(
                name: "ix_agronomic_interventions_origin_recommendation_id",
                schema: "field",
                table: "agronomic_interventions",
                column: "origin_recommendation_id");

            migrationBuilder.CreateIndex(
                name: "ix_agronomic_interventions_plantation_id_executed_at_utc",
                schema: "field",
                table: "agronomic_interventions",
                columns: new[] { "plantation_id", "executed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_agronomist_affiliation_projections_agronomist_id_plantation",
                schema: "field",
                table: "agronomist_affiliation_projections",
                columns: new[] { "agronomist_id", "plantation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alert_projections_sector_id",
                schema: "field",
                table: "alert_projections",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_inspections_client_reference",
                schema: "field",
                table: "field_inspections",
                column: "client_reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_field_inspections_plantation_id_inspected_at_utc",
                schema: "field",
                table: "field_inspections",
                columns: new[] { "plantation_id", "inspected_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_field_inspections_visit_id",
                schema: "field",
                table: "field_inspections",
                column: "visit_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_observations_inspection_id",
                schema: "field",
                table: "field_observations",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "ix_field_visits_agronomist_id_scheduled_date",
                schema: "field",
                table: "field_visits",
                columns: new[] { "agronomist_id", "scheduled_date" });

            migrationBuilder.CreateIndex(
                name: "ix_field_visits_plantation_id_status",
                schema: "field",
                table: "field_visits",
                columns: new[] { "plantation_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on_utc_occurred_on_utc",
                schema: "field",
                table: "outbox_messages",
                columns: new[] { "processed_on_utc", "occurred_on_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_recommendation_projections_sector_id",
                schema: "field",
                table: "recommendation_projections",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_sector_projections_plantation_id",
                schema: "field",
                table: "sector_projections",
                column: "plantation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agronomic_interventions",
                schema: "field");

            migrationBuilder.DropTable(
                name: "agronomist_affiliation_projections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "alert_projections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "field_observations",
                schema: "field");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "field");

            migrationBuilder.DropTable(
                name: "inspection_alert_links",
                schema: "field");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "field");

            migrationBuilder.DropTable(
                name: "plantation_projections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "recommendation_projections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "sector_projections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "user_projections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "field_inspections",
                schema: "field");

            migrationBuilder.DropTable(
                name: "field_visits",
                schema: "field");
        }
    }
}
