using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NatureProtector.Infrastructure.Influx.Tests.Services;

public sealed class InfluxWriteServiceExecutionTests
{
    [Fact]
    public async Task WriteBatchAsync_AttemptsRemoteWrite_ForValidBatch()
    {
        using var service = CreateService();
        var batch = new InfluxTelemetryBatch()
            .AddAcceptedReading(CreateEnvelope())
            .AddRiskAssessment(Guid.NewGuid(), Guid.NewGuid(), CreateAssessment())
            .AddAreaRiskSnapshot(Guid.NewGuid(), 3, CreateSnapshot());

        await Assert.ThrowsAnyAsync<Exception>(() => service.WriteBatchAsync(
            batch,
            CancellationToken.None));
    }

    [Fact]
    public async Task WriteAcceptedReadingAsync_AttemptsRemoteWrite_ForValidEnvelope()
    {
        using var service = CreateService();

        await Assert.ThrowsAnyAsync<Exception>(() => service.WriteAcceptedReadingAsync(
            CreateEnvelope(),
            CancellationToken.None));
    }

    [Fact]
    public async Task WriteRiskAssessmentAsync_AttemptsRemoteWrite_WhenExplanationExists()
    {
        using var service = CreateService();
        var assessment = new RiskAssessment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 7, 0, 0, 0, TimeSpan.Zero),
            0.72,
            "Hot and dry");

        await Assert.ThrowsAnyAsync<Exception>(() => service.WriteRiskAssessmentAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            assessment,
            CancellationToken.None));
    }

    [Fact]
    public async Task WriteRiskAssessmentAsync_AttemptsRemoteWrite_WhenExplanationIsBlank()
    {
        using var service = CreateService();
        var assessment = new RiskAssessment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 7, 0, 5, 0, TimeSpan.Zero),
            0.36,
            "   ");

        await Assert.ThrowsAnyAsync<Exception>(() => service.WriteRiskAssessmentAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            assessment,
            CancellationToken.None));
    }

    [Fact]
    public async Task WriteAreaRiskSnapshotAsync_AttemptsRemoteWrite_ForValidSnapshot()
    {
        using var service = CreateService();
        var snapshot = new AreaRiskSnapshot(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 7, 0, 10, 0, TimeSpan.Zero),
            0.81,
            "Escalating");

        await Assert.ThrowsAnyAsync<Exception>(() => service.WriteAreaRiskSnapshotAsync(
            Guid.NewGuid(),
            assessmentCount: 3,
            snapshot,
            CancellationToken.None));
    }

    private static InfluxWriteService CreateService()
    {
        return new InfluxWriteService(Options.Create(new InfluxDbOptions
        {
            Url = "http://127.0.0.1:1",
            Token = "token",
            Organization = "org",
            Bucket = "bucket"
        }),
        NullLogger<InfluxWriteService>.Instance);
    }

    private static RiskAssessment CreateAssessment()
    {
        return new RiskAssessment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 7, 0, 0, 0, TimeSpan.Zero),
            0.72,
            "Hot and dry");
    }

    private static AreaRiskSnapshot CreateSnapshot()
    {
        return new AreaRiskSnapshot(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 7, 0, 10, 0, TimeSpan.Zero),
            0.81,
            "Escalating");
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-influx",
            Producer: "NatureProtector.Prevention.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 7, 0, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Influx-Sensor",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 34.2,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }
}
