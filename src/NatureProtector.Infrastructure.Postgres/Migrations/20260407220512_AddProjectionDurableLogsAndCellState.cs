using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectionDurableLogsAndCellState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "projection");

            migrationBuilder.CreateTable(
                name: "accepted_reading_log",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MeasurementUnit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperationalState = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false),
                    EventTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Producer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    EnvelopeJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accepted_reading_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accepted_reading_log_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_accepted_reading_log_sensor_nodes_SensorId",
                        column: x => x.SensorId,
                        principalSchema: "control",
                        principalTable: "sensor_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "area_risk_snapshot_log",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AggregateRiskScore = table.Column<double>(type: "double precision", nullable: false),
                    AggregateRiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AssessmentCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_risk_snapshot_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_area_risk_snapshot_log_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_area_risk_snapshot_log_simulation_runs_SimulationRunId",
                        column: x => x.SimulationRunId,
                        principalSchema: "control",
                        principalTable: "simulation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "cell_operational_state",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridCellId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LatestAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    SnapshotTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RiskScore = table.Column<double>(type: "double precision", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cell_operational_state", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cell_operational_state_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cell_operational_state_grid_cells_GridCellId",
                        column: x => x.GridCellId,
                        principalSchema: "control",
                        principalTable: "grid_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_cell_operational_state_sensor_nodes_SensorId",
                        column: x => x.SensorId,
                        principalSchema: "control",
                        principalTable: "sensor_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessment_log",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridCellId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RiskScore = table.Column<double>(type: "double precision", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExplanationSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_assessment_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_risk_assessment_log_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_risk_assessment_log_grid_cells_GridCellId",
                        column: x => x.GridCellId,
                        principalSchema: "control",
                        principalTable: "grid_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_risk_assessment_log_sensor_nodes_SensorId",
                        column: x => x.SensorId,
                        principalSchema: "control",
                        principalTable: "sensor_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accepted_reading_log_AreaId_EventTime",
                schema: "projection",
                table: "accepted_reading_log",
                columns: new[] { "AreaId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_accepted_reading_log_EventId",
                schema: "projection",
                table: "accepted_reading_log",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accepted_reading_log_SensorId_EventTime",
                schema: "projection",
                table: "accepted_reading_log",
                columns: new[] { "SensorId", "EventTime" });

            migrationBuilder.CreateIndex(
                name: "IX_area_risk_snapshot_log_AreaId_SnapshotTimestamp",
                schema: "projection",
                table: "area_risk_snapshot_log",
                columns: new[] { "AreaId", "SnapshotTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_area_risk_snapshot_log_SimulationRunId",
                schema: "projection",
                table: "area_risk_snapshot_log",
                column: "SimulationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_cell_operational_state_AreaId_SnapshotTimestamp",
                schema: "projection",
                table: "cell_operational_state",
                columns: new[] { "AreaId", "SnapshotTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_cell_operational_state_GridCellId",
                schema: "projection",
                table: "cell_operational_state",
                column: "GridCellId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cell_operational_state_SensorId",
                schema: "projection",
                table: "cell_operational_state",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_AreaId_Timestamp",
                schema: "projection",
                table: "risk_assessment_log",
                columns: new[] { "AreaId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_GridCellId_Timestamp",
                schema: "projection",
                table: "risk_assessment_log",
                columns: new[] { "GridCellId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_SensorId",
                schema: "projection",
                table: "risk_assessment_log",
                column: "SensorId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_SourceEventId",
                schema: "projection",
                table: "risk_assessment_log",
                column: "SourceEventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accepted_reading_log",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "area_risk_snapshot_log",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "cell_operational_state",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "risk_assessment_log",
                schema: "projection");
        }
    }
}
