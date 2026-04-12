using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialControlSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "control");

            migrationBuilder.CreateTable(
                name: "configuration_versions",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configuration_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "dataset_artifacts",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetCode = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DatasetType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AreaCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Checksum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dataset_artifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "areas",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    GeometryGeoJson = table.Column<string>(type: "text", nullable: true),
                    MetadataJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_areas_configuration_versions_ConfigurationVersionId",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rule_set_versions",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ParametersJson = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_set_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_rule_set_versions_configuration_versions_ConfigurationVersi~",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_networks",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_networks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_networks_configuration_versions_ConfigurationVersion~",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_profiles",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SensorFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AccuracyProfileJson = table.Column<string>(type: "text", nullable: true),
                    NoiseProfileJson = table.Column<string>(type: "text", nullable: true),
                    FaultProfileJson = table.Column<string>(type: "text", nullable: true),
                    PublicationPolicyJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_profiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_profiles_configuration_versions_ConfigurationVersion~",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "area_contexts",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    VegetationType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VegetationDensity = table.Column<double>(type: "double precision", nullable: false),
                    PopulationExposure = table.Column<double>(type: "double precision", nullable: false),
                    CriticalInfrastructureExposure = table.Column<double>(type: "double precision", nullable: false),
                    Seasonality = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_area_contexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_area_contexts_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grid_cells",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CellCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CentroidLatitude = table.Column<double>(type: "double precision", nullable: false),
                    CentroidLongitude = table.Column<double>(type: "double precision", nullable: false),
                    PolygonGeoJson = table.Column<string>(type: "text", nullable: true),
                    AltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                    SlopeDegrees = table.Column<double>(type: "double precision", nullable: true),
                    AspectDegrees = table.Column<double>(type: "double precision", nullable: true),
                    LandCoverClass = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DominantForestType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DominantFuelModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TreeCoverDensity = table.Column<double>(type: "double precision", nullable: true),
                    StructuralHazard = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ConjuncturalHazard = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grid_cells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grid_cells_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_grid_cells_configuration_versions_ConfigurationVersionId",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scenario_definitions",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BaseScenarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ScenarioKind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ParametersJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scenario_definitions_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scenario_definitions_configuration_versions_ConfigurationVe~",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scenario_definitions_scenario_definitions_BaseScenarioId",
                        column: x => x.BaseScenarioId,
                        principalSchema: "control",
                        principalTable: "scenario_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sensor_nodes",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: false),
                    GridCellId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfigurationVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NetworkId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AltitudeMeters = table.Column<double>(type: "double precision", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    InstallationProfile = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sensor_nodes_areas_AreaId",
                        column: x => x.AreaId,
                        principalSchema: "control",
                        principalTable: "areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sensor_nodes_configuration_versions_ConfigurationVersionId",
                        column: x => x.ConfigurationVersionId,
                        principalSchema: "control",
                        principalTable: "configuration_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_nodes_grid_cells_GridCellId",
                        column: x => x.GridCellId,
                        principalSchema: "control",
                        principalTable: "grid_cells",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sensor_nodes_sensor_networks_NetworkId",
                        column: x => x.NetworkId,
                        principalSchema: "control",
                        principalTable: "sensor_networks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_sensor_nodes_sensor_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "control",
                        principalTable: "sensor_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "scenario_dataset_bindings",
                schema: "control",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScenarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatasetArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    BindingRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenario_dataset_bindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scenario_dataset_bindings_dataset_artifacts_DatasetArtifact~",
                        column: x => x.DatasetArtifactId,
                        principalSchema: "control",
                        principalTable: "dataset_artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_scenario_dataset_bindings_scenario_definitions_ScenarioId",
                        column: x => x.ScenarioId,
                        principalSchema: "control",
                        principalTable: "scenario_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_area_contexts_AreaId",
                schema: "control",
                table: "area_contexts",
                column: "AreaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_areas_ConfigurationVersionId_Code",
                schema: "control",
                table: "areas",
                columns: new[] { "ConfigurationVersionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_configuration_versions_VersionNumber",
                schema: "control",
                table: "configuration_versions",
                column: "VersionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dataset_artifacts_DatasetCode_AreaCode_Version",
                schema: "control",
                table: "dataset_artifacts",
                columns: new[] { "DatasetCode", "AreaCode", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grid_cells_AreaId_CellCode",
                schema: "control",
                table: "grid_cells",
                columns: new[] { "AreaId", "CellCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_grid_cells_ConfigurationVersionId",
                schema: "control",
                table: "grid_cells",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_set_versions_ConfigurationVersionId_Name_Version",
                schema: "control",
                table: "rule_set_versions",
                columns: new[] { "ConfigurationVersionId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scenario_dataset_bindings_DatasetArtifactId",
                schema: "control",
                table: "scenario_dataset_bindings",
                column: "DatasetArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_dataset_bindings_ScenarioId_DatasetArtifactId_Bind~",
                schema: "control",
                table: "scenario_dataset_bindings",
                columns: new[] { "ScenarioId", "DatasetArtifactId", "BindingRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scenario_definitions_AreaId_Code",
                schema: "control",
                table: "scenario_definitions",
                columns: new[] { "AreaId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scenario_definitions_BaseScenarioId",
                schema: "control",
                table: "scenario_definitions",
                column: "BaseScenarioId");

            migrationBuilder.CreateIndex(
                name: "IX_scenario_definitions_ConfigurationVersionId",
                schema: "control",
                table: "scenario_definitions",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_networks_ConfigurationVersionId_Name",
                schema: "control",
                table: "sensor_networks",
                columns: new[] { "ConfigurationVersionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_AreaId_GridCellId_Name",
                schema: "control",
                table: "sensor_nodes",
                columns: new[] { "AreaId", "GridCellId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_ConfigurationVersionId",
                schema: "control",
                table: "sensor_nodes",
                column: "ConfigurationVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_GridCellId",
                schema: "control",
                table: "sensor_nodes",
                column: "GridCellId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_NetworkId",
                schema: "control",
                table: "sensor_nodes",
                column: "NetworkId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_nodes_ProfileId",
                schema: "control",
                table: "sensor_nodes",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_sensor_profiles_ConfigurationVersionId_Name",
                schema: "control",
                table: "sensor_profiles",
                columns: new[] { "ConfigurationVersionId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "area_contexts",
                schema: "control");

            migrationBuilder.DropTable(
                name: "rule_set_versions",
                schema: "control");

            migrationBuilder.DropTable(
                name: "scenario_dataset_bindings",
                schema: "control");

            migrationBuilder.DropTable(
                name: "sensor_nodes",
                schema: "control");

            migrationBuilder.DropTable(
                name: "dataset_artifacts",
                schema: "control");

            migrationBuilder.DropTable(
                name: "scenario_definitions",
                schema: "control");

            migrationBuilder.DropTable(
                name: "grid_cells",
                schema: "control");

            migrationBuilder.DropTable(
                name: "sensor_networks",
                schema: "control");

            migrationBuilder.DropTable(
                name: "sensor_profiles",
                schema: "control");

            migrationBuilder.DropTable(
                name: "areas",
                schema: "control");

            migrationBuilder.DropTable(
                name: "configuration_versions",
                schema: "control");
        }
    }
}
