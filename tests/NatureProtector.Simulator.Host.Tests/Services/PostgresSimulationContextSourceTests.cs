using Microsoft.Extensions.Options;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.Tests.TestInfrastructure;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class PostgresSimulationContextSourceTests
{
    [Fact]
    public async Task CreateAsync_BuildsContext_FromControlPlaneCodes_AndAppliesProfileFallbacks()
    {
        await using var scope = new SqliteControlDbContextScope();
        var ids = await SeedControlPlaneAsync(scope, includeActiveSensor: true);
        var options = Options.Create(new SimulatorOptions
        {
            AreaId = Guid.Empty,
            ScenarioId = Guid.Empty,
            ControlPlaneAreaCode = "proenca-a-nova",
            ControlPlaneScenarioCode = "scenario_b",
            StartTimestamp = new DateTimeOffset(2026, 4, 12, 9, 0, 0, TimeSpan.Zero)
        });

        var source = new PostgresSimulationContextSource(scope.Factory, options);

        var context = await source.CreateAsync(CancellationToken.None);

        Assert.Equal(ids.AreaId, context.AreaId);
        Assert.Equal(ids.ConfigurationVersionId, context.ConfigurationVersionId);
        Assert.Equal("scenario_b", context.ScenarioCode);
        Assert.Equal(ScenarioCategory.HighRisk, context.Scenario.Category);
        Assert.Equal(33.5, context.Scenario.Parameters.BaseTemperature);
        Assert.Equal(21.0, context.Scenario.Parameters.BaseHumidity);
        Assert.Equal(8.1, context.Scenario.Parameters.BaseWindSpeed);
        Assert.Equal(0.15, context.Scenario.Parameters.FailureRate);
        Assert.Equal(0.22, context.Scenario.Parameters.NoiseLevel);
        Assert.Equal(2.5, context.Scenario.Parameters.TimeAcceleration);
        Assert.Equal(new DateTimeOffset(2026, 4, 12, 9, 0, 0, TimeSpan.Zero), context.StartTimestamp);
        Assert.Equal(TimeSpan.FromSeconds(15), context.Interval);
        Assert.Equal(6, context.NumberOfCycles);

        var sensor = Assert.Single(context.Sensors);
        Assert.Equal("pilot-temp-001", sensor.Name);
        Assert.Equal(SensorType.Temperature, sensor.Type);
        Assert.Equal(TimeSpan.FromSeconds(15), sensor.Profile.SamplingInterval);
        Assert.Equal("RabbitMq", sensor.Profile.CommunicationMode);
        Assert.Equal(0.22, sensor.Profile.NoiseLevel);
        Assert.Equal("Low latency", sensor.Profile.LatencyProfile);
        Assert.Equal("Rare failures", sensor.Profile.FailureProfile);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenResolvedAreaHasNoActiveSensors()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedControlPlaneAsync(scope, includeActiveSensor: false);
        var options = Options.Create(new SimulatorOptions
        {
            AreaId = Guid.Empty,
            ScenarioId = Guid.Empty,
            ControlPlaneAreaCode = "proenca-a-nova",
            ControlPlaneScenarioCode = "scenario_b"
        });

        var source = new PostgresSimulationContextSource(scope.Factory, options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => source.CreateAsync(CancellationToken.None));
        Assert.Contains("No active sensor nodes were found", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenAreaCannotBeResolved()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedControlPlaneAsync(scope, includeActiveSensor: true);
        var options = Options.Create(new SimulatorOptions
        {
            AreaId = Guid.Empty,
            ScenarioId = Guid.Empty,
            ControlPlaneAreaCode = "missing-area",
            ControlPlaneScenarioCode = "scenario_b"
        });

        var source = new PostgresSimulationContextSource(scope.Factory, options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => source.CreateAsync(CancellationToken.None));
        Assert.Contains("Control plane area could not be resolved", exception.Message);
    }

    private static async Task<SeededIds> SeedControlPlaneAsync(
        SqliteControlDbContextScope scope,
        bool includeActiveSensor)
    {
        var configurationVersionId = Guid.Parse("de000000-0000-0000-0000-000000000001");
        var areaId = Guid.Parse("de000000-0000-0000-0000-000000000002");
        var cellId = Guid.Parse("de000000-0000-0000-0000-000000000003");
        var profileId = Guid.Parse("de000000-0000-0000-0000-000000000004");
        var scenarioId = Guid.Parse("de000000-0000-0000-0000-000000000005");
        var sensorId = Guid.Parse("de000000-0000-0000-0000-000000000006");

        await scope.SeedAsync(async dbContext =>
        {
            dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
            {
                Id = configurationVersionId,
                VersionNumber = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            });

            dbContext.Areas.Add(new AreaRecord
            {
                Id = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "proenca-a-nova",
                Name = "Proença-a-Nova"
            });

            dbContext.GridCells.Add(new GridCellRecord
            {
                Id = cellId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                CellCode = "PRO-001",
                CentroidLatitude = 39.75,
                CentroidLongitude = -7.90
            });

            dbContext.SensorProfiles.Add(new SensorProfileRecord
            {
                Id = profileId,
                ConfigurationVersionId = configurationVersionId,
                Name = "pilot-profile",
                SensorFamily = "pilot",
                PublicationPolicyJson = "{\"sampling_interval_seconds\":15,\"communication_mode\":\"\"}",
                NoiseProfileJson = "{\"noise_level\":0.22}",
                FaultProfileJson = "{\"latency_profile\":\"\",\"failure_profile\":\"\"}"
            });

            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "scenario_b",
                Name = "Scenario B",
                ScenarioKind = ScenarioCategory.HighRisk,
                Description = "Critical weather context",
                ParametersJson =
                    """
                    {
                      "simulator_options": {
                        "BaseTemperature": 33.5,
                        "BaseHumidity": 21.0,
                        "BaseWindSpeed": 8.1,
                        "FailureRate": 0.15,
                        "NoiseLevel": 0.22,
                        "TimeAcceleration": 2.5,
                        "IntervalSeconds": 15,
                        "NumberOfCycles": 6
                      }
                    }
                    """
            });

            dbContext.SensorNodes.Add(new SensorNodeRecord
            {
                Id = sensorId,
                AreaId = areaId,
                GridCellId = cellId,
                ProfileId = profileId,
                ConfigurationVersionId = configurationVersionId,
                Name = "pilot-temp-001",
                Type = SensorType.Temperature,
                Latitude = 39.75,
                Longitude = -7.90,
                AltitudeMeters = 340,
                IsActive = includeActiveSensor
            });

            await Task.CompletedTask;
        });

        return new SeededIds(configurationVersionId, areaId);
    }

    private sealed record SeededIds(Guid ConfigurationVersionId, Guid AreaId);
}
