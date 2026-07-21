using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPublishedAtToInboxEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                schema: "pipeline",
                table: "event_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_inbox_SimulationRunId_PublishedAt",
                schema: "pipeline",
                table: "event_inbox",
                columns: new[] { "SimulationRunId", "PublishedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_inbox_SimulationRunId_PublishedAt",
                schema: "pipeline",
                table: "event_inbox");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                schema: "pipeline",
                table: "event_inbox");
        }
    }
}
