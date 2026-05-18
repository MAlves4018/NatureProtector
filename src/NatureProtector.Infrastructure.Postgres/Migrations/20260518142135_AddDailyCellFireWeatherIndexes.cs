using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCellFireWeatherIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FireIndexProvenance",
                schema: "projection",
                table: "daily_cell_state",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "FireWeatherIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "KeetchByramDroughtIndex",
                schema: "projection",
                table: "daily_cell_state",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FireIndexProvenance",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "FireWeatherIndex",
                schema: "projection",
                table: "daily_cell_state");

            migrationBuilder.DropColumn(
                name: "KeetchByramDroughtIndex",
                schema: "projection",
                table: "daily_cell_state");
        }
    }
}
