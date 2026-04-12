using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationRunsToControlSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "simulation_runs",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScenarioName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LogicalStartTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    NumberOfCycles = table.Column<int>(type: "integer", nullable: false),
                    ExecutionSeed = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_simulation_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_simulation_runs_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_simulation_runs_configuration_versions_ConfigurationVersion~",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_simulation_runs_scenario_definitions_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "control",
                        principalTable: "scenario_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_simulation_runs_AreaId_CreatedAt",
                schema: "control",
                table: "simulation_runs",
                columns: new[] { "AreaId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_simulation_runs_ConfigurationVersionId",
                schema: "control",
                table: "simulation_runs",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_simulation_runs_ScenarioId_CreatedAt",
                schema: "control",
                table: "simulation_runs",
                columns: new[] { "ScenarioId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "simulation_runs",
                schema: "control");
        }
    }
}
