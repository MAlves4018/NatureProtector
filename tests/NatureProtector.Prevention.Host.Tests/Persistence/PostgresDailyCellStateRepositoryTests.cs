using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Projection;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;
using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresDailyCellStateRepositoryTests
{
    [Fact]
    public async Task DailyCellState_TextualContextFields_AreMappedAsText()
    {
        await using var scope = new SqliteControlDbContextScope();
        await using var dbContext = scope.CreateDbContext();
        var entityType = dbContext.Model.FindEntityType(typeof(DailyCellStateRecord));

        Assert.NotNull(entityType);
        foreach (var propertyName in new[]
        {
            nameof(DailyCellStateRecord.AntecedentState),
            nameof(DailyCellStateRecord.DroughtContext),
            nameof(DailyCellStateRecord.KbdiLimitations),
            nameof(DailyCellStateRecord.FireIndexProvenance),
            nameof(DailyCellStateRecord.FireWeatherLimitations),
            nameof(DailyCellStateRecord.Provenance)
        })
        {
            var property = entityType!.FindProperty(propertyName);

            Assert.NotNull(property);
            Assert.Null(property!.GetMaxLength());
            Assert.Equal("text", property.GetColumnType());
        }

        Assert.Equal(50, entityType!.FindProperty(nameof(DailyCellStateRecord.KbdiCalculationStatus))!.GetMaxLength());
        Assert.Equal(50, entityType.FindProperty(nameof(DailyCellStateRecord.FireWeatherCalculationStatus))!.GetMaxLength());
        Assert.Equal(100, entityType.FindProperty(nameof(DailyCellStateRecord.CandidateParameterSetVersion))!.GetMaxLength());
    }

    [Fact]
    public async Task UpsertAsync_CreatesAndReadsDailyCellState_ByCellAndDay()
    {
        await using var scope = new SqliteControlDbContextScope();
        var ids = await SeedTopologyAsync(scope);
        var repository = new PostgresDailyCellStateRepository(scope.Factory);
        var reading = CreateReading(ids.AreaId, ids.SensorId, SensorMetricType.Temperature, MeasurementUnit.Celsius, 34.5);
        var missing = await repository.GetForReadingAsync(reading, simulationRunId: null, CancellationToken.None);
        var input = RiskInput.FromNormalizedReading(
            reading,
            RiskEligibilityResult.Eligible,
            missing.State,
            simulationRunId: null,
            missing.GridCellId,
            missing.ConfigurationVersionId,
            missing.TerritorialContext);

        await repository.UpsertAsync(input, CancellationToken.None);

        var lookup = await repository.GetForReadingAsync(reading, simulationRunId: null, CancellationToken.None);

        Assert.Null(missing.State);
        Assert.Equal(ids.GridCellId, missing.GridCellId);
        Assert.NotNull(missing.TerritorialContext);
        Assert.Equal(ids.GridCellId, missing.TerritorialContext!.GridCellId);
        Assert.True(missing.TerritorialContext.TerritoryComponent > 0.5);
        Assert.NotNull(lookup.State);
        Assert.Equal(ids.AreaId, lookup.State!.AreaId);
        Assert.Equal(ids.GridCellId, lookup.State.GridCellId);
        Assert.Equal(ids.SensorId, lookup.State.SensorId);
        Assert.Equal(34.5, lookup.State.MaxTemperatureCelsius);
        Assert.Equal(reading.EventId, lookup.State.LastSourceEventId);
    }

    [Fact]
    public async Task UpsertAsync_UsesScenarioDailyReference_AndTreatsZeroPrecipitationAsPresent()
    {
        await using var scope = new SqliteControlDbContextScope();
        var ids = await SeedTopologyAsync(scope);
        var scenarioId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        await scope.SeedAsync(dbContext =>
        {
            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = ids.AreaId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                Code = "scenario_b",
                Name = "Scenario B",
                ScenarioKind = ScenarioCategory.HighRisk,
                ParametersJson = """
                    {
                      "daily_reference": {
                        "temperature_min_c": 20.5,
                        "temperature_mean_c": 26.842,
                        "temperature_max_c": 33.9,
                        "relative_humidity_min_pct": 18.0,
                        "precipitation_total_mm": 0.0,
                        "wind_speed_max_ms": 4.95,
                        "fwi_reference": 65.377,
                        "kbdi_reference": 650.106,
                        "fire_index_reference_kind": "critical_index_context"
                      },
                      "simulator_options": {}
                    }
                    """
            });
            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = ids.AreaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                ScenarioCode = "scenario_b",
                ScenarioName = "Scenario B",
                CreatedAt = DateTimeOffset.UtcNow,
                LogicalStartTimestamp = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
                IntervalSeconds = 30,
                NumberOfCycles = 1,
                Status = SimulationRunStatus.Running
            });

            return Task.CompletedTask;
        });

        var repository = new PostgresDailyCellStateRepository(scope.Factory);
        var reading = CreateReading(ids.AreaId, ids.SensorId, SensorMetricType.Temperature, MeasurementUnit.Celsius, 31.0);
        var lookup = await repository.GetForReadingAsync(reading, runId, CancellationToken.None);
        var input = RiskInput.FromNormalizedReading(
            reading,
            RiskEligibilityResult.Eligible,
            lookup.State,
            runId,
            lookup.GridCellId,
            lookup.ConfigurationVersionId,
            lookup.TerritorialContext);

        await repository.UpsertAsync(input, CancellationToken.None);

        var updated = await repository.GetForReadingAsync(reading, runId, CancellationToken.None);

        Assert.NotNull(updated.State);
        Assert.Equal(runId, updated.State!.SimulationRunId);
        Assert.Equal(0.0, updated.State!.DailyPrecipitationMillimeters);
        Assert.Equal(33.9, updated.State.MaxTemperatureCelsius);
        Assert.Equal(18.0, updated.State.LatestHumidityPercent);
        Assert.Equal(4.95, updated.State.LatestWindSpeedMetersPerSecond);
        Assert.NotNull(updated.State.FireWeatherIndex);
        Assert.NotNull(updated.State.KeetchByramDroughtIndex);
        Assert.NotEqual(FireWeatherIndexCalculationStatus.Partial, updated.State.FireWeatherCalculationStatus);
        Assert.NotEqual(KbdiCalculationStatus.Missing, updated.State.KbdiCalculationStatus);
        Assert.Equal(KbdiCalculationStatus.LimitedAntecedentHistory, updated.State.KbdiCalculationStatus);
        Assert.DoesNotContain("precipitation_24h_missing", updated.State.FireWeatherLimitations ?? string.Empty);
        Assert.Contains("scenario_daily_reference", updated.State.Provenance);
    }

    [Fact]
    public async Task UpsertAsync_ThrowsSpecificPermanentException_WhenSimulationRunIsMissing()
    {
        await using var scope = new SqliteControlDbContextScope();
        var ids = await SeedTopologyAsync(scope);
        var repository = new PostgresDailyCellStateRepository(scope.Factory);
        var missingRunId = Guid.NewGuid();
        var reading = CreateReading(ids.AreaId, ids.SensorId, SensorMetricType.Temperature, MeasurementUnit.Celsius, 31.0);
        var lookup = await repository.GetForReadingAsync(reading, missingRunId, CancellationToken.None);
        var input = RiskInput.FromNormalizedReading(
            reading,
            RiskEligibilityResult.Eligible,
            lookup.State,
            missingRunId,
            lookup.GridCellId,
            lookup.ConfigurationVersionId,
            lookup.TerritorialContext);

        var exception = await Assert.ThrowsAsync<MissingSimulationRunReferenceException>(
            () => repository.UpsertAsync(input, CancellationToken.None));

        Assert.Equal(missingRunId, exception.SimulationRunId);
        Assert.Contains("control.simulation_runs", exception.Message);
        await using var dbContext = scope.CreateDbContext();
        Assert.Empty(await dbContext.DailyCellStates.ToListAsync());
    }

    [Fact]
    public async Task UpsertAsync_DoesNotAdvanceKbdiMultipleTimesWithinSameLogicalDate()
    {
        await using var scope = new SqliteControlDbContextScope();
        var ids = await SeedTopologyAsync(scope);
        var scenarioId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        await scope.SeedAsync(dbContext =>
        {
            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = ids.AreaId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                Code = "scenario_b",
                Name = "Scenario B",
                ScenarioKind = ScenarioCategory.HighRisk,
                ParametersJson = """
                    {
                      "daily_reference": {
                        "temperature_max_c": 33.9,
                        "relative_humidity_min_pct": 18.0,
                        "precipitation_total_mm": 0.0,
                        "wind_speed_max_ms": 4.95
                      },
                      "simulator_options": {}
                    }
                    """
            });
            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = ids.AreaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                ScenarioCode = "scenario_b",
                ScenarioName = "Scenario B",
                CreatedAt = DateTimeOffset.UtcNow,
                LogicalStartTimestamp = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
                IntervalSeconds = 30,
                NumberOfCycles = 2,
                Status = SimulationRunStatus.Running
            });

            return Task.CompletedTask;
        });

        var repository = new PostgresDailyCellStateRepository(scope.Factory);
        var firstReading = CreateReading(ids.AreaId, ids.SensorId, SensorMetricType.Temperature, MeasurementUnit.Celsius, 31.0);
        var firstLookup = await repository.GetForReadingAsync(firstReading, runId, CancellationToken.None);
        await repository.UpsertAsync(RiskInput.FromNormalizedReading(
            firstReading,
            RiskEligibilityResult.Eligible,
            firstLookup.State,
            runId,
            firstLookup.GridCellId,
            firstLookup.ConfigurationVersionId,
            firstLookup.TerritorialContext), CancellationToken.None);
        var firstState = (await repository.GetForReadingAsync(firstReading, runId, CancellationToken.None)).State!;

        var secondReading = CreateReading(
            ids.AreaId,
            ids.SensorId,
            SensorMetricType.WindSpeed,
            MeasurementUnit.MetersPerSecond,
            5.0,
            firstReading.EventTime.AddSeconds(30));
        await repository.UpsertAsync(RiskInput.FromNormalizedReading(
            secondReading,
            RiskEligibilityResult.Eligible,
            firstState,
            runId,
            firstState.GridCellId,
            firstState.ConfigurationVersionId,
            firstLookup.TerritorialContext), CancellationToken.None);
        var secondState = (await repository.GetForReadingAsync(secondReading, runId, CancellationToken.None)).State!;

        Assert.Equal(firstState.KeetchByramDroughtIndex, secondState.KeetchByramDroughtIndex);
        Assert.Equal(firstState.PreviousKeetchByramDroughtIndex, secondState.PreviousKeetchByramDroughtIndex);
        Assert.Equal(KbdiCalculationStatus.LimitedAntecedentHistory, secondState.KbdiCalculationStatus);
    }

    [Fact]
    public async Task UpsertAsync_ToleratesConcurrentCreates_ForSameCellDayAndRun()
    {
        await using var scope = new SqliteControlDbContextScope(useFileDatabase: true);
        var ids = await SeedTopologyAsync(scope);
        var runId = await SeedSimulationRunAsync(scope, ids, numberOfCycles: 3);
        var repository = new PostgresDailyCellStateRepository(scope.Factory);
        var firstEventTime = new DateTimeOffset(2026, 5, 18, 12, 30, 0, TimeSpan.Zero);
        var readings = Enumerable.Range(0, 3)
            .Select(index => CreateReading(
                ids.AreaId,
                ids.SensorId,
                SensorMetricType.Temperature,
                MeasurementUnit.Celsius,
                31.0 + index,
                firstEventTime.AddSeconds(index)))
            .ToArray();
        var lookups = await Task.WhenAll(readings.Select(reading =>
            repository.GetForReadingAsync(reading, runId, CancellationToken.None)));
        var inputs = readings
            .Zip(lookups, (reading, lookup) => RiskInput.FromNormalizedReading(
                reading,
                RiskEligibilityResult.Eligible,
                lookup.State,
                runId,
                lookup.GridCellId,
                lookup.ConfigurationVersionId,
                lookup.TerritorialContext))
            .ToArray();

        await Task.WhenAll(inputs.Select(input => repository.UpsertAsync(input, CancellationToken.None)));

        await using var dbContext = scope.CreateDbContext();
        var day = new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);
        var dailyStates = await dbContext.DailyCellStates
            .Where(entity =>
                entity.AreaId == ids.AreaId &&
                entity.GridCellId == ids.GridCellId &&
                entity.LogicalDate == day &&
                entity.SimulationRunId == runId)
            .ToListAsync();

        Assert.All(lookups, lookup => Assert.Null(lookup.State));
        var dailyState = Assert.Single(dailyStates);
        Assert.Contains(readings, reading => reading.EventId == dailyState.LastSourceEventId);
    }

    private static async Task<SeedIds> SeedTopologyAsync(SqliteControlDbContextScope scope)
    {
        var ids = new SeedIds(
            ConfigurationVersionId: Guid.NewGuid(),
            AreaId: Guid.NewGuid(),
            GridCellId: Guid.NewGuid(),
            SensorProfileId: Guid.NewGuid(),
            SensorId: Guid.NewGuid());

        await scope.SeedAsync(dbContext =>
        {
            dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = ids.ConfigurationVersionId,
                VersionNumber = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            dbContext.Areas.Add(new AreaRecord
            {
                Id = ids.AreaId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                Code = "area-daily",
                Name = "Area Daily"
            });
            dbContext.GridCells.Add(new GridCellRecord
            {
                Id = ids.GridCellId,
                AreaId = ids.AreaId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                CellCode = "cell-daily",
                CentroidLatitude = 39.8,
                CentroidLongitude = -7.9,
                AltitudeMeters = 450,
                SlopeDegrees = 20,
                AspectDegrees = 180,
                LandCoverClass = "forest",
                DominantFuelModel = "pine",
                StructuralHazard = "high"
            });
            dbContext.SensorProfiles.Add(new SensorProfileRecord
            {
                Id = ids.SensorProfileId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                Name = "profile-daily"
            });
            dbContext.SensorNodes.Add(new SensorNodeRecord
            {
                Id = ids.SensorId,
                AreaId = ids.AreaId,
                GridCellId = ids.GridCellId,
                ProfileId = ids.SensorProfileId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                Name = "sensor-daily",
                Type = SensorType.Temperature,
                Latitude = 39.8,
                Longitude = -7.9,
                IsActive = true
            });

            return Task.CompletedTask;
        });

        return ids;
    }

    private static async Task<Guid> SeedSimulationRunAsync(
        SqliteControlDbContextScope scope,
        SeedIds ids,
        int numberOfCycles)
    {
        var scenarioId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        await scope.SeedAsync(dbContext =>
        {
            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = ids.AreaId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                Code = "scenario-concurrent",
                Name = "Scenario Concurrent",
                ScenarioKind = ScenarioCategory.HighRisk,
                ParametersJson = """
                    {
                      "daily_reference": {
                        "temperature_max_c": 33.9,
                        "relative_humidity_min_pct": 18.0,
                        "precipitation_total_mm": 0.0,
                        "wind_speed_max_ms": 4.95
                      },
                      "simulator_options": {}
                    }
                    """
            });
            dbContext.SimulationRuns.Add(new SimulationRunRecord
            {
                Id = runId,
                AreaId = ids.AreaId,
                ScenarioId = scenarioId,
                ConfigurationVersionId = ids.ConfigurationVersionId,
                ScenarioCode = "scenario-concurrent",
                ScenarioName = "Scenario Concurrent",
                CreatedAt = DateTimeOffset.UtcNow,
                LogicalStartTimestamp = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
                IntervalSeconds = 30,
                NumberOfCycles = numberOfCycles,
                Status = SimulationRunStatus.Running
            });

            return Task.CompletedTask;
        });

        return runId;
    }

    private static NormalizedReading CreateReading(
        Guid areaId,
        Guid sensorId,
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value,
        DateTimeOffset? eventTime = null)
    {
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "daily-state-test",
            AreaId: areaId,
            SensorId: sensorId,
            SensorName: "sensor-daily",
            MetricType: metricType,
            Value: value,
            Unit: unit,
            Latitude: 39.8,
            Longitude: -7.9,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: eventTime ?? new DateTimeOffset(2026, 5, 18, 12, 30, 0, TimeSpan.Zero),
            IngestTime: null);
    }

    private sealed record SeedIds(
        Guid ConfigurationVersionId,
        Guid AreaId,
        Guid GridCellId,
        Guid SensorProfileId,
        Guid SensorId);
}
