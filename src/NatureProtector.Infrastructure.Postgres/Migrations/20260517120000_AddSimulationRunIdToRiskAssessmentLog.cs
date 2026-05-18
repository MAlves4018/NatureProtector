using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationRunIdToRiskAssessmentLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SimulationRunId",
                schema: "projection",
                table: "risk_assessment_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_SimulationRunId",
                schema: "projection",
                table: "risk_assessment_log",
                column: "SimulationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_AreaId_SimulationRunId_Timestamp",
                schema: "projection",
                table: "risk_assessment_log",
                columns: new[] { "AreaId", "SimulationRunId", "Timestamp" });

            migrationBuilder.AddForeignKey(
                name: "FK_risk_assessment_log_simulation_runs_SimulationRunId",
                schema: "projection",
                table: "risk_assessment_log",
                column: "SimulationRunId",
                principalSchema: "control",
                principalTable: "simulation_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_risk_assessment_log_simulation_runs_SimulationRunId",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropIndex(
                name: "IX_risk_assessment_log_SimulationRunId",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropIndex(
                name: "IX_risk_assessment_log_AreaId_SimulationRunId_Timestamp",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "SimulationRunId",
                schema: "projection",
                table: "risk_assessment_log");
        }
    }
}
