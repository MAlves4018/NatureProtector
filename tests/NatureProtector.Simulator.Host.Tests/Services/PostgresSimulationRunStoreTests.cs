using System.Text.Json;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.Tests.TestInfrastructure;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class PostgresSimulationRunStoreTests
{
    [Fact]
    public void NormalizeUtc_ConvertsNonUtcOffsetToUtc()
    {
        var timestamp = new DateTimeOffset(2026, 4, 11, 10, 34, 32, TimeSpan.FromHours(2));

        var normalized = PostgresSimulationRunStore.NormalizeUtc(timestamp);

        Assert.Equal(TimeSpan.Zero, normalized.Offset);
        Assert.Equal(new DateTimeOffset(2026, 4, 11, 8, 34, 32, TimeSpan.Zero), normalized);
    }

    [Fact]
    public void NormalizeUtc_ReturnsNull_WhenNullableTimestampIsNull()
    {
        Assert.Null(PostgresSimulationRunStore.NormalizeUtc((DateTimeOffset?)null));
    }

    [Fact]
    public async Task UpsertAsync_DoesNothing_WhenConfigurationVersionIsMissing()
    {
        await using var scope = new SqliteControlDbContextScope();
        var store = new PostgresSimulationRunStore(scope.Factory);
        var context = CreateSimulationContext(configurationVersionId: null);
        var run = new SimulationRun(Guid.Parse("11111111-2222-3333-4444-555555555555"), executionSeed: 7);

        await store.UpsertAsync(context, run, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        Assert.Empty(dbContext.SimulationRuns);
    }

    [Fact]
    public async Task UpsertAsync_InsertsAndUpdatesSingleSimulationRunRecord()
    {
        await using var scope = new SqliteControlDbContextScope();
        var ids = await SeedControlPlaneAsync(scope);
        var store = new PostgresSimulationRunStore(scope.Factory);
        var context = CreateSimulationContext(configurationVersionId: ids.ConfigurationVersionId, areaId: ids.AreaId, scenarioId: ids.ScenarioId);
        var run = new SimulationRun(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), executionSeed: 42);

        await store.UpsertAsync(context, run, CancellationToken.None);

        run.MarkReady();
        run.Start(new DateTimeOffset(2026, 4, 12, 18, 0, 0, TimeSpan.FromHours(2)));
        run.Complete(new DateTimeOffset(2026, 4, 12, 18, 5, 0, TimeSpan.FromHours(2)));

        await store.UpsertAsync(context, run, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var records = dbContext.SimulationRuns.ToArray();
        var record = Assert.Single(records);

        Assert.Equal(run.Id, record.Id);
        Assert.Equal(ids.AreaId, record.AreaId);
        Assert.Equal(ids.ScenarioId, record.ScenarioId);
        Assert.Equal(ids.ConfigurationVersionId, record.ConfigurationVersionId);
        Assert.Equal("scenario-b", record.ScenarioCode);
        Assert.Equal("Scenario B", record.ScenarioName);
        Assert.Equal(SimulationRunStatus.Completed, record.Status);
        Assert.Equal(TimeSpan.Zero, record.StartedAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, record.EndedAt!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, record.LogicalStartTimestamp.Offset);
        Assert.Equal(5, record.IntervalSeconds);
        Assert.Equal(3, record.NumberOfCycles);
        Assert.Equal(42, record.ExecutionSeed);

        using var metadata = JsonDocument.Parse(record.MetadataJson!);
        Assert.Equal(1, metadata.RootElement.GetProperty("sensor_count").GetInt32());
        Assert.Equal("HighRisk", metadata.RootElement.GetProperty("scenario_category").GetString());
    }

    private static SimulationContext CreateSimulationContext(
        Guid? configurationVersionId,
        Guid? areaId = null,
        Guid? scenarioId = null)
    {
        var sensor = new Sensor(
            id: Guid.Parse("99999999-0000-0000-0000-000000000001"),
            name: "sim-temp-001",
            type: SensorType.Temperature,
            location: new Location(39.75, -7.90, 340),
            profile: new SensorProfile(
                id: Guid.Parse("99999999-0000-0000-0000-000000000002"),
                samplingInterval: TimeSpan.FromSeconds(5),
                communicationMode: "RabbitMq",
                noiseLevel: 0.1,
                latencyProfile: "Low latency",
                failureProfile: "Rare failures"));

        var scenario = new Scenario(
            id: scenarioId ?? Guid.Parse("99999999-0000-0000-0000-000000000003"),
            name: "Scenario B",
            category: ScenarioCategory.HighRisk,
            parameters: new ScenarioParameters(
                baseTemperature: 32,
                baseHumidity: 20,
                baseWindSpeed: 9,
                failureRate: 0.2,
                noiseLevel: 0.1,
                timeAcceleration: 2));

        return new SimulationContext(
            areaId: areaId ?? Guid.Parse("99999999-0000-0000-0000-000000000004"),
            scenario: scenario,
            sensors: [sensor],
            startTimestamp: new DateTimeOffset(2026, 4, 12, 16, 0, 0, TimeSpan.FromHours(2)),
            interval: TimeSpan.FromSeconds(5),
            numberOfCycles: 3,
            configurationVersionId: configurationVersionId,
            scenarioCode: "scenario-b");
    }

    private static async Task<SeededIds> SeedControlPlaneAsync(SqliteControlDbContextScope scope)
    {
        var configurationVersionId = Guid.Parse("12345678-0000-0000-0000-000000000001");
        var areaId = Guid.Parse("12345678-0000-0000-0000-000000000002");
        var scenarioId = Guid.Parse("12345678-0000-0000-0000-000000000003");

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

            dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
            {
                Id = scenarioId,
                AreaId = areaId,
                ConfigurationVersionId = configurationVersionId,
                Code = "scenario-b",
                Name = "Scenario B",
                ScenarioKind = ScenarioCategory.HighRisk,
                ParametersJson = "{}"
            });

            await Task.CompletedTask;
        });

        return new SeededIds(configurationVersionId, areaId, scenarioId);
    }

    private sealed record SeededIds(Guid ConfigurationVersionId, Guid AreaId, Guid ScenarioId);
}
