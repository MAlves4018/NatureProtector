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
    public async Task WriteAcceptedReadingAsync_DoesNotRethrow_WhenFailureIsTolerated()
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

        Assert.Equal(1, inner.AcceptedReadingCalls);
    }

    [Fact]
    public async Task WriteAcceptedReadingAsync_Rethrows_WhenFailureIsStrict()
    {
        var inner = new ThrowingInfluxWriteService();
        var service = CreateService(
            inner,
            new InfluxDbOptions
            {
                Enabled = true,
                FailPipelineOnWriteError = true
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteAcceptedReadingAsync(
            CreateEnvelope(),
            CancellationToken.None));
    }

    [Fact]
    public async Task WriteAcceptedReadingAsync_DoesNotCallInner_WhenAcceptedReadingsAreDisabled()
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

        await service.WriteAcceptedReadingAsync(CreateEnvelope(), CancellationToken.None);

        Assert.Equal(0, inner.AcceptedReadingCalls);
    }

    [Fact]
    public async Task WriteRiskAssessmentAsync_DoesNotCallInner_WhenRiskAssessmentsAreDisabled()
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

        await service.WriteRiskAssessmentAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateAssessment(),
            CancellationToken.None);

        Assert.Equal(0, inner.RiskAssessmentCalls);
    }

    [Fact]
    public async Task WriteAreaRiskSnapshotAsync_DoesNotCallInner_WhenAreaRiskSnapshotsAreDisabled()
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

        await service.WriteAreaRiskSnapshotAsync(
            Guid.NewGuid(),
            2,
            CreateSnapshot(),
            CancellationToken.None);

        Assert.Equal(0, inner.AreaRiskSnapshotCalls);
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

        public Task WriteAcceptedReadingAsync(EventEnvelope<SensorReadingProducedPayload> envelope, CancellationToken cancellationToken)
        {
            AcceptedReadingCalls++;
            throw new InvalidOperationException("Simulated InfluxDB failure.");
        }

        public Task WriteRiskAssessmentAsync(Guid areaId, Guid sensorId, RiskAssessment assessment, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated InfluxDB failure.");
        }

        public Task WriteAreaRiskSnapshotAsync(Guid areaId, int assessmentCount, AreaRiskSnapshot snapshot, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated InfluxDB failure.");
        }
    }
}
