using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NatureProtector.Infrastructure.Postgres.Persistence;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    [DbContext(typeof(NatureProtectorControlDbContext))]
    [Migration("20260408001500_AddProjectionSchema")]
    /// <inheritdoc />
    public partial class AddProjectionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projection");

            migrationBuilder.CreateTable(
                name: "area_operational_state",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AggregateRiskScore = table.Column<double>(type: "double precision", nullable: false),
                    AggregateRiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssessmentCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_operational_state", x => x.Id);
                    table.ForeignKey(
                        name: "FK_area_operational_state_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_area_operational_state_configuration_versions_ConfigurationVersionId",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_area_operational_state_simulation_runs_SimulationRunId",
                        column: x => x.SimulationRunId,
                        principalSchema: "control",
                        principalTable: "simulation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "alert_state",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaOperationalStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlertCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_state", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_state_area_operational_state_AreaOperationalStateId",
                        column: x => x.AreaOperationalStateId,
                        principalSchema: "projection",
                        principalTable: "area_operational_state",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_state_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_state_configuration_versions_ConfigurationVersionId",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_state_AreaId_AlertCode_Status",
                schema: "projection",
                table: "alert_state",
                columns: new[] { "AreaId", "AlertCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_alert_state_AreaOperationalStateId",
                schema: "projection",
                table: "alert_state",
                column: "AreaOperationalStateId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_state_ConfigurationVersionId",
                schema: "projection",
                table: "alert_state",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_state_UpdatedAt",
                schema: "projection",
                table: "alert_state",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_area_operational_state_AreaId",
                schema: "projection",
                table: "area_operational_state",
                column: "AreaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_area_operational_state_ConfigurationVersionId",
                schema: "projection",
                table: "area_operational_state",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_area_operational_state_SimulationRunId",
                schema: "projection",
                table: "area_operational_state",
                column: "SimulationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_area_operational_state_SnapshotTimestamp",
                schema: "projection",
                table: "area_operational_state",
                column: "SnapshotTimestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_state",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "area_operational_state",
                schema: "projection");
        }
    }
}
