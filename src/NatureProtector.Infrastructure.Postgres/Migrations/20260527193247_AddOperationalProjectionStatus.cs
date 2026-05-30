using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalProjectionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CarryForwardStatus",
                schema: "projection",
                table: "cell_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Current");

            migrationBuilder.AddColumn<string>(
                name: "CoverageStatus",
                schema: "projection",
                table: "cell_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Complete");

            migrationBuilder.AddColumn<string>(
                name: "FreshnessStatus",
                schema: "projection",
                table: "cell_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Fresh");

            migrationBuilder.AddColumn<string>(
                name: "CarryForwardStatus",
                schema: "projection",
                table: "area_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Current");

            migrationBuilder.AddColumn<string>(
                name: "CoverageStatus",
                schema: "projection",
                table: "area_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Complete");

            migrationBuilder.AddColumn<string>(
                name: "FreshnessStatus",
                schema: "projection",
                table: "area_operational_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Fresh");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarryForwardStatus",
                schema: "projection",
                table: "cell_operational_state");

            migrationBuilder.DropColumn(
                name: "CoverageStatus",
                schema: "projection",
                table: "cell_operational_state");

            migrationBuilder.DropColumn(
                name: "FreshnessStatus",
                schema: "projection",
                table: "cell_operational_state");

            migrationBuilder.DropColumn(
                name: "CarryForwardStatus",
                schema: "projection",
                table: "area_operational_state");

            migrationBuilder.DropColumn(
                name: "CoverageStatus",
                schema: "projection",
                table: "area_operational_state");

            migrationBuilder.DropColumn(
                name: "FreshnessStatus",
                schema: "projection",
                table: "area_operational_state");
        }
    }
}
