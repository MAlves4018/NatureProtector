using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCellKbdiStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KbdiCalculationStatus",
                schema: "projection",
                table: "daily_cell_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Missing");

            migrationBuilder.AddColumn<string>(
                name: "KbdiLimitations",
                schema: "projection",
                table: "daily_cell_state",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NormalizedKeetchByramDroughtIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PreviousKeetchByramDroughtIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KbdiCalculationStatus",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "KbdiLimitations",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "NormalizedKeetchByramDroughtIndex",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "PreviousKeetchByramDroughtIndex",
                schema: "projection",
                table: "daily_cell_state");
        }
    }
}
