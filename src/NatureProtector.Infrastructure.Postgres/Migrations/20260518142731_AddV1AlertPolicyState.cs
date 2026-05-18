using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddV1AlertPolicyState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AlertCooldownUntil",
                schema: "projection",
                table: "area_operational_state",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingAlertCycles",
                schema: "projection",
                table: "area_operational_state",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingAlertState",
                schema: "projection",
                table: "area_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertCooldownUntil",
                schema: "projection",
                table: "area_operational_state");

            migrationBuilder.DropColumn(
                name: "PendingAlertCycles",
                schema: "projection",
                table: "area_operational_state");

            migrationBuilder.DropColumn(
                name: "PendingAlertState",
                schema: "projection",
                table: "area_operational_state");
        }
    }
}
