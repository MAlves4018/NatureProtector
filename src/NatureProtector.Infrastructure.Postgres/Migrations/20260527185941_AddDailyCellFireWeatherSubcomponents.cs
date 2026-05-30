using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCellFireWeatherSubcomponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BuildupIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DroughtCode",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DuffMoistureCode",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FineFuelMoistureCode",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FireWeatherCalculationStatus",
                schema: "projection",
                table: "daily_cell_state",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Missing");

            migrationBuilder.AddColumn<string>(
                name: "FireWeatherLimitations",
                schema: "projection",
                table: "daily_cell_state",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "InitialSpreadIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "NormalizedFireWeatherIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuildupIndex",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "DroughtCode",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "DuffMoistureCode",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "FineFuelMoistureCode",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "FireWeatherCalculationStatus",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "FireWeatherLimitations",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "InitialSpreadIndex",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "NormalizedFireWeatherIndex",
                schema: "projection",
                table: "daily_cell_state");
        }
    }
}
