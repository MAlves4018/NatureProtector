using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRuntimeLifecycleOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "request_id", schema: "control", table: "runtime_orchestrator_executions", type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()");
            migrationBuilder.AddColumn<Guid>(name: "simulation_run_id", schema: "control", table: "runtime_orchestrator_executions", type: "uuid", nullable: true);
            migrationBuilder.AddColumn<string>(name: "requested_state", schema: "control", table: "runtime_orchestrator_executions", type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Requested");
            migrationBuilder.AddColumn<string>(name: "provider_state", schema: "control", table: "runtime_orchestrator_executions", type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Requested");
            migrationBuilder.AddColumn<string>(name: "run_state", schema: "control", table: "runtime_orchestrator_executions", type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending");
            migrationBuilder.AddColumn<string>(name: "processing_state", schema: "control", table: "runtime_orchestrator_executions", type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending");
            migrationBuilder.AddColumn<string>(name: "terminal_outcome", schema: "control", table: "runtime_orchestrator_executions", type: "character varying(50)", maxLength: 50, nullable: true);
            migrationBuilder.AddColumn<bool>(name: "is_operational", schema: "control", table: "runtime_orchestrator_executions", type: "boolean", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "deadline_at", schema: "control", table: "runtime_orchestrator_executions", type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP + interval '1 hour'");
            migrationBuilder.AddColumn<DateTimeOffset>(name: "producer_completed_at", schema: "control", table: "runtime_orchestrator_executions", type: "timestamp with time zone", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "system_completed_at", schema: "control", table: "runtime_orchestrator_executions", type: "timestamp with time zone", nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_runtime_orchestrator_executions_is_operational",
                schema: "control",
                table: "runtime_orchestrator_executions",
                column: "is_operational",
                unique: true,
                filter: "is_operational = TRUE AND terminal_outcome IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_runtime_orchestrator_executions_log_correlation",
                schema: "control",
                table: "runtime_orchestrator_executions",
                column: "log_correlation",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_runtime_orchestrator_executions_request_id",
                schema: "control",
                table: "runtime_orchestrator_executions",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_runtime_orchestrator_executions_simulation_run_id",
                schema: "control",
                table: "runtime_orchestrator_executions",
                column: "simulation_run_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_runtime_orchestrator_executions_simulation_runs_simulation_run_id",
                schema: "control",
                table: "runtime_orchestrator_executions",
                column: "simulation_run_id",
                principalSchema: "control",
                principalTable: "simulation_runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_runtime_orchestrator_executions_simulation_runs_simulation_run_id", schema: "control", table: "runtime_orchestrator_executions");
            migrationBuilder.DropIndex(name: "IX_runtime_orchestrator_executions_is_operational", schema: "control", table: "runtime_orchestrator_executions");
            migrationBuilder.DropIndex(name: "IX_runtime_orchestrator_executions_log_correlation", schema: "control", table: "runtime_orchestrator_executions");
            migrationBuilder.DropIndex(name: "IX_runtime_orchestrator_executions_request_id", schema: "control", table: "runtime_orchestrator_executions");
            migrationBuilder.DropIndex(name: "IX_runtime_orchestrator_executions_simulation_run_id", schema: "control", table: "runtime_orchestrator_executions");
            foreach (var column in new[] { "request_id", "simulation_run_id", "requested_state", "provider_state", "run_state", "processing_state", "terminal_outcome", "is_operational", "deadline_at", "producer_completed_at", "system_completed_at" })
                migrationBuilder.DropColumn(name: column, schema: "control", table: "runtime_orchestrator_executions");
        }
    }
}
