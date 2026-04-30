using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Infrastructure.Influx.Tests.Services;

public sealed class NoOpInfluxWriteServiceTests
{
    [Fact]
    public async Task AllMethods_DoNotThrow_WhenCancellationTokenIsActive()
    {
        var service = CreateService();
        var envelope = CreateEnvelope();
        var assessment = CreateAssessment();
        var snapshot = CreateSnapshot();
        var batch = new InfluxTelemetryBatch()
            .AddAcceptedReading(envelope)
            .AddRiskAssessment(Guid.NewGuid(), Guid.NewGuid(), assessment)
            .AddAreaRiskSnapshot(Guid.NewGuid(), 2, snapshot);

        await service.WriteBatchAsync(batch, CancellationToken.None);
        await service.WriteAcceptedReadingAsync(envelope, CancellationToken.None);
        await service.WriteRiskAssessmentAsync(Guid.NewGuid(), Guid.NewGuid(), assessment, CancellationToken.None);
        await service.WriteAreaRiskSnapshotAsync(Guid.NewGuid(), 2, snapshot, CancellationToken.None);
    }

    [Fact]
    public async Task WriteBatchAsync_ThrowsOperationCanceledException_WhenCancellationIsRequested()
    {
        var service = CreateService();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(CreateEnvelope()),
            cancellationTokenSource.Token));
    }

    private static NoOpInfluxWriteService CreateService()
    {
        return new NoOpInfluxWriteService(
            Options.Create(new InfluxDbOptions
            {
                Enabled = false
            }),
            NullLogger<NoOpInfluxWriteService>.Instance);
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-noop",
            Producer: "NatureProtector.Prevention.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor-NoOp",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 30.1,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private static RiskAssessment CreateAssessment()
    {
        return new RiskAssessment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 29, 10, 5, 0, TimeSpan.Zero),
            0.72,
            "Risk assessment for no-op writer.");
    }

    private static AreaRiskSnapshot CreateSnapshot()
    {
        return new AreaRiskSnapshot(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 29, 10, 10, 0, TimeSpan.Zero),
            0.81,
            "Snapshot for no-op writer.");
    }
}
