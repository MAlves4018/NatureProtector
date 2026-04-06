using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.Fakes;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class ReadingRiskPipelineTests
{
    [Fact]
    public async Task ProcessAcceptedReadingAsync_Throws_WhenEnvelopeIsNull()
    {
        var pipeline = CreatePipeline(
            acceptedReadingRepository: new InMemoryAcceptedReadingRepository(),
            riskAssessmentRepository: new InMemoryRiskAssessmentRepository(),
            areaRiskSnapshotRepository: new InMemoryAreaRiskSnapshotRepository(),
            influxWriteService: new FakeInfluxWriteService());

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.ProcessAcceptedReadingAsync(
            envelope: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("envelope", ex.ParamName);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_PersistsAcceptedReading_Assessment_AndSnapshot()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            influxWriteService);
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 36.0,
            eventTime: new DateTimeOffset(2026, 4, 6, 18, 15, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        var storedAccepted = await acceptedReadingRepository.GetAllAsync(CancellationToken.None);
        var storedAssessments = await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None);
        var storedSnapshot = await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None);

        var accepted = Assert.Single(storedAccepted);
        Assert.Equal(envelope.EventId, accepted.EventId);

        var assessment = Assert.Single(storedAssessments);
        Assert.Equal(envelope.EventTime, assessment.Timestamp);
        Assert.Equal(envelope.Payload.SensorId, influxWriteService.RiskAssessments.Single().SensorId);

        Assert.NotNull(storedSnapshot);
        Assert.Equal(envelope.EventTime, storedSnapshot!.Timestamp);
        Assert.Equal(storedSnapshot.AggregateRiskScore, influxWriteService.AreaSnapshots.Single().Snapshot.AggregateRiskScore);

        Assert.Single(influxWriteService.AcceptedReadings);
        Assert.Single(influxWriteService.RiskAssessments);
        Assert.Single(influxWriteService.AreaSnapshots);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_AggregatesMultipleReadings_FromSameArea()
    {
        var areaId = Guid.NewGuid();
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            influxWriteService);
        var first = EnvelopeFactory.Create(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 22.0,
            eventTime: new DateTimeOffset(2026, 4, 6, 18, 0, 0, TimeSpan.Zero));
        var second = EnvelopeFactory.Create(
            areaId: areaId,
            sensorId: Guid.NewGuid(),
            metricType: SensorMetricType.WindSpeed,
            unit: MeasurementUnit.MetersPerSecond,
            value: 18.0,
            eventTime: new DateTimeOffset(2026, 4, 6, 18, 5, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(first, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(second, CancellationToken.None);

        var assessments = await riskAssessmentRepository.GetByAreaAsync(areaId, CancellationToken.None);
        var snapshot = await areaRiskSnapshotRepository.GetLatestAsync(areaId, CancellationToken.None);

        Assert.Equal(2, assessments.Count);
        Assert.NotNull(snapshot);
        Assert.Equal(second.EventTime, snapshot!.Timestamp);
        Assert.Equal(2, influxWriteService.AreaSnapshots.Last().AssessmentCount);
        Assert.Equal(2, influxWriteService.RiskAssessments.Count);
        Assert.Equal(2, influxWriteService.AcceptedReadings.Count);
    }

    private static ReadingRiskPipeline CreatePipeline(
        IAcceptedReadingRepository acceptedReadingRepository,
        IRiskAssessmentRepository riskAssessmentRepository,
        IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
        FakeInfluxWriteService influxWriteService)
    {
        return new ReadingRiskPipeline(
            acceptedReadingRepository,
            new SimpleRiskScoringService(),
            riskAssessmentRepository,
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository,
            influxWriteService,
            NullLogger<ReadingRiskPipeline>.Instance);
    }
}
