using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCellStateProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_cell_state",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridCellId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LogicalDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DailyPrecipitationMillimeters = table.Column<double>(type: "double precision", nullable: true),
                    MaxTemperatureCelsius = table.Column<double>(type: "double precision", nullable: true),
                    LatestHumidityPercent = table.Column<double>(type: "double precision", nullable: true),
                    LatestWindSpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    AntecedentState = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DroughtContext = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CandidateParameterSetVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Provenance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastSourceEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_cell_state", x => x.Id);
                    table.ForeignKey(
                        name: "FK_daily_cell_state_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_cell_state_configuration_versions_ConfigurationVersio~",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_daily_cell_state_grid_cells_GridCellId",
                        column: x => x.GridCellId,
                        principalSchema: "control",
                        principalTable: "grid_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_daily_cell_state_sensor_nodes_SensorId",
                        column: x => x.SensorId,
                        principalSchema: "control",
                        principalTable: "sensor_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_daily_cell_state_simulation_runs_SimulationRunId",
                        column: x => x.SimulationRunId,
                        principalSchema: "control",
                        principalTable: "simulation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_daily_cell_state_AreaId_GridCellId_LogicalDate_SimulationRu~",
                schema: "projection",
                table: "daily_cell_state",
                columns: new[] { "AreaId", "GridCellId", "LogicalDate", "SimulationRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_daily_cell_state_ConfigurationVersionId",
                schema: "projection",
                table: "daily_cell_state",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_cell_state_GridCellId",
                schema: "projection",
                table: "daily_cell_state",
                column: "GridCellId");

            migrationBuilder.CreateIndex(
                name: "IX_daily_cell_state_SensorId_LogicalDate",
                schema: "projection",
                table: "daily_cell_state",
                columns: new[] { "SensorId", "LogicalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_cell_state_SimulationRunId",
                schema: "projection",
                table: "daily_cell_state",
                column: "SimulationRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_cell_state",
                schema: "projection");
        }
    }
}
