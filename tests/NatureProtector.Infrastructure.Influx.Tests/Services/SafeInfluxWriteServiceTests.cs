using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Infrastructure.Influx.Tests.Services;

public sealed class SafeInfluxWriteServiceTests
{
    [Fact]
    public async Task WriteAcceptedReadingAsync_ValidEnvelope_ForwardsAcceptedReadingBatch()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(inner, new InfluxDbOptions { Enabled = true });
        var envelope = CreateEnvelope();

        await service.WriteAcceptedReadingAsync(envelope, CancellationToken.None);

        var batch = Assert.Single(inner.Batches);
        Assert.Equal(1, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.RiskAssessmentCount);
        Assert.Equal(0, batch.AreaRiskSnapshotCount);
        Assert.Same(envelope, Assert.Single(batch.AcceptedReadings));
    }

    [Fact]
    public async Task WriteRiskAssessmentAsync_ValidAssessment_ForwardsRiskAssessmentBatch()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(inner, new InfluxDbOptions { Enabled = true });
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var assessment = CreateAssessment();

        await service.WriteRiskAssessmentAsync(areaId, sensorId, assessment, CancellationToken.None);

        var batch = Assert.Single(inner.Batches);
        var write = Assert.Single(batch.RiskAssessments);
        Assert.Equal(areaId, write.AreaId);
        Assert.Equal(sensorId, write.SensorId);
        Assert.Same(assessment, write.Assessment);
        Assert.Equal(0, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.AreaRiskSnapshotCount);
    }

    [Fact]
    public async Task WriteAreaRiskSnapshotAsync_ValidSnapshot_ForwardsAreaRiskSnapshotBatch()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(inner, new InfluxDbOptions { Enabled = true });
        var areaId = Guid.NewGuid();
        var snapshot = CreateSnapshot();

        await service.WriteAreaRiskSnapshotAsync(areaId, 7, snapshot, CancellationToken.None);

        var batch = Assert.Single(inner.Batches);
        var write = Assert.Single(batch.AreaRiskSnapshots);
        Assert.Equal(areaId, write.AreaId);
        Assert.Equal(7, write.AssessmentCount);
        Assert.Same(snapshot, write.Snapshot);
        Assert.Equal(0, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.RiskAssessmentCount);
    }

    [Fact]
    public async Task WriteAcceptedReadingAsync_NullEnvelope_ThrowsArgumentNullException()
    {
        var service = CreateService(
            new RecordingInfluxWriteService(),
            new InfluxDbOptions { Enabled = true });

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.WriteAcceptedReadingAsync(null!, CancellationToken.None));

        Assert.Equal("envelope", exception.ParamName);
    }

    [Fact]
    public async Task WriteRiskAssessmentAsync_NullAssessment_ThrowsArgumentNullException()
    {
        var service = CreateService(
            new RecordingInfluxWriteService(),
            new InfluxDbOptions { Enabled = true });

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.WriteRiskAssessmentAsync(Guid.NewGuid(), Guid.NewGuid(), null!, CancellationToken.None));

        Assert.Equal("assessment", exception.ParamName);
    }

    [Fact]
    public async Task WriteAreaRiskSnapshotAsync_NullSnapshot_ThrowsArgumentNullException()
    {
        var service = CreateService(
            new RecordingInfluxWriteService(),
            new InfluxDbOptions { Enabled = true });

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.WriteAreaRiskSnapshotAsync(Guid.NewGuid(), 1, null!, CancellationToken.None));

        Assert.Equal("snapshot", exception.ParamName);
    }

    [Fact]
    public async Task WriteAcceptedReadingAsync_InnerThrowsAndFailureIsTolerated_DoesNotRethrow()
    {
        var inner = new ThrowingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                FailPipelineOnWriteError = false
            });

        await service.WriteAcceptedReadingAsync(CreateEnvelope(), CancellationToken.None);

        Assert.Equal(1, inner.BatchCalls);
    }

    [Fact]
    public async Task WriteBatchAsync_DoesNotRethrow_WhenFailureIsTolerated()
    {
        var inner = new ThrowingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                FailPipelineOnWriteError = false
            });

        await service.WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(CreateEnvelope()),
            CancellationToken.None);

        Assert.Equal(1, inner.BatchCalls);
    }

    [Fact]
    public async Task WriteBatchAsync_Rethrows_WhenFailureIsStrict()
    {
        var inner = new ThrowingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                FailPipelineOnWriteError = true
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(CreateEnvelope()),
            CancellationToken.None));
    }

    [Fact]
    public async Task WriteBatchAsync_DoesNotCallInner_WhenBatchIsEmpty()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true
            });

        await service.WriteBatchAsync(new InfluxTelemetryBatch(), CancellationToken.None);

        Assert.Equal(0, inner.BatchCalls);
    }

    [Fact]
    public async Task WriteBatchAsync_RespectsAcceptedReadingsFlag()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                Writes = new InfluxWriteOptions
                {
                    AcceptedReadings = false
                }
            });

        await service.WriteBatchAsync(
            new InfluxTelemetryBatch().AddAcceptedReading(CreateEnvelope()),
            CancellationToken.None);

        Assert.Equal(0, inner.BatchCalls);
    }

    [Fact]
    public async Task WriteBatchAsync_RespectsRiskAssessmentsFlag()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                Writes = new InfluxWriteOptions
                {
                    RiskAssessments = false
                }
            });

        await service.WriteBatchAsync(
            new InfluxTelemetryBatch().AddRiskAssessment(
                Guid.NewGuid(),
                Guid.NewGuid(),
                CreateAssessment()),
            CancellationToken.None);

        Assert.Equal(0, inner.BatchCalls);
    }

    [Fact]
    public async Task WriteBatchAsync_RespectsAreaRiskSnapshotsFlag()
    {
        var inner = new RecordingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                Writes = new InfluxWriteOptions
                {
                    AreaRiskSnapshots = false
                }
            });

        await service.WriteBatchAsync(
            new InfluxTelemetryBatch().AddAreaRiskSnapshot(
                Guid.NewGuid(),
                2,
                CreateSnapshot()),
            CancellationToken.None);

        Assert.Equal(0, inner.BatchCalls);
    }

    private static SafeInfluxWriteService CreateService(IInfluxWriteService inner, InfluxDbOptions options)
    {
        return new SafeInfluxWriteService(
            () => inner,
            Options.Create(options),
            NullLogger<SafeInfluxWriteService>.Instance);
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-safe",
            Producer: "NatureProtector.Prevention.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 29, 10, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor-Safe",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 31.5,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private static RiskAssessment CreateAssessment()
    {
        return new RiskAssessment(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 29, 10, 5, 0, TimeSpan.Zero),
            0.62,
            "Risk assessment for safe writer.");
    }

    private static AreaRiskSnapshot CreateSnapshot()
    {
        return new AreaRiskSnapshot(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 4, 29, 10, 10, 0, TimeSpan.Zero),
            0.74,
            "Snapshot for safe writer.");
    }

    private sealed class RecordingInfluxWriteService : IInfluxWriteService
    {
        public int AcceptedReadingCalls { get; private set; }
        public int RiskAssessmentCalls { get; private set; }
        public int AreaRiskSnapshotCalls { get; private set; }
        public int BatchCalls { get; private set; }
        public List<InfluxTelemetryBatch> Batches { get; } = [];

        public Task WriteBatchAsync(InfluxTelemetryBatch batch, CancellationToken cancellationToken)
        {
            BatchCalls++;
            Batches.Add(batch);
            return Task.CompletedTask;
        }

        public Task WriteAcceptedReadingAsync(EventEnvelope<SensorReadingProducedPayload> envelope, CancellationToken cancellationToken)
        {
            AcceptedReadingCalls++;
            return Task.CompletedTask;
        }

        public Task WriteRiskAssessmentAsync(Guid areaId, Guid sensorId, RiskAssessment assessment, CancellationToken cancellationToken)
        {
            RiskAssessmentCalls++;
            return Task.CompletedTask;
        }

        public Task WriteAreaRiskSnapshotAsync(Guid areaId, int assessmentCount, AreaRiskSnapshot snapshot, CancellationToken cancellationToken)
        {
            AreaRiskSnapshotCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingInfluxWriteService : IInfluxWriteService
    {
        public int AcceptedReadingCalls { get; private set; }
        public int BatchCalls { get; private set; }

        public Task WriteBatchAsync(InfluxTelemetryBatch batch, CancellationToken cancellationToken)
        {
            BatchCalls++;
            throw new InvalidOperationException("Simulated InfluxDB failure.");
        }

        public Task WriteAcceptedReadingAsync(EventEnvelope<SensorReadingProducedPayload> envelope, CancellationToken cancellationToken)
        {
            AcceptedReadingCalls++;
            return WriteBatchAsync(
                new InfluxTelemetryBatch().AddAcceptedReading(envelope),
                cancellationToken);
        }

        public Task WriteRiskAssessmentAsync(Guid areaId, Guid sensorId, RiskAssessment assessment, CancellationToken cancellationToken)
        {
            return WriteBatchAsync(
                new InfluxTelemetryBatch().AddRiskAssessment(areaId, sensorId, assessment),
                cancellationToken);
        }

        public Task WriteAreaRiskSnapshotAsync(Guid areaId, int assessmentCount, AreaRiskSnapshot snapshot, CancellationToken cancellationToken)
        {
            return WriteBatchAsync(
                new InfluxTelemetryBatch().AddAreaRiskSnapshot(areaId, assessmentCount, snapshot),
                cancellationToken);
        }
    }
}
