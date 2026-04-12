using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineInboxSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pipeline");

            migrationBuilder.CreateTable(
                name: "event_inbox",
                schema: "pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Producer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    EnvelopeJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "processing_attempts",
                schema: "pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboxEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Stage = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_attempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_processing_attempts_event_inbox_InboxEventId",
                        column: x => x.InboxEventId,
                        principalSchema: "pipeline",
                        principalTable: "event_inbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rejected_events",
                schema: "pipeline",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InboxEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RawBodyUtf8 = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rejected_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rejected_events_event_inbox_InboxEventId",
                        column: x => x.InboxEventId,
                        principalSchema: "pipeline",
                        principalTable: "event_inbox",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_event_inbox_EventId",
                schema: "pipeline",
                table: "event_inbox",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_inbox_Status_ReceivedAt",
                schema: "pipeline",
                table: "event_inbox",
                columns: new[] { "Status", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_attempts_InboxEventId_AttemptNumber",
                schema: "pipeline",
                table: "processing_attempts",
                columns: new[] { "InboxEventId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rejected_events_InboxEventId",
                schema: "pipeline",
                table: "rejected_events",
                column: "InboxEventId");

            migrationBuilder.CreateIndex(
                name: "IX_rejected_events_RejectedAt",
                schema: "pipeline",
                table: "rejected_events",
                column: "RejectedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processing_attempts",
                schema: "pipeline");

            migrationBuilder.DropTable(
                name: "rejected_events",
                schema: "pipeline");

            migrationBuilder.DropTable(
                name: "event_inbox",
                schema: "pipeline");
        }
    }
}
