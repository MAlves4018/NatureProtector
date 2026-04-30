using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;

namespace NatureProtector.Prevention.Host.Tests.TestInfrastructure;

internal sealed record SeededControlPlane(
    Guid ConfigurationVersionId,
    Guid AreaId,
    Guid GridCellId,
    Guid SensorProfileId,
    Guid SensorId);

internal static class ControlPlaneSeedData
{
    public static async Task<SeededControlPlane> SeedAreaWithSensorAsync(
        SqliteControlDbContextScope scope,
        Guid? areaId = null,
        Guid? sensorId = null,
        Guid? gridCellId = null,
        bool isActive = true)
    {
        var configurationVersionId = Guid.NewGuid();
        var versionNumber = Random.Shared.Next(1, int.MaxValue);
        var resolvedAreaId = areaId ?? Guid.NewGuid();
        var resolvedGridCellId = gridCellId ?? Guid.NewGuid();
        var sensorProfileId = Guid.NewGuid();
        var resolvedSensorId = sensorId ?? Guid.NewGuid();

        await scope.SeedAsync(dbContext =>
        {
            dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = configurationVersionId,
                VersionNumber = versionNumber,
                Description = "test",
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
                CreatedBy = "tests"
            });

            dbContext.Areas.Add(new AreaRecord
            {
                Id = resolvedAreaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "PT-PAN",
                Name = "Proenca-a-Nova"
            });

            dbContext.GridCells.Add(new GridCellRecord
            {
                Id = resolvedGridCellId,
                AreaId = resolvedAreaId,
                ConfigurationVersionId = configurationVersionId,
                CellCode = "CELL-001",
                CentroidLatitude = 39.75,
                CentroidLongitude = -7.92
            });

            dbContext.SensorProfiles.Add(new SensorProfileRecord
            {
                Id = sensorProfileId,
                ConfigurationVersionId = configurationVersionId,
                Name = "Default weather profile",
                SensorFamily = "weather"
            });

            dbContext.SensorNodes.Add(new SensorNodeRecord
            {
                Id = resolvedSensorId,
                AreaId = resolvedAreaId,
                GridCellId = resolvedGridCellId,
                ProfileId = sensorProfileId,
                ConfigurationVersionId = configurationVersionId,
                Name = "Sensor-01",
                Type = SensorType.WeatherStation,
                Latitude = 39.75,
                Longitude = -7.92,
                IsActive = isActive
            });

            return Task.CompletedTask;
        });

        return new SeededControlPlane(
            configurationVersionId,
            resolvedAreaId,
            resolvedGridCellId,
            sensorProfileId,
            resolvedSensorId);
    }
}
