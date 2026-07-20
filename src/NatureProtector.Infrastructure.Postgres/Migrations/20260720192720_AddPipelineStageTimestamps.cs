using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineStageTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AlertedAt",
                schema: "projection",
                table: "risk_assessment_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssessedAt",
                schema: "projection",
                table: "risk_assessment_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProjectedAt",
                schema: "projection",
                table: "risk_assessment_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PersistedAt",
                schema: "pipeline",
                table: "event_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PersistedAt",
                schema: "projection",
                table: "accepted_reading_log",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """UPDATE projection.risk_assessment_log SET "AssessedAt" = "CreatedAt" WHERE "AssessedAt" IS NULL;""");

            migrationBuilder.Sql(
                """UPDATE pipeline.event_inbox SET "PersistedAt" = "ReceivedAt" WHERE "PersistedAt" IS NULL;""");

            migrationBuilder.Sql(
                """UPDATE projection.accepted_reading_log SET "PersistedAt" = "CreatedAt" WHERE "PersistedAt" IS NULL;""");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "AssessedAt",
                schema: "projection",
                table: "risk_assessment_log",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PersistedAt",
                schema: "pipeline",
                table: "event_inbox",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "PersistedAt",
                schema: "projection",
                table: "accepted_reading_log",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_SimulationRunId_AssessedAt",
                schema: "projection",
                table: "risk_assessment_log",
                columns: new[] { "SimulationRunId", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_risk_assessment_log_SimulationRunId_ProjectedAt",
                schema: "projection",
                table: "risk_assessment_log",
                columns: new[] { "SimulationRunId", "ProjectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_event_inbox_SimulationRunId_PersistedAt",
                schema: "pipeline",
                table: "event_inbox",
                columns: new[] { "SimulationRunId", "PersistedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_accepted_reading_log_PersistedAt",
                schema: "projection",
                table: "accepted_reading_log",
                column: "PersistedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_risk_assessment_log_SimulationRunId_AssessedAt",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropIndex(
                name: "IX_risk_assessment_log_SimulationRunId_ProjectedAt",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropIndex(
                name: "IX_event_inbox_SimulationRunId_PersistedAt",
                schema: "pipeline",
                table: "event_inbox");

            migrationBuilder.DropIndex(
                name: "IX_accepted_reading_log_PersistedAt",
                schema: "projection",
                table: "accepted_reading_log");

            migrationBuilder.DropColumn(
                name: "AlertedAt",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "AssessedAt",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "ProjectedAt",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "PersistedAt",
                schema: "pipeline",
                table: "event_inbox");

            migrationBuilder.DropColumn(
                name: "PersistedAt",
                schema: "projection",
                table: "accepted_reading_log");
        }
    }
}
