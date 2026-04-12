using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NatureProtector.Infrastructure.Postgres.Persistence;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    [DbContext(typeof(NatureProtectorControlDbContext))]
    [Migration("20260407234000_AddPipelineRetriesAndQuarantine")]
    /// <inheritdoc />
    public partial class AddPipelineRetriesAndQuarantine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptNotBefore",
                schema: "pipeline",
                table: "event_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuarantinedAt",
                schema: "pipeline",
                table: "event_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "quarantined_events",
                schema: "pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboxEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalAttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    QuarantineCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    QuarantineReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    QuarantinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quarantined_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quarantined_events_event_inbox_InboxEventId",
                        column: x => x.InboxEventId,
                        principalSchema: "pipeline",
                        principalTable: "event_inbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_inbox_Status_NextAttemptNotBefore",
                schema: "pipeline",
                table: "event_inbox",
                columns: new[] { "Status", "NextAttemptNotBefore" });

            migrationBuilder.CreateIndex(
                name: "IX_quarantined_events_EventId",
                schema: "pipeline",
                table: "quarantined_events",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_quarantined_events_InboxEventId",
                schema: "pipeline",
                table: "quarantined_events",
                column: "InboxEventId");

            migrationBuilder.CreateIndex(
                name: "IX_quarantined_events_QuarantinedAt",
                schema: "pipeline",
                table: "quarantined_events",
                column: "QuarantinedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quarantined_events",
                schema: "pipeline");

            migrationBuilder.DropIndex(
                name: "IX_event_inbox_Status_NextAttemptNotBefore",
                schema: "pipeline",
                table: "event_inbox");

            migrationBuilder.DropColumn(
                name: "NextAttemptNotBefore",
                schema: "pipeline",
                table: "event_inbox");

            migrationBuilder.DropColumn(
                name: "QuarantinedAt",
                schema: "pipeline",
                table: "event_inbox");
        }
    }
}
