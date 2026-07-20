using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class FixCycleAggregationSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<double>(
                name: "AggregateRiskScore",
                schema: "projection",
                table: "cell_cycle_snapshot",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<string>(
                name: "AggregationReason",
                schema: "projection",
                table: "cell_cycle_snapshot",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AggregationStatus",
                schema: "projection",
                table: "cell_cycle_snapshot",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Available");

            migrationBuilder.AlterColumn<double>(
                name: "AggregateRiskScore",
                schema: "projection",
                table: "area_cycle_snapshot",
                type: "double precision",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.AddColumn<string>(
                name: "AggregationReason",
                schema: "projection",
                table: "area_cycle_snapshot",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AggregationStatus",
                schema: "projection",
                table: "area_cycle_snapshot",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Available");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AggregationReason",
                schema: "projection",
                table: "cell_cycle_snapshot");

            migrationBuilder.DropColumn(
                name: "AggregationStatus",
                schema: "projection",
                table: "cell_cycle_snapshot");

            migrationBuilder.DropColumn(
                name: "AggregationReason",
                schema: "projection",
                table: "area_cycle_snapshot");

            migrationBuilder.DropColumn(
                name: "AggregationStatus",
                schema: "projection",
                table: "area_cycle_snapshot");

            migrationBuilder.AlterColumn<double>(
                name: "AggregateRiskScore",
                schema: "projection",
                table: "cell_cycle_snapshot",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "AggregateRiskScore",
                schema: "projection",
                table: "area_cycle_snapshot",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldNullable: true);
        }
    }
}
