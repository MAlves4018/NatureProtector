using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddTemporalCycleProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CycleIndex",
                schema: "projection",
                table: "area_operational_state",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "area_cycle_snapshot",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleIndex = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CellCount = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCount = table.Column<int>(type: "integer", nullable: false),
                    ObservedCount = table.Column<int>(type: "integer", nullable: false),
                    MissingCount = table.Column<int>(type: "integer", nullable: false),
                    BlockedCount = table.Column<int>(type: "integer", nullable: false),
                    EligibleCount = table.Column<int>(type: "integer", nullable: false),
                    AggregateRiskScore = table.Column<double>(type: "double precision", nullable: false),
                    AggregateRiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SnapshotTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AlertEvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AlertOutcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsOperational = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_cycle_snapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cell_cycle_snapshot",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleIndex = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridCellId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedCount = table.Column<int>(type: "integer", nullable: false),
                    ObservedCount = table.Column<int>(type: "integer", nullable: false),
                    MissingCount = table.Column<int>(type: "integer", nullable: false),
                    BlockedCount = table.Column<int>(type: "integer", nullable: false),
                    EligibleCount = table.Column<int>(type: "integer", nullable: false),
                    AggregateRiskScore = table.Column<double>(type: "double precision", nullable: false),
                    AggregateRiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SnapshotTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cell_cycle_snapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cycle_observation",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleIndex = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    SensorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridCellId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    MetricOrigin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RiskScore = table.Column<double>(type: "double precision", nullable: true),
                    RiskLevel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EventTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_observation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "cycle_settlement",
                schema: "projection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleIndex = table.Column<int>(type: "integer", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedSensorIdsJson = table.Column<string>(type: "text", nullable: false),
                    ObservedSensorIdsJson = table.Column<string>(type: "text", nullable: false),
                    MissingSensorIdsJson = table.Column<string>(type: "text", nullable: false),
                    BlockedSensorIdsJson = table.Column<string>(type: "text", nullable: false),
                    EligibleSensorIdsJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsOperational = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinalizationReason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cycle_settlement", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_area_cycle_snapshot_AreaId_CycleIndex",
                schema: "projection",
                table: "area_cycle_snapshot",
                columns: new[] { "AreaId", "CycleIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_area_cycle_snapshot_SimulationRunId_CycleIndex_AreaId",
                schema: "projection",
                table: "area_cycle_snapshot",
                columns: new[] { "SimulationRunId", "CycleIndex", "AreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cell_cycle_snapshot_SimulationRunId_CycleIndex_GridCellId",
                schema: "projection",
                table: "cell_cycle_snapshot",
                columns: new[] { "SimulationRunId", "CycleIndex", "GridCellId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_observation_EventId",
                schema: "projection",
                table: "cycle_observation",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_observation_SimulationRunId_CycleIndex_SensorId",
                schema: "projection",
                table: "cycle_observation",
                columns: new[] { "SimulationRunId", "CycleIndex", "SensorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cycle_settlement_AreaId_Status",
                schema: "projection",
                table: "cycle_settlement",
                columns: new[] { "AreaId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_cycle_settlement_SimulationRunId_CycleIndex",
                schema: "projection",
                table: "cycle_settlement",
                columns: new[] { "SimulationRunId", "CycleIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "area_cycle_snapshot",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "cell_cycle_snapshot",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "cycle_observation",
                schema: "projection");

            migrationBuilder.DropTable(
                name: "cycle_settlement",
                schema: "projection");

            migrationBuilder.DropColumn(
                name: "CycleIndex",
                schema: "projection",
                table: "area_operational_state");
        }
    }
}
