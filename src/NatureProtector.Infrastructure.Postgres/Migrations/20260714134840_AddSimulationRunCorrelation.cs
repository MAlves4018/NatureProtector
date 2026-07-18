using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationRunCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "orchestrator_correlation_id",
                schema: "control",
                table: "simulation_runs",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SimulationRunId",
                schema: "pipeline",
                table: "event_inbox",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_simulation_runs_orchestrator_correlation_id",
                schema: "control",
                table: "simulation_runs",
                column: "orchestrator_correlation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_inbox_SimulationRunId_Status",
                schema: "pipeline",
                table: "event_inbox",
                columns: new[] { "SimulationRunId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_simulation_runs_orchestrator_correlation_id",
                schema: "control",
                table: "simulation_runs");

            migrationBuilder.DropIndex(
                name: "IX_event_inbox_SimulationRunId_Status",
                schema: "pipeline",
                table: "event_inbox");

            migrationBuilder.DropColumn(
                name: "orchestrator_correlation_id",
                schema: "control",
                table: "simulation_runs");

            migrationBuilder.DropColumn(
                name: "SimulationRunId",
                schema: "pipeline",
                table: "event_inbox");
        }
    }
}
