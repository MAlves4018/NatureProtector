using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;
using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresDailyCellStateRepositoryTests
{
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
            missing.ConfigurationVersionId);

        await repository.UpsertAsync(input, CancellationToken.None);

        var lookup = await repository.GetForReadingAsync(reading, simulationRunId: null, CancellationToken.None);

        Assert.Null(missing.State);
        Assert.Equal(ids.GridCellId, missing.GridCellId);
        Assert.NotNull(lookup.State);
        Assert.Equal(ids.AreaId, lookup.State!.AreaId);
        Assert.Equal(ids.GridCellId, lookup.State.GridCellId);
        Assert.Equal(ids.SensorId, lookup.State.SensorId);
        Assert.Equal(34.5, lookup.State.MaxTemperatureCelsius);
        Assert.Equal(reading.EventId, lookup.State.LastSourceEventId);
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
                CentroidLongitude = -7.9
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

    private static NormalizedReading CreateReading(
        Guid areaId,
        Guid sensorId,
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value)
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
            EventTime: new DateTimeOffset(2026, 5, 18, 12, 30, 0, TimeSpan.Zero),
            IngestTime: null);
    }

    private sealed record SeedIds(
        Guid ConfigurationVersionId,
        Guid AreaId,
        Guid GridCellId,
        Guid SensorProfileId,
        Guid SensorId);
}
