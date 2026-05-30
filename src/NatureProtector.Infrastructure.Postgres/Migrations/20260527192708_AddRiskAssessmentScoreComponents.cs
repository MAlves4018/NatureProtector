using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskAssessmentScoreComponents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AdjustedScore",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<double>(
                name: "BaseRisk",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<string>(
                name: "CalculationStatus",
                schema: "projection",
                table: "risk_assessment_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "CandidateFallback");

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceFactor",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "DominantDriver",
                schema: "projection",
                table: "risk_assessment_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Mixed");

            migrationBuilder.AddColumn<double>(
                name: "DroughtComponent",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FuelComponent",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "GeomorphologyComponent",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HazardComponent",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "IntegrityFactor",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "Limitations",
                schema: "projection",
                table: "risk_assessment_log",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MeteorologyComponent",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "ParameterSetVersion",
                schema: "projection",
                table: "risk_assessment_log",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "Score100",
                schema: "projection",
                table: "risk_assessment_log",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "TerritoryComponent",
                schema: "projection",
                table: "risk_assessment_log",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustedScore",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "BaseRisk",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "CalculationStatus",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "ConfidenceFactor",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "DominantDriver",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "DroughtComponent",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "FuelComponent",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "GeomorphologyComponent",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "HazardComponent",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "IntegrityFactor",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "Limitations",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "MeteorologyComponent",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "ParameterSetVersion",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "Score100",
                schema: "projection",
                table: "risk_assessment_log");

            migrationBuilder.DropColumn(
                name: "TerritoryComponent",
                schema: "projection",
                table: "risk_assessment_log");
        }
    }
}
