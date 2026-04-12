using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Projection;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class PostgresControlPlaneServiceTests
{
    [Fact]
    public async Task AvailabilityMetadata_IsExposed()
    {
        await using var scope = new SqliteControlDbContextScope();
        var service = new PostgresControlPlaneService(scope.Factory);

        Assert.True(service.IsAvailable);
        Assert.Equal("PostgreSQL-backed control plane is available.", service.AvailabilityMessage);
    }

    [Fact]
    public async Task ConfigurationQueries_ProjectCounts_AndActivateTargetVersion()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var service = new PostgresControlPlaneService(scope.Factory);

        var configurations = await service.ListConfigurationsAsync(CancellationToken.None);
        var activeBefore = await service.GetActiveConfigurationAsync(CancellationToken.None);
        var activated = await service.ActivateConfigurationAsync(2, CancellationToken.None);
        var activeAfter = await service.GetActiveConfigurationAsync(CancellationToken.None);
        var missing = await service.ActivateConfigurationAsync(999, CancellationToken.None);

        Assert.Equal([2, 1], configurations.Select(configuration => configuration.VersionNumber));

        var versionOne = Assert.Single(configurations, configuration => configuration.VersionNumber == 1);
        Assert.True(versionOne.IsActive);
        Assert.Equal(1, versionOne.AreaCount);
        Assert.Equal(2, versionOne.GridCellCount);
        Assert.Equal(3, versionOne.SensorNodeCount);
        Assert.Equal(2, versionOne.ScenarioCount);
        Assert.Equal(1, versionOne.SimulationRunCount);

        var versionTwo = Assert.Single(configurations, configuration => configuration.VersionNumber == 2);
        Assert.False(versionTwo.IsActive);
        Assert.Equal(1, versionTwo.AreaCount);

        Assert.NotNull(activeBefore);
        Assert.Equal(1, activeBefore!.VersionNumber);

        Assert.NotNull(activated);
        Assert.Equal(2, activated!.VersionNumber);
        Assert.True(activated.IsActive);

        Assert.NotNull(activeAfter);
        Assert.Equal(2, activeAfter!.VersionNumber);
        Assert.True(activeAfter.IsActive);

        Assert.Null(missing);

        await using var verificationContext = scope.CreateDbContext();
        var activeVersions = verificationContext.ConfigurationVersions.Where(entity => entity.IsActive).ToArray();
        Assert.Single(activeVersions);
        Assert.Equal(seed.ConfigurationVersion2Id, activeVersions[0].Id);
    }

    [Fact]
    public async Task TopologyQueries_ReturnProjectedAreaGridSensorAndScenarioData()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedAsync(scope);
        var service = new PostgresControlPlaneService(scope.Factory);

        var areas = await service.ListAreasAsync(configurationVersion: null, CancellationToken.None);
        var area = await service.GetAreaAsync("proenca-a-nova", configurationVersion: null, CancellationToken.None);
        var gridCells = await service.ListGridCellsAsync("proenca-a-nova", configurationVersion: null, skip: -5, take: 0, CancellationToken.None);
        var sensorNodes = await service.ListSensorNodesAsync("proenca-a-nova", configurationVersion: null, skip: 1, take: 1, CancellationToken.None);
        var scenarios = await service.ListScenariosAsync("proenca-a-nova", configurationVersion: null, CancellationToken.None);

        var summary = Assert.Single(areas);
        Assert.Equal("proenca-a-nova", summary.Code);
        Assert.Equal(2, summary.GridCellCount);
        Assert.Equal(2, summary.SensorNodeCount);
        Assert.Equal(2, summary.ScenarioCount);

        Assert.NotNull(area);
        Assert.Equal("Proença-a-Nova", area!.Name);
        Assert.NotNull(area.Context);
        Assert.Equal("Mixed forest", area.Context!.VegetationType);
        Assert.Equal(0.73, area.Context.VegetationDensity);

        Assert.Equal(2, gridCells.Count);
        Assert.Equal(["PRO-001", "PRO-002"], gridCells.Select(cell => cell.CellCode));
        Assert.Equal(1, gridCells[0].SensorNodeCount);
        Assert.Equal(1, gridCells[1].SensorNodeCount);

        var pagedSensor = Assert.Single(sensorNodes);
        Assert.Equal("pro-temp-001", pagedSensor.Name);
        Assert.True(pagedSensor.IsActive);

        Assert.Equal(2, scenarios.Count);
        var derivedScenario = Assert.Single(scenarios, scenario => scenario.Code == "scenario_b");
        Assert.Equal("scenario_a", derivedScenario.BaseScenarioCode);
        Assert.Equal(2, derivedScenario.DatasetBindingCount);
    }

    [Fact]
    public async Task RuntimeQueries_ReturnSimulationAndOperationalProjections()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var service = new PostgresControlPlaneService(scope.Factory);

        var runs = await service.ListSimulationRunsAsync("proenca-a-nova", "scenario_b", 1, skip: 0, take: 10, CancellationToken.None);
        var run = await service.GetSimulationRunAsync(seed.RunId, CancellationToken.None);
        var areaState = await service.GetAreaOperationalStateAsync("proenca-a-nova", configurationVersion: null, CancellationToken.None);
        var cellStates = await service.ListCellOperationalStatesAsync("proenca-a-nova", configurationVersion: null, skip: 0, take: 10, CancellationToken.None);
        var alerts = await service.ListActiveAlertsAsync("proenca-a-nova", 1, CancellationToken.None);

        var listedRun = Assert.Single(runs);
        Assert.Equal(seed.RunId, listedRun.Id);
        Assert.Equal("Completed", listedRun.Status);
        Assert.Equal(24, listedRun.NumberOfCycles);

        Assert.NotNull(run);
        Assert.Equal("scenario_b", run!.ScenarioCode);
        Assert.Equal(1234, run.ExecutionSeed);

        Assert.NotNull(areaState);
        Assert.Equal("VeryHigh", areaState!.AggregateRiskLevel);
        Assert.Equal("Critical", areaState.Severity);
        Assert.Equal(12, areaState.AssessmentCount);

        Assert.Equal(2, cellStates.Count);
        Assert.Equal(["PRO-002", "PRO-001"], cellStates.Select(state => state.CellCode));
        Assert.Equal("pro-humidity-001", cellStates[0].SensorName);

        var alert = Assert.Single(alerts);
        Assert.Equal("area-risk-high", alert.AlertCode);
        Assert.Equal("Open", alert.Status);
    }

    [Fact]
    public async Task Queries_ReturnEmptyOrNull_WhenConfigurationCannotBeResolved()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await SeedAsync(scope);
        var service = new PostgresControlPlaneService(scope.Factory);

        Assert.Empty(await service.ListAreasAsync(999, CancellationToken.None));
        Assert.Null(await service.GetAreaAsync("proenca-a-nova", 999, CancellationToken.None));
        Assert.Empty(await service.ListGridCellsAsync("proenca-a-nova", 999, 0, 10, CancellationToken.None));
        Assert.Empty(await service.ListSensorNodesAsync("proenca-a-nova", 999, 0, 10, CancellationToken.None));
        Assert.Empty(await service.ListScenariosAsync("proenca-a-nova", 999, CancellationToken.None));
        Assert.Null(await service.GetAreaOperationalStateAsync("proenca-a-nova", 999, CancellationToken.None));
        Assert.Empty(await service.ListCellOperationalStatesAsync("proenca-a-nova", 999, 0, 10, CancellationToken.None));
        Assert.Empty(await service.ListActiveAlertsAsync("proenca-a-nova", 999, CancellationToken.None));
        Assert.Empty(await service.ListSimulationRunsAsync("proenca-a-nova", "scenario_b", seed.ConfigurationVersion2Number, 0, 10, CancellationToken.None));
    }

    private static async Task<SeededIds> SeedAsync(SqliteControlDbContextScope scope)
    {
        var configurationVersion1Id = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var configurationVersion2Id = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var area1Id = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var area2Id = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var cell1Id = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var cell2Id = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var cell3Id = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var profile1Id = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var network1Id = Guid.Parse("50000000-0000-0000-0000-000000000001");
        var sensor1Id = Guid.Parse("60000000-0000-0000-0000-000000000001");
        var sensor2Id = Guid.Parse("60000000-0000-0000-0000-000000000002");
        var sensor3Id = Guid.Parse("60000000-0000-0000-0000-000000000003");
        var sensor4Id = Guid.Parse("60000000-0000-0000-0000-000000000004");
        var scenarioAId = Guid.Parse("70000000-0000-0000-0000-000000000001");
        var scenarioBId = Guid.Parse("70000000-0000-0000-0000-000000000002");
        var scenarioCId = Guid.Parse("70000000-0000-0000-0000-000000000003");
        var artifactAId = Guid.Parse("80000000-0000-0000-0000-000000000001");
        var artifactBId = Guid.Parse("80000000-0000-0000-0000-000000000002");
        var runId = Guid.Parse("90000000-0000-0000-0000-000000000001");
        var areaStateId = Guid.Parse("a0000000-0000-0000-0000-000000000001");

        await scope.SeedAsync(async dbContext =>
        {
            dbContext.ConfigurationVersions.AddRange(
                new ConfigurationVersionRecord
                {
                    Id = configurationVersion1Id,
                    VersionNumber = 1,
                    Description = "Pilot v1",
                    IsActive = true,
                    CreatedAt = new DateTimeOffset(2026, 4, 12, 10, 0, 0, TimeSpan.Zero),
                    CreatedBy = "tests"
                },
                new ConfigurationVersionRecord
                {
                    Id = configurationVersion2Id,
                    VersionNumber = 2,
                    Description = "Pilot v2",
                    IsActive = false,
                    CreatedAt = new DateTimeOffset(2026, 4, 12, 11, 0, 0, TimeSpan.Zero),
                    CreatedBy = "tests"
                });

            dbContext.Areas.AddRange(
                new AreaRecord
                {
                    Id = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    Code = "proenca-a-nova",
                    Name = "Proença-a-Nova",
                    CountryCode = "PT",
                    GeometryGeoJson = "{\"type\":\"Polygon\"}",
                    MetadataJson = "{\"source\":\"tests\"}"
                },
                new AreaRecord
                {
                    Id = area2Id,
                    ConfigurationVersionId = configurationVersion2Id,
                    Code = "castelo-branco",
                    Name = "Castelo Branco",
                    CountryCode = "PT"
                });

            dbContext.AreaContexts.Add(new AreaContextRecord
            {
                Id = Guid.Parse("21000000-0000-0000-0000-000000000001"),
                AreaId = area1Id,
                VegetationType = "Mixed forest",
                VegetationDensity = 0.73,
                PopulationExposure = 0.24,
                CriticalInfrastructureExposure = 0.11,
                Seasonality = "summer_peak"
            });

            dbContext.GridCells.AddRange(
                new GridCellRecord
                {
                    Id = cell1Id,
                    AreaId = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    CellCode = "PRO-001",
                    CentroidLatitude = 39.75,
                    CentroidLongitude = -7.90,
                    AltitudeMeters = 340,
                    SlopeDegrees = 7.5,
                    AspectDegrees = 125,
                    LandCoverClass = "forest",
                    StructuralHazard = "high"
                },
                new GridCellRecord
                {
                    Id = cell2Id,
                    AreaId = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    CellCode = "PRO-002",
                    CentroidLatitude = 39.76,
                    CentroidLongitude = -7.89,
                    AltitudeMeters = 355,
                    SlopeDegrees = 11.2,
                    AspectDegrees = 210,
                    LandCoverClass = "shrubs",
                    StructuralHazard = "medium"
                },
                new GridCellRecord
                {
                    Id = cell3Id,
                    AreaId = area2Id,
                    ConfigurationVersionId = configurationVersion2Id,
                    CellCode = "CB-001",
                    CentroidLatitude = 39.82,
                    CentroidLongitude = -7.48
                });

            dbContext.SensorProfiles.Add(new SensorProfileRecord
            {
                Id = profile1Id,
                ConfigurationVersionId = configurationVersion1Id,
                Name = "pilot-profile",
                SensorFamily = "pilot"
            });

            dbContext.SensorNetworks.Add(new SensorNetworkRecord
            {
                Id = network1Id,
                ConfigurationVersionId = configurationVersion1Id,
                Name = "pilot-network"
            });

            dbContext.SensorNodes.AddRange(
                new SensorNodeRecord
                {
                    Id = sensor1Id,
                    AreaId = area1Id,
                    GridCellId = cell1Id,
                    ProfileId = profile1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    NetworkId = network1Id,
                    Name = "pro-temp-001",
                    Type = SensorType.Temperature,
                    Latitude = 39.75,
                    Longitude = -7.90,
                    AltitudeMeters = 340,
                    IsActive = true,
                    InstallationProfile = "field"
                },
                new SensorNodeRecord
                {
                    Id = sensor2Id,
                    AreaId = area1Id,
                    GridCellId = cell1Id,
                    ProfileId = profile1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    NetworkId = network1Id,
                    Name = "pro-temp-002",
                    Type = SensorType.Temperature,
                    Latitude = 39.751,
                    Longitude = -7.901,
                    AltitudeMeters = 341,
                    IsActive = false,
                    InstallationProfile = "backup"
                },
                new SensorNodeRecord
                {
                    Id = sensor3Id,
                    AreaId = area1Id,
                    GridCellId = cell2Id,
                    ProfileId = profile1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    NetworkId = network1Id,
                    Name = "pro-humidity-001",
                    Type = SensorType.Humidity,
                    Latitude = 39.76,
                    Longitude = -7.89,
                    AltitudeMeters = 355,
                    IsActive = true,
                    InstallationProfile = "field"
                },
                new SensorNodeRecord
                {
                    Id = sensor4Id,
                    AreaId = area2Id,
                    GridCellId = cell3Id,
                    ProfileId = profile1Id,
                    ConfigurationVersionId = configurationVersion2Id,
                    Name = "cb-temp-001",
                    Type = SensorType.Temperature,
                    Latitude = 39.82,
                    Longitude = -7.48,
                    IsActive = true
                });

            dbContext.ScenarioDefinitions.AddRange(
                new ScenarioDefinitionRecord
                {
                    Id = scenarioAId,
                    AreaId = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    Code = "scenario_a",
                    Name = "Scenario A",
                    ScenarioKind = ScenarioCategory.Base,
                    Description = "Moderate conditions"
                },
                new ScenarioDefinitionRecord
                {
                    Id = scenarioBId,
                    AreaId = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    BaseScenarioId = scenarioAId,
                    Code = "scenario_b",
                    Name = "Scenario B",
                    ScenarioKind = ScenarioCategory.HighRisk,
                    Description = "Critical conditions"
                },
                new ScenarioDefinitionRecord
                {
                    Id = scenarioCId,
                    AreaId = area2Id,
                    ConfigurationVersionId = configurationVersion2Id,
                    Code = "scenario_c",
                    Name = "Scenario C",
                    ScenarioKind = ScenarioCategory.Exercise,
                    Description = "Castelo Branco exercise"
                });

            dbContext.DatasetArtifacts.AddRange(
                new DatasetArtifactRecord
                {
                    Id = artifactAId,
                    DatasetCode = "weather_reference",
                    DatasetType = "weather",
                    SourceName = "tests",
                    AreaCode = "proenca-a-nova",
                    Version = "v1",
                    Format = "parquet",
                    RelativePath = "data/weather_reference.parquet"
                },
                new DatasetArtifactRecord
                {
                    Id = artifactBId,
                    DatasetCode = "scenario_candidates",
                    DatasetType = "scenario",
                    SourceName = "tests",
                    AreaCode = "proenca-a-nova",
                    Version = "v1",
                    Format = "parquet",
                    RelativePath = "data/scenario_candidates.parquet"
                });

            dbContext.ScenarioDatasetBindings.AddRange(
                new ScenarioDatasetBindingRecord
                {
                    Id = Guid.Parse("81000000-0000-0000-0000-000000000001"),
                    ScenarioId = scenarioAId,
                    DatasetArtifactId = artifactAId,
                    BindingRole = "reference"
                },
                new ScenarioDatasetBindingRecord
                {
                    Id = Guid.Parse("81000000-0000-0000-0000-000000000002"),
                    ScenarioId = scenarioBId,
                    DatasetArtifactId = artifactAId,
                    BindingRole = "reference"
                },
                new ScenarioDatasetBindingRecord
                {
                    Id = Guid.Parse("81000000-0000-0000-0000-000000000003"),
                    ScenarioId = scenarioBId,
                    DatasetArtifactId = artifactBId,
                    BindingRole = "candidate"
                });

            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = area1Id,
                ScenarioId = scenarioBId,
                ConfigurationVersionId = configurationVersion1Id,
                ScenarioCode = "scenario_b",
                ScenarioName = "Scenario B",
                CreatedAt = new DateTimeOffset(2026, 4, 12, 12, 0, 0, TimeSpan.Zero),
                StartedAt = new DateTimeOffset(2026, 4, 12, 12, 1, 0, TimeSpan.Zero),
                EndedAt = new DateTimeOffset(2026, 4, 12, 12, 20, 0, TimeSpan.Zero),
                LogicalStartTimestamp = new DateTimeOffset(2020, 9, 13, 10, 0, 0, TimeSpan.Zero),
                IntervalSeconds = 300,
                NumberOfCycles = 24,
                ExecutionSeed = 1234,
                Status = SimulationRunStatus.Completed,
                MetadataJson = "{\"source\":\"tests\"}"
            });

            dbContext.AreaOperationalStates.Add(new AreaOperationalStateRecord
            {
                Id = areaStateId,
                AreaId = area1Id,
                ConfigurationVersionId = configurationVersion1Id,
                SimulationRunId = runId,
                SnapshotTimestamp = new DateTimeOffset(2026, 4, 12, 12, 19, 0, TimeSpan.Zero),
                AggregateRiskScore = 0.91,
                AggregateRiskLevel = "VeryHigh",
                Severity = "Critical",
                Summary = "Very high area risk",
                AssessmentCount = 12,
                UpdatedAt = new DateTimeOffset(2026, 4, 12, 12, 19, 30, TimeSpan.Zero)
            });

            dbContext.CellOperationalStates.AddRange(
                new CellOperationalStateRecord
                {
                    Id = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                    AreaId = area1Id,
                    GridCellId = cell1Id,
                    SensorId = sensor1Id,
                    SnapshotTimestamp = new DateTimeOffset(2026, 4, 12, 12, 18, 0, TimeSpan.Zero),
                    RiskScore = 0.72,
                    RiskLevel = "High",
                    Severity = "High",
                    Summary = "High temperature risk",
                    UpdatedAt = new DateTimeOffset(2026, 4, 12, 12, 18, 30, TimeSpan.Zero)
                },
                new CellOperationalStateRecord
                {
                    Id = Guid.Parse("b0000000-0000-0000-0000-000000000002"),
                    AreaId = area1Id,
                    GridCellId = cell2Id,
                    SensorId = sensor3Id,
                    SnapshotTimestamp = new DateTimeOffset(2026, 4, 12, 12, 19, 0, TimeSpan.Zero),
                    RiskScore = 0.95,
                    RiskLevel = "Extreme",
                    Severity = "Emergency",
                    Summary = "Critical humidity risk",
                    UpdatedAt = new DateTimeOffset(2026, 4, 12, 12, 19, 40, TimeSpan.Zero)
                });

            dbContext.AlertStates.AddRange(
                new AlertStateRecord
                {
                    Id = Guid.Parse("c0000000-0000-0000-0000-000000000001"),
                    AreaId = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    AreaOperationalStateId = areaStateId,
                    AlertCode = "area-risk-high",
                    Severity = "Critical",
                    Status = "Open",
                    Message = "Area risk is VeryHigh with score 0.91.",
                    TriggeredAt = new DateTimeOffset(2026, 4, 12, 12, 19, 0, TimeSpan.Zero),
                    UpdatedAt = new DateTimeOffset(2026, 4, 12, 12, 19, 30, TimeSpan.Zero)
                },
                new AlertStateRecord
                {
                    Id = Guid.Parse("c0000000-0000-0000-0000-000000000002"),
                    AreaId = area1Id,
                    ConfigurationVersionId = configurationVersion1Id,
                    AreaOperationalStateId = areaStateId,
                    AlertCode = "area-risk-closed",
                    Severity = "Low",
                    Status = "Resolved",
                    Message = "Resolved alert",
                    TriggeredAt = new DateTimeOffset(2026, 4, 12, 11, 0, 0, TimeSpan.Zero),
                    UpdatedAt = new DateTimeOffset(2026, 4, 12, 11, 30, 0, TimeSpan.Zero),
                    ResolvedAt = new DateTimeOffset(2026, 4, 12, 11, 30, 0, TimeSpan.Zero)
                });

            await Task.CompletedTask;
        });

        return new SeededIds(configurationVersion1Id, configurationVersion2Id, 2, runId);
    }

    private sealed record SeededIds(
        Guid ConfigurationVersion1Id,
        Guid ConfigurationVersion2Id,
        int ConfigurationVersion2Number,
        Guid RunId);
}
