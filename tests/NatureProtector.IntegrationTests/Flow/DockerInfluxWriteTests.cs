using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerInfluxWriteTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task InfluxWriteService_WritesPipelineBatch_OnRealInfluxDb()
    {
        await using var influxDatabase = await TemporaryInfluxDatabase.CreateAsync();
        var options = influxDatabase.CreateOptions();
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var envelope = CreateEnvelope(areaId, sensorId, timestamp, simulationRunId);
        var assessment = CreateAssessment(timestamp.AddSeconds(1));
        var snapshot = CreateSnapshot(timestamp.AddSeconds(2));
        using var service = new InfluxWriteService(
            Options.Create(options),
            NullLogger<InfluxWriteService>.Instance);
        var batch = new InfluxTelemetryBatch()
            .AddAcceptedReading(envelope)
            .AddRiskAssessment(areaId, sensorId, assessment, envelope.EventId, simulationRunId)
            .AddAreaRiskSnapshot(areaId, 1, snapshot, envelope.EventId, simulationRunId);

        await service.WriteBatchAsync(batch, CancellationToken.None);

        var acceptedReading = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"""
            SELECT time, event_id, simulation_run_id, area_id, sensor_id, sensor_name,
                   metric_type, unit, operational_state, value, latitude, longitude
            FROM accepted_readings
            WHERE event_id = '{envelope.EventId}'
            """);
        AssertString(acceptedReading, "event_id", envelope.EventId.ToString());
        AssertString(acceptedReading, "simulation_run_id", simulationRunId.ToString());
        AssertString(acceptedReading, "area_id", areaId.ToString());
        AssertString(acceptedReading, "sensor_id", sensorId.ToString());
        AssertString(acceptedReading, "sensor_name", "Docker-Influx-Sensor");
        AssertString(acceptedReading, "metric_type", SensorMetricType.Temperature.ToString());
        AssertString(acceptedReading, "unit", MeasurementUnit.Celsius.ToString());
        AssertString(acceptedReading, "operational_state", SensorOperationalState.Nominal.ToString());
        AssertNumber(acceptedReading, "value", 34.2);
        AssertNumber(acceptedReading, "latitude", 39.8);
        AssertNumber(acceptedReading, "longitude", -7.9);
        AssertTimestamp(acceptedReading, timestamp);

        var riskAssessment = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"""
            SELECT time, event_id, simulation_run_id, area_id, sensor_id, risk_level,
                   risk_score, has_explanation
            FROM risk_assessments
            WHERE event_id = '{envelope.EventId}'
            """);
        AssertString(riskAssessment, "event_id", envelope.EventId.ToString());
        AssertString(riskAssessment, "simulation_run_id", simulationRunId.ToString());
        AssertString(riskAssessment, "area_id", areaId.ToString());
        AssertString(riskAssessment, "sensor_id", sensorId.ToString());
        AssertString(riskAssessment, "risk_level", assessment.RiskLevel.ToString());
        AssertNumber(riskAssessment, "risk_score", assessment.RiskScore);
        AssertInteger(riskAssessment, "has_explanation", 1);
        AssertTimestamp(riskAssessment, assessment.Timestamp);

        var areaSnapshot = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"""
            SELECT time, event_id, simulation_run_id, area_id, aggregate_risk_level,
                   severity, aggregate_risk_score, assessment_count
            FROM area_risk_snapshots
            WHERE event_id = '{envelope.EventId}'
            """);
        AssertString(areaSnapshot, "event_id", envelope.EventId.ToString());
        AssertString(areaSnapshot, "simulation_run_id", simulationRunId.ToString());
        AssertString(areaSnapshot, "area_id", areaId.ToString());
        AssertString(areaSnapshot, "aggregate_risk_level", snapshot.AggregateRiskLevel.ToString());
        AssertString(areaSnapshot, "severity", SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel).ToString());
        AssertNumber(areaSnapshot, "aggregate_risk_score", snapshot.AggregateRiskScore);
        AssertInteger(areaSnapshot, "assessment_count", 1);
        AssertTimestamp(areaSnapshot, snapshot.Timestamp);

        await AssertMeasurementRowCountAsync(options, "accepted_readings", 1);
        await AssertMeasurementRowCountAsync(options, "risk_assessments", 1);
        await AssertMeasurementRowCountAsync(options, "area_risk_snapshots", 1);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task ReadingRiskPipeline_PersistsPostgresAndInfluxOutputs_OnRealServices()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        await using var influxDatabase = await TemporaryInfluxDatabase.CreateAsync();
        var options = influxDatabase.CreateOptions();
        var areaId = Guid.NewGuid();
        var gridCellId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var configurationVersionId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 5, 5, TimeSpan.Zero);
        await SeedControlPlaneAsync(
            database,
            configurationVersionId,
            areaId,
            gridCellId,
            sensorId,
            simulationRunId,
            timestamp);
        var dbContextFactory = database.CreateFactory();
        using var influxWriteService = new InfluxWriteService(
            Options.Create(options),
            NullLogger<InfluxWriteService>.Instance);
        var pipeline = new ReadingRiskPipeline(
            new PostgresAcceptedReadingRepository(
                dbContextFactory,
                NullLogger<PostgresAcceptedReadingRepository>.Instance),
            new RiskEligibilityService(),
            new PostgresDailyCellStateRepository(dbContextFactory),
            new SimpleRiskScoringService(),
            new PostgresRiskAssessmentRepository(
                dbContextFactory,
                NullLogger<PostgresRiskAssessmentRepository>.Instance),
            new AreaRiskSnapshotService(),
            new PostgresAreaRiskSnapshotRepository(
                dbContextFactory,
                NullLogger<PostgresAreaRiskSnapshotRepository>.Instance),
            new PostgresAreaOperationalProjectionStore(
                dbContextFactory,
                NullLogger<PostgresAreaOperationalProjectionStore>.Instance),
            influxWriteService,
            NullLogger<ReadingRiskPipeline>.Instance);
        var envelope = CreateEnvelope(areaId, sensorId, timestamp, simulationRunId);

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        await using var dbContext = database.CreateDbContext();
        Assert.Equal(1, await dbContext.AcceptedReadingLogs.CountAsync(entity => entity.EventId == envelope.EventId));
        Assert.Equal(1, await dbContext.RiskAssessmentLogs.CountAsync(entity => entity.SourceEventId == envelope.EventId));
        Assert.Equal(1, await dbContext.AreaRiskSnapshotLogs.CountAsync(entity => entity.AreaId == areaId));
        Assert.Equal(1, await dbContext.CellOperationalStates.CountAsync(entity => entity.GridCellId == gridCellId));
        Assert.Equal(1, await dbContext.AreaOperationalStates.CountAsync(entity => entity.AreaId == areaId));

        var persistedRiskAssessment = await dbContext.RiskAssessmentLogs.SingleAsync(
            entity => entity.SourceEventId == envelope.EventId);
        var persistedAreaSnapshot = await dbContext.AreaRiskSnapshotLogs.SingleAsync(
            entity => entity.Id == envelope.EventId);

        var acceptedReading = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"""
            SELECT time, event_id, simulation_run_id, area_id, sensor_id,
                   value, metric_type, unit, operational_state
            FROM accepted_readings
            WHERE event_id = '{envelope.EventId}'
            """);
        AssertString(acceptedReading, "event_id", envelope.EventId.ToString());
        AssertString(acceptedReading, "simulation_run_id", simulationRunId.ToString());
        AssertString(acceptedReading, "area_id", areaId.ToString());
        AssertString(acceptedReading, "sensor_id", sensorId.ToString());
        AssertString(acceptedReading, "metric_type", SensorMetricType.Temperature.ToString());
        AssertString(acceptedReading, "unit", MeasurementUnit.Celsius.ToString());
        AssertString(acceptedReading, "operational_state", SensorOperationalState.Nominal.ToString());
        AssertNumber(acceptedReading, "value", 34.2);
        AssertTimestamp(acceptedReading, timestamp);

        var riskAssessment = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"""
            SELECT time, event_id, simulation_run_id, area_id, sensor_id,
                   risk_level, risk_score
            FROM risk_assessments
            WHERE event_id = '{envelope.EventId}'
            """);
        AssertString(riskAssessment, "event_id", envelope.EventId.ToString());
        AssertString(riskAssessment, "simulation_run_id", simulationRunId.ToString());
        AssertString(riskAssessment, "area_id", areaId.ToString());
        AssertString(riskAssessment, "sensor_id", sensorId.ToString());
        AssertString(riskAssessment, "risk_level", persistedRiskAssessment.RiskLevel);
        AssertNumber(riskAssessment, "risk_score", persistedRiskAssessment.RiskScore);
        AssertTimestamp(riskAssessment, persistedRiskAssessment.Timestamp);

        var areaSnapshot = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"""
            SELECT time, event_id, simulation_run_id, area_id,
                   aggregate_risk_level, aggregate_risk_score, assessment_count
            FROM area_risk_snapshots
            WHERE event_id = '{envelope.EventId}'
            """);
        AssertString(areaSnapshot, "event_id", envelope.EventId.ToString());
        AssertString(areaSnapshot, "simulation_run_id", simulationRunId.ToString());
        AssertString(areaSnapshot, "area_id", areaId.ToString());
        AssertString(areaSnapshot, "aggregate_risk_level", persistedAreaSnapshot.AggregateRiskLevel);
        AssertNumber(areaSnapshot, "aggregate_risk_score", persistedAreaSnapshot.AggregateRiskScore);
        AssertInteger(areaSnapshot, "assessment_count", persistedAreaSnapshot.AssessmentCount);
        AssertTimestamp(areaSnapshot, persistedAreaSnapshot.SnapshotTimestamp);

        await AssertMeasurementRowCountAsync(options, "accepted_readings", 1);
        await AssertMeasurementRowCountAsync(options, "risk_assessments", 1);
        await AssertMeasurementRowCountAsync(options, "area_risk_snapshots", 1);
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task TemporaryInfluxDatabase_Dispose_RemovesDatabase()
    {
        var database = await TemporaryInfluxDatabase.CreateAsync();
        var name = database.Name;

        Assert.True(await database.ExistsAsync());

        await database.DisposeAsync();

        Assert.False(await TemporaryInfluxDatabase.DatabaseExistsAsync(name));
    }

    private static async Task<JsonElement> AssertEventuallyReturnsSingleRowAsync(
        NatureProtector.Infrastructure.Influx.Configuration.InfluxDbOptions options,
        string sql)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Token);

        Exception? lastException = null;

        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                var rows = await QueryRowsAsync(httpClient, options, sql);
                if (rows.Length == 1)
                {
                    return rows[0];
                }

                if (rows.Length > 1)
                {
                    throw new InvalidOperationException(
                        $"InfluxDB query returned {rows.Length} rows where exactly one was expected. SQL: {sql}");
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(
            $"InfluxDB query did not return one row for SQL: {sql}",
            lastException);
    }

    private static async Task<JsonElement[]> QueryRowsAsync(
        HttpClient httpClient,
        NatureProtector.Infrastructure.Influx.Configuration.InfluxDbOptions options,
        string sql)
    {
        var payload = JsonSerializer.Serialize(new
        {
            db = options.Bucket,
            q = sql
        });
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(
            new Uri(new Uri(options.Url), "/api/v3/query_sql"),
            content);
        var responseBody = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().Select(static row => row.Clone()).ToArray()
            : [];
    }

    private static async Task AssertMeasurementRowCountAsync(
        NatureProtector.Infrastructure.Influx.Configuration.InfluxDbOptions options,
        string measurement,
        long expectedRows)
    {
        var row = await AssertEventuallyReturnsSingleRowAsync(
            options,
            $"SELECT COUNT(*) AS total FROM {measurement}");

        AssertInteger(row, "total", expectedRows);
    }

    private static void AssertString(JsonElement row, string propertyName, string expected)
    {
        var property = GetProperty(row, propertyName);
        Assert.Equal(expected, property.GetString());
    }

    private static void AssertNumber(JsonElement row, string propertyName, double expected)
    {
        var property = GetProperty(row, propertyName);
        Assert.Equal(expected, property.GetDouble(), precision: 6);
    }

    private static void AssertInteger(JsonElement row, string propertyName, long expected)
    {
        var property = GetProperty(row, propertyName);
        var actual = property.ValueKind == JsonValueKind.String
            ? long.Parse(property.GetString()!, CultureInfo.InvariantCulture)
            : property.GetInt64();

        Assert.Equal(expected, actual);
    }

    private static void AssertTimestamp(JsonElement row, DateTimeOffset expected)
    {
        var value = GetProperty(row, "time").GetString()
            ?? throw new InvalidOperationException("InfluxDB row did not include a timestamp string.");
        var actual = DateTimeOffset
            .Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            .ToUniversalTime();

        Assert.Equal(expected.ToUniversalTime(), actual);
    }

    private static JsonElement GetProperty(JsonElement row, string propertyName)
    {
        Assert.True(
            row.TryGetProperty(propertyName, out var property),
            $"InfluxDB row did not include '{propertyName}'. Row: {row.GetRawText()}");

        return property;
    }

    private static async Task SeedControlPlaneAsync(
        TemporaryPostgresDatabase database,
        Guid configurationVersionId,
        Guid areaId,
        Guid gridCellId,
        Guid sensorId,
        Guid simulationRunId,
        DateTimeOffset timestamp)
    {
        var profileId = Guid.NewGuid();
        var scenarioId = Guid.NewGuid();
        await using var dbContext = database.CreateDbContext();
        dbContext.ConfigurationVersions.Add(new ConfigurationVersionRecord
        {
            Id = configurationVersionId,
            VersionNumber = 10_001,
            Description = "Docker integration configuration",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = "integration-test"
        });
        dbContext.Areas.Add(new AreaRecord
        {
            Id = areaId,
            ConfigurationVersionId = configurationVersionId,
            Code = $"IT-{areaId:N}"[..20],
            Name = "Integration Test Area",
            CountryCode = "PT"
        });
        dbContext.GridCells.Add(new GridCellRecord
        {
            Id = gridCellId,
            AreaId = areaId,
            ConfigurationVersionId = configurationVersionId,
            CellCode = $"CELL-{gridCellId:N}"[..20],
            CentroidLatitude = 39.8,
            CentroidLongitude = -7.9,
            LandCoverClass = "Matos",
            DominantForestType = "Florestas de pinheiro bravo",
            DominantFuelModel = "Matos",
            TreeCoverDensity = 0.55,
            StructuralHazard = "muito_alta",
            SlopeDegrees = 18.0,
            AspectDegrees = 180.0,
            AltitudeMeters = 420.0
        });
        dbContext.SensorProfiles.Add(new SensorProfileRecord
        {
            Id = profileId,
            ConfigurationVersionId = configurationVersionId,
            Name = "Integration temperature profile",
            SensorFamily = "meteorological"
        });
        dbContext.SensorNodes.Add(new SensorNodeRecord
        {
            Id = sensorId,
            AreaId = areaId,
            GridCellId = gridCellId,
            ProfileId = profileId,
            ConfigurationVersionId = configurationVersionId,
            Name = "Docker-Influx-Sensor",
            Type = SensorType.Temperature,
            Latitude = 39.8,
            Longitude = -7.9,
            AltitudeMeters = 420.0,
            IsActive = true,
            InstallationProfile = "integration"
        });
        dbContext.ScenarioDefinitions.Add(new ScenarioDefinitionRecord
        {
            Id = scenarioId,
            AreaId = areaId,
            ConfigurationVersionId = configurationVersionId,
            Code = $"SCN-{scenarioId:N}"[..20],
            Name = "Integration Test Scenario",
            ScenarioKind = ScenarioCategory.HighRisk,
            Description = "Docker integration scenario",
            ParametersJson = "{}"
        });
        dbContext.SimulationRuns.Add(new SimulationRunRecord
        {
            Id = simulationRunId,
            AreaId = areaId,
            ScenarioId = scenarioId,
            ConfigurationVersionId = configurationVersionId,
            ScenarioCode = $"SCN-{scenarioId:N}"[..20],
            ScenarioName = "Integration Test Scenario",
            CreatedAt = timestamp.AddMinutes(-1),
            StartedAt = timestamp.AddSeconds(-30),
            LogicalStartTimestamp = timestamp,
            IntervalSeconds = 60,
            NumberOfCycles = 1,
            ExecutionSeed = 42,
            Status = SimulationRunStatus.Running,
            MetadataJson = "{\"source\":\"docker-integration\"}"
        });

        await dbContext.SaveChangesAsync();
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(
        Guid areaId,
        Guid sensorId,
        DateTimeOffset timestamp,
        Guid? simulationRunId = null)
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "v1",
            EventId: Guid.NewGuid(),
            CorrelationId: $"docker-influx-{Guid.NewGuid():N}",
            Producer: "NatureProtector.IntegrationTests",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: areaId,
            EventTime: timestamp,
            IngestTime: timestamp.AddSeconds(1),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: simulationRunId ?? Guid.NewGuid(),
                SensorId: sensorId,
                SensorName: "Docker-Influx-Sensor",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 34.2,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private static RiskAssessment CreateAssessment(DateTimeOffset timestamp)
    {
        return new RiskAssessment(
            Guid.NewGuid(),
            timestamp,
            0.72,
            "Docker integration candidate score.");
    }

    private static AreaRiskSnapshot CreateSnapshot(DateTimeOffset timestamp)
    {
        return new AreaRiskSnapshot(
            Guid.NewGuid(),
            timestamp,
            0.72,
            "Docker integration aggregate.");
    }
}
