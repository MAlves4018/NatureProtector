using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.Fakes;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Readings;
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
            projectionStore: new InMemoryAreaOperationalProjectionStore(),
            influxWriteService: new FakeInfluxWriteService(),
            riskEligibilityService: new RiskEligibilityService());

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
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService());
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
        Assert.Equal(envelope.EventId, storedSnapshot!.Id);
        Assert.Equal(envelope.EventTime, storedSnapshot!.Timestamp);
        Assert.Equal(storedSnapshot.AggregateRiskScore, influxWriteService.AreaSnapshots.Single().Snapshot.AggregateRiskScore);
        var projection = Assert.Single(projectionStore.States);
        Assert.Equal(envelope.AreaId, projection.AreaId);
        Assert.Equal(storedSnapshot.AggregateRiskLevel.ToString(), projection.AggregateRiskLevel);
        var cellState = Assert.Single(projectionStore.CellStates);
        Assert.Equal(envelope.Payload.SensorId, cellState.SensorId);
        Assert.Equal(assessment.RiskLevel.ToString(), cellState.RiskLevel);

        Assert.Single(influxWriteService.AcceptedReadings);
        Assert.Single(influxWriteService.RiskAssessments);
        Assert.Single(influxWriteService.AreaSnapshots);
        Assert.Single(influxWriteService.Batches);
        Assert.Equal(3, influxWriteService.Batches.Single().PointCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_AggregatesMultipleReadings_FromSameArea()
    {
        var areaId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService());
        var first = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 22.0,
            eventTime: new DateTimeOffset(2026, 4, 6, 18, 0, 0, TimeSpan.Zero));
        var second = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
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
        Assert.Equal(2, influxWriteService.Batches.Count);
        var projection = Assert.Single(projectionStore.States);
        Assert.Equal(2, projection.AssessmentCount);
        Assert.Equal(2, projectionStore.CellStates.Count);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_UsesLatestAssessmentPerSensor_ForAreaSnapshot()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService());
        var first = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 21.0,
            eventTime: new DateTimeOffset(2026, 4, 6, 18, 0, 0, TimeSpan.Zero));
        var second = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 41.0,
            eventTime: new DateTimeOffset(2026, 4, 6, 18, 5, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(first, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(second, CancellationToken.None);

        var allAssessments = await riskAssessmentRepository.GetByAreaAsync(areaId, CancellationToken.None);
        var latestAssessments = await riskAssessmentRepository.GetLatestByAreaAsync(areaId, CancellationToken.None);
        var snapshot = await areaRiskSnapshotRepository.GetLatestAsync(areaId, CancellationToken.None);

        Assert.Equal(2, allAssessments.Count);
        var latestAssessment = Assert.Single(latestAssessments);
        Assert.NotNull(snapshot);
        Assert.Equal(latestAssessment.RiskScore, snapshot!.AggregateRiskScore, precision: 3);
        Assert.Equal(second.EventTime, snapshot.Timestamp);
        Assert.Equal(1, influxWriteService.AreaSnapshots.Last().AssessmentCount);
        Assert.Equal(2, influxWriteService.Batches.Count);
        var projection = Assert.Single(projectionStore.States);
        Assert.Equal(1, projection.AssessmentCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_Completes_WhenInfluxFailureIsTolerated()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var throwingInner = new ThrowingInfluxWriteService();
        var safeInfluxWriteService = new SafeInfluxWriteService(
            () => throwingInner,
            Options.Create(new InfluxDbOptions
            {
                Enabled = true,
                FailPipelineOnWriteError = false
            }),
            NullLogger<SafeInfluxWriteService>.Instance);
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            safeInfluxWriteService,
            new RiskEligibilityService());
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 33.5,
            eventTime: new DateTimeOffset(2026, 4, 29, 11, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Single(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.NotNull(await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        Assert.Single(projectionStore.States);
        Assert.Single(projectionStore.CellStates);
        Assert.Equal(1, throwingInner.BatchCalls);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_PersistsOperationalState_WhenInfluxIsDisabled()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var noOpInfluxWriteService = new NoOpInfluxWriteService(
            Options.Create(new InfluxDbOptions
            {
                Enabled = false
            }),
            NullLogger<NoOpInfluxWriteService>.Instance);
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            noOpInfluxWriteService,
            new RiskEligibilityService());
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Humidity,
            unit: MeasurementUnit.Percent,
            value: 18.0,
            eventTime: new DateTimeOffset(2026, 4, 30, 9, 30, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Single(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.NotNull(await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        Assert.Single(projectionStore.States);
        Assert.Single(projectionStore.CellStates);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_UsesSingleInfluxBatchPerEvent()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService());
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 32.2,
            eventTime: new DateTimeOffset(2026, 4, 29, 11, 15, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        var batch = Assert.Single(influxWriteService.Batches);
        Assert.Equal(1, batch.AcceptedReadingCount);
        Assert.Equal(1, batch.RiskAssessmentCount);
        Assert.Equal(1, batch.AreaRiskSnapshotCount);
        Assert.Equal(3, batch.PointCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_EvaluatesEligibilityBeforeScoring()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var callSequence = new List<string>();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new TrackingRiskEligibilityService(callSequence),
            new TrackingRiskScoringService(callSequence));
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 31.0,
            eventTime: new DateTimeOffset(2026, 4, 30, 14, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Equal(["eligibility", "scoring"], callSequence);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_PassesEligibilityMetadataToRiskInput()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new CapturingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new PartialEligibleRiskEligibilityService(),
            scoringService);
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.WindSpeed,
            unit: MeasurementUnit.MetersPerSecond,
            value: 12.0,
            eventTime: new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.NotNull(scoringService.LastInput);
        Assert.Equal(RiskInputStatus.PartialButUsable, scoringService.LastInput!.InputStatus);
        Assert.Equal(RiskEligibilityReason.DelayedReading, scoringService.LastInput.EligibilityReason);
        Assert.Equal(ObservationalConfidenceLevel.Low, scoringService.LastInput.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Compromised, scoringService.LastInput.OperationalIntegrity);
        Assert.Contains("Delayed", scoringService.LastInput.QualityFlags);
        Assert.Contains(RiskInput.LowCoverageFlag, scoringService.LastInput.QualityFlags);
        var carried = Assert.Single(scoringService.LastInput.ClassifierResults);
        Assert.Equal("temporal_classifier", carried.ClassifierName);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_AttachesDailyCellStateContext_WhenAvailable()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new CapturingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService(),
            scoringService);
        var first = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 28.0,
            eventTime: new DateTimeOffset(2026, 5, 18, 8, 0, 0, TimeSpan.Zero));
        var second = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 33.0,
            eventTime: new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(first, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(second, CancellationToken.None);

        Assert.Equal(2, scoringService.Inputs.Count);
        Assert.Equal(DailyCellStateStatus.Present, scoringService.Inputs[0].DailyCellStateStatus);
        Assert.DoesNotContain(RiskInput.MissingDailyCellStateFlag, scoringService.Inputs[0].QualityFlags);
        Assert.NotNull(scoringService.Inputs[0].DailyCellState);
        Assert.Equal(28.0, scoringService.Inputs[0].DailyCellState!.MaxTemperatureCelsius);
        Assert.Equal(DailyCellStateStatus.Present, scoringService.Inputs[1].DailyCellStateStatus);
        Assert.NotNull(scoringService.Inputs[1].DailyCellState);
        Assert.Equal(33.0, scoringService.Inputs[1].DailyCellState!.MaxTemperatureCelsius);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_PreservesDelayedEligibility_WhenDailyMetricsAreComplete()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new CapturingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService(),
            scoringService);
        var start = new DateTimeOffset(2026, 5, 18, 8, 0, 0, TimeSpan.Zero);
        var temperature = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 30.0,
            eventTime: start);
        var humidity = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Humidity,
            unit: MeasurementUnit.Percent,
            value: 35.0,
            eventTime: start.AddMinutes(1));
        var wind = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.WindSpeed,
            unit: MeasurementUnit.MetersPerSecond,
            value: 7.0,
            eventTime: start.AddMinutes(2));
        var delayed = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: simulationRunId,
            sensorId: sensorId,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 34.2,
            operationalState: SensorOperationalState.Delayed,
            eventTime: start.AddMinutes(3));

        await pipeline.ProcessAcceptedReadingAsync(temperature, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(humidity, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(wind, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(delayed, CancellationToken.None);

        Assert.Equal(4, scoringService.Inputs.Count);
        var delayedInput = scoringService.Inputs[^1];
        Assert.True(delayedInput.Metrics.IsCompleteV1);
        Assert.Equal(RiskInputStatus.PartialButUsable, delayedInput.InputStatus);
        Assert.Equal(RiskEligibilityReason.DelayedReading, delayedInput.EligibilityReason);
        Assert.Contains("Delayed", delayedInput.QualityFlags);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_CompletesWithoutRiskArtifacts_WhenReadingIsNotEligible()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new ThrowingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new NotEligibleRiskEligibilityService(),
            scoringService);
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.WindDirection,
            unit: MeasurementUnit.Degrees,
            value: 180.0,
            eventTime: new DateTimeOffset(2026, 4, 30, 14, 30, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Null(await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        Assert.Empty(projectionStore.States);
        Assert.Empty(projectionStore.CellStates);
        Assert.Equal(0, scoringService.CallCount);
        var batch = Assert.Single(influxWriteService.Batches);
        Assert.Equal(1, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.RiskAssessmentCount);
        Assert.Equal(0, batch.AreaRiskSnapshotCount);
        Assert.Single(influxWriteService.AcceptedReadings);
        Assert.Empty(influxWriteService.RiskAssessments);
        Assert.Empty(influxWriteService.AreaSnapshots);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_SameAreaDifferentRuns_DoesNotMixSnapshotAssessments()
    {
        var areaId = Guid.NewGuid();
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService());
        var firstRunId = Guid.NewGuid();
        var secondRunId = Guid.NewGuid();
        var firstRunReading = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: firstRunId,
            sensorId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 22.0,
            eventTime: new DateTimeOffset(2026, 5, 14, 8, 0, 0, TimeSpan.Zero));
        var secondRunReading = EnvelopeFactory.Create(
            areaId: areaId,
            simulationRunId: secondRunId,
            sensorId: Guid.NewGuid(),
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 41.0,
            eventTime: new DateTimeOffset(2026, 5, 14, 9, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(firstRunReading, CancellationToken.None);
        await pipeline.ProcessAcceptedReadingAsync(secondRunReading, CancellationToken.None);

        var firstRunLatest = await riskAssessmentRepository.GetLatestByAreaAsync(
            areaId,
            CancellationToken.None,
            firstRunId);
        var secondRunSnapshot = await areaRiskSnapshotRepository.GetLatestAsync(
            areaId,
            CancellationToken.None,
            secondRunId);

        var firstRunAssessment = Assert.Single(firstRunLatest);
        Assert.NotNull(secondRunSnapshot);
        Assert.Equal(1, influxWriteService.AreaSnapshots.Last().AssessmentCount);
        Assert.NotEqual(firstRunAssessment.RiskScore, secondRunSnapshot!.AggregateRiskScore);
        Assert.Equal(1, projectionStore.States.Single().AssessmentCount);
        Assert.Equal(secondRunId, projectionStore.States.Single().SimulationRunId);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_DroppedReading_DoesNotCreateRiskAssessment()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new ThrowingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService(),
            scoringService);
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 31.0,
            operationalState: SensorOperationalState.Dropped,
            eventTime: new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Null(await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        Assert.Empty(projectionStore.States);
        Assert.Empty(projectionStore.CellStates);
        Assert.Equal(0, scoringService.CallCount);
        var batch = Assert.Single(influxWriteService.Batches);
        Assert.Equal(1, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.RiskAssessmentCount);
        Assert.Equal(0, batch.AreaRiskSnapshotCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_UnsupportedMetric_DoesNotCreateRiskAssessment()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new ThrowingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new RiskEligibilityService(),
            scoringService);
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.WindDirection,
            unit: MeasurementUnit.Degrees,
            value: 180.0,
            eventTime: new DateTimeOffset(2026, 5, 13, 12, 5, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Null(await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        Assert.Empty(projectionStore.States);
        Assert.Empty(projectionStore.CellStates);
        Assert.Equal(0, scoringService.CallCount);
        var batch = Assert.Single(influxWriteService.Batches);
        Assert.Equal(1, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.RiskAssessmentCount);
        Assert.Equal(0, batch.AreaRiskSnapshotCount);
    }

    [Fact]
    public async Task Blocked_DoesNotCreateNumericRiskAssessment()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var projectionStore = new InMemoryAreaOperationalProjectionStore();
        var influxWriteService = new FakeInfluxWriteService();
        var scoringService = new ThrowingRiskScoringService();
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            new BlockedRiskEligibilityService(),
            scoringService);
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 22.0,
            eventTime: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero));

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Empty(await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None));
        Assert.Null(await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None));
        Assert.Equal(0, scoringService.CallCount);
        var batch = Assert.Single(influxWriteService.Batches);
        Assert.Equal(1, batch.AcceptedReadingCount);
        Assert.Equal(0, batch.RiskAssessmentCount);
        Assert.Equal(0, batch.AreaRiskSnapshotCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_TemporalEligibleCycle_ProjectsOnlyFinalizedCycleScope()
    {
        var areaId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var projectedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var alertedAt = projectedAt.AddMilliseconds(20);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new RecordingRiskAssessmentRepository();
        var projectionStore = new RecordingProjectionStore(new AreaProjectionWriteResult(projectedAt, alertedAt));
        var influxWriteService = new FakeInfluxWriteService();
        var coordinator = new RecordingCycleProjectionCoordinator((_, assessment) =>
        [
            new FinalizedCycleProjection(
                runId,
                CycleIndex: 3,
                areaId,
                new AreaRiskSnapshot(eventId, projectedAt, assessment!.RiskScore, "finalized temporal cycle"),
                EligibleCount: 1,
                IsOperational: true,
                EligibleEventIds: [eventId])
        ]);
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            new InMemoryAreaRiskSnapshotRepository(),
            projectionStore,
            influxWriteService,
            new RiskEligibilityService(),
            cycleProjectionCoordinator: coordinator);
        var envelope = EnvelopeFactory.Create(
            areaId: areaId,
            eventId: eventId,
            simulationRunId: runId,
            cycleIndex: 3,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 37.0,
            eventTime: projectedAt);

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        var record = Assert.Single(coordinator.Records);
        Assert.Equal(runId, record.SimulationRunId);
        Assert.Equal(3, record.CycleIndex);
        Assert.Equal(CycleObservationOutcome.Eligible, record.Outcome);
        Assert.NotNull(record.Assessment);
        var save = Assert.Single(projectionStore.Saves);
        Assert.Equal(areaId, save.AreaId);
        Assert.Equal(runId, save.SimulationRunId);
        Assert.Equal(3, save.CycleIndex);
        Assert.Equal(1, save.AssessmentCount);
        var projected = Assert.Single(riskAssessmentRepository.ProjectedCalls);
        Assert.Equal(eventId, projected.SourceEventId);
        Assert.Equal(projectedAt, projected.ProjectedAt);
        Assert.Equal(alertedAt, projected.AlertedAt);
        Assert.Empty(projectionStore.CellSaves);
        Assert.Empty(projectionStore.UnavailableCalls);
        Assert.Empty(influxWriteService.AreaSnapshots);
        Assert.Single(influxWriteService.RiskAssessments);
        Assert.Equal(2, Assert.Single(influxWriteService.Batches).PointCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_TemporalBlockedCycle_MarksFinalizedCycleUnavailable()
    {
        var areaId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 7, 21, 13, 0, 0, TimeSpan.Zero);
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new RecordingRiskAssessmentRepository();
        var projectionStore = new RecordingProjectionStore(new AreaProjectionWriteResult(observedAt, null));
        var scoringService = new ThrowingRiskScoringService();
        var coordinator = new RecordingCycleProjectionCoordinator((_, _) =>
        [
            new FinalizedCycleProjection(
                runId,
                CycleIndex: 4,
                areaId,
                Snapshot: null,
                EligibleCount: 0,
                IsOperational: true,
                EligibleEventIds: [],
                AggregationReason: "all_readings_blocked")
        ]);
        var pipeline = CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            new InMemoryAreaRiskSnapshotRepository(),
            projectionStore,
            influxWriteService: new FakeInfluxWriteService(),
            new BlockedRiskEligibilityService(),
            scoringService,
            coordinator);
        var envelope = EnvelopeFactory.Create(
            areaId: areaId,
            eventId: eventId,
            simulationRunId: runId,
            cycleIndex: 4,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 22.0,
            eventTime: observedAt);

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        var record = Assert.Single(coordinator.Records);
        Assert.Equal(CycleObservationOutcome.Blocked, record.Outcome);
        Assert.Null(record.Assessment);
        var unavailable = Assert.Single(projectionStore.UnavailableCalls);
        Assert.Equal(areaId, unavailable.AreaId);
        Assert.Equal(runId, unavailable.SimulationRunId);
        Assert.Equal(4, unavailable.CycleIndex);
        Assert.Equal("all_readings_blocked", unavailable.Reason);
        Assert.Empty(projectionStore.Saves);
        Assert.Empty(riskAssessmentRepository.AddedAssessments);
        Assert.Empty(riskAssessmentRepository.ProjectedCalls);
        Assert.Equal(0, scoringService.CallCount);
    }

    [Fact]
    public async Task ProcessAcceptedReadingAsync_TemporalFinalization_IgnoresNonOperationalCycles()
    {
        var areaId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var observedAt = new DateTimeOffset(2026, 7, 21, 14, 0, 0, TimeSpan.Zero);
        var projectionStore = new RecordingProjectionStore(new AreaProjectionWriteResult(observedAt, null));
        var coordinator = new RecordingCycleProjectionCoordinator((_, assessment) =>
        [
            new FinalizedCycleProjection(
                runId,
                CycleIndex: 5,
                areaId,
                new AreaRiskSnapshot(eventId, observedAt, assessment!.RiskScore, "non-operational cycle"),
                EligibleCount: 1,
                IsOperational: false,
                EligibleEventIds: [eventId])
        ]);
        var pipeline = CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new RecordingRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            projectionStore,
            influxWriteService: new FakeInfluxWriteService(),
            new RiskEligibilityService(),
            cycleProjectionCoordinator: coordinator);
        var envelope = EnvelopeFactory.Create(
            areaId: areaId,
            eventId: eventId,
            simulationRunId: runId,
            cycleIndex: 5,
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 35.0,
            eventTime: observedAt);

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        Assert.Single(coordinator.Records);
        Assert.Empty(projectionStore.Saves);
        Assert.Empty(projectionStore.UnavailableCalls);
    }

    private static ReadingRiskPipeline CreatePipeline(
        IAcceptedReadingRepository acceptedReadingRepository,
        IRiskAssessmentRepository riskAssessmentRepository,
        IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
        IAreaOperationalProjectionStore projectionStore,
        IInfluxWriteService influxWriteService,
        IRiskEligibilityService riskEligibilityService,
        IRiskScoringService? riskScoringService = null,
        ICycleProjectionCoordinator? cycleProjectionCoordinator = null)
    {
        return new ReadingRiskPipeline(
            acceptedReadingRepository,
            riskEligibilityService,
            new InMemoryDailyCellStateRepository(),
            riskScoringService ?? new SimpleRiskScoringService(),
            riskAssessmentRepository,
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository,
            projectionStore,
            influxWriteService,
            NullLogger<ReadingRiskPipeline>.Instance,
            cycleProjectionCoordinator);
    }

    private sealed class TrackingRiskEligibilityService(List<string> callSequence) : IRiskEligibilityService
    {
        public Task<RiskEligibilityResult> EvaluateAsync(
            NormalizedReading reading,
            CancellationToken cancellationToken)
        {
            callSequence.Add("eligibility");
            return Task.FromResult(RiskEligibilityResult.Eligible);
        }
    }

    private sealed class TrackingRiskScoringService(List<string> callSequence) : IRiskScoringService
    {
        public RiskAssessment CreateAssessment(RiskInput input)
        {
            callSequence.Add("scoring");

            return new RiskAssessment(
                id: Guid.NewGuid(),
                timestamp: input.EventTime,
                riskScore: 0.65,
                explanationSummary: "tracked");
        }
    }

    private sealed class CapturingRiskScoringService : IRiskScoringService
    {
        public RiskInput? LastInput { get; private set; }
        public List<RiskInput> Inputs { get; } = [];

        public RiskAssessment CreateAssessment(RiskInput input)
        {
            LastInput = input;
            Inputs.Add(input);

            return new RiskAssessment(
                id: Guid.NewGuid(),
                timestamp: input.EventTime,
                riskScore: 0.42,
                explanationSummary: "captured");
        }
    }

    private sealed class NotEligibleRiskEligibilityService : IRiskEligibilityService
    {
        public Task<RiskEligibilityResult> EvaluateAsync(
            NormalizedReading reading,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(RiskEligibilityResult.NotEligible(
                RiskEligibilityReason.UnsupportedMetric,
                "Metric is not currently eligible for risk evaluation."));
        }
    }

    private sealed class ThrowingRiskScoringService : IRiskScoringService
    {
        public int CallCount { get; private set; }

        public RiskAssessment CreateAssessment(RiskInput input)
        {
            CallCount++;
            throw new InvalidOperationException("Scoring should not be called for ineligible readings.");
        }
    }

    private sealed class BlockedRiskEligibilityService : IRiskEligibilityService
    {
        public Task<RiskEligibilityResult> EvaluateAsync(
            NormalizedReading reading,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(RiskEligibilityResult.Blocked(
                RiskEligibilityReason.MissingRequiredValue,
                "Critical metric is missing for risk scoring.",
                ["MissingValue"]));
        }
    }

    private sealed class PartialEligibleRiskEligibilityService : IRiskEligibilityService
    {
        public Task<RiskEligibilityResult> EvaluateAsync(
            NormalizedReading reading,
            CancellationToken cancellationToken)
        {
            var classifierResult = ClassifierResult.Create(
                classifierName: "temporal_classifier",
                status: ClassifierStatus.Warning,
                severity: ClassifierSeverity.Medium,
                qualityFlags: ["Delayed"],
                reasons: ["late_arrival"],
                evaluatedAt: new DateTimeOffset(2026, 5, 12, 9, 0, 1, TimeSpan.Zero),
                ruleSetVersion: "v1.0");

            return Task.FromResult(RiskEligibilityResult.PartialButUsable(
                RiskEligibilityReason.DelayedReading,
                "Reading is delayed but still usable.",
                qualityFlags: ["Delayed"],
                classifierResults: [classifierResult]));
        }
    }

    private sealed class RecordingCycleProjectionCoordinator(
        Func<CycleRecord, RiskAssessment?, IReadOnlyList<FinalizedCycleProjection>> finalizationsFactory)
        : ICycleProjectionCoordinator
    {
        public List<CycleRecord> Records { get; } = [];

        public Task<IReadOnlyList<FinalizedCycleProjection>> RecordAsync(
            Guid simulationRunId,
            int cycleIndex,
            Guid areaId,
            Guid sensorId,
            Guid eventId,
            DateTimeOffset eventTime,
            MetricOrigin origin,
            CycleObservationOutcome outcome,
            RiskAssessment? assessment,
            CancellationToken cancellationToken)
        {
            var record = new CycleRecord(
                simulationRunId,
                cycleIndex,
                areaId,
                sensorId,
                eventId,
                eventTime,
                origin,
                outcome,
                assessment);
            Records.Add(record);
            return Task.FromResult(finalizationsFactory(record, assessment));
        }
    }

    private sealed class RecordingProjectionStore(AreaProjectionWriteResult result) : IAreaOperationalProjectionStore
    {
        public List<CellSaveCall> CellSaves { get; } = [];
        public List<ProjectionSaveCall> Saves { get; } = [];
        public List<UnavailableCall> UnavailableCalls { get; } = [];

        public Task SaveCellAsync(
            Guid areaId,
            Guid sensorId,
            RiskAssessment assessment,
            CancellationToken cancellationToken)
        {
            CellSaves.Add(new CellSaveCall(areaId, sensorId, assessment));
            return Task.CompletedTask;
        }

        public Task<AreaProjectionWriteResult> SaveAsync(
            Guid areaId,
            AreaRiskSnapshot snapshot,
            int assessmentCount,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null,
            int? cycleIndex = null)
        {
            Saves.Add(new ProjectionSaveCall(areaId, snapshot, assessmentCount, simulationRunId, cycleIndex));
            return Task.FromResult(result);
        }

        public Task MarkUnavailableAsync(
            Guid areaId,
            DateTimeOffset snapshotTimestamp,
            string reason,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null,
            int? cycleIndex = null)
        {
            UnavailableCalls.Add(new UnavailableCall(areaId, snapshotTimestamp, reason, simulationRunId, cycleIndex));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRiskAssessmentRepository : IRiskAssessmentRepository
    {
        public List<AssessmentAddCall> AddedAssessments { get; } = [];
        public List<ProjectedCall> ProjectedCalls { get; } = [];

        public Task<RiskAssessment> AddAsync(
            Guid areaId,
            Guid sensorId,
            Guid sourceEventId,
            RiskAssessment assessment,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null)
        {
            AddedAssessments.Add(new AssessmentAddCall(areaId, sensorId, sourceEventId, assessment, simulationRunId));
            return Task.FromResult(assessment);
        }

        public Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
            Guid areaId,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyCollection<RiskAssessment>>(
                AddedAssessments
                    .Where(item => item.AreaId == areaId)
                    .Select(item => item.Assessment)
                    .ToArray());

        public Task<IReadOnlyCollection<RiskAssessment>> GetLatestByAreaAsync(
            Guid areaId,
            CancellationToken cancellationToken,
            Guid? simulationRunId = null)
            => GetByAreaAsync(areaId, cancellationToken);

        public Task MarkProjectedAsync(
            Guid sourceEventId,
            DateTimeOffset projectedAt,
            DateTimeOffset? alertedAt,
            CancellationToken cancellationToken)
        {
            ProjectedCalls.Add(new ProjectedCall(sourceEventId, projectedAt, alertedAt));
            return Task.CompletedTask;
        }
    }

    private sealed record CycleRecord(
        Guid SimulationRunId,
        int CycleIndex,
        Guid AreaId,
        Guid SensorId,
        Guid EventId,
        DateTimeOffset EventTime,
        MetricOrigin Origin,
        CycleObservationOutcome Outcome,
        RiskAssessment? Assessment);

    private sealed record CellSaveCall(Guid AreaId, Guid SensorId, RiskAssessment Assessment);

    private sealed record ProjectionSaveCall(
        Guid AreaId,
        AreaRiskSnapshot Snapshot,
        int AssessmentCount,
        Guid? SimulationRunId,
        int? CycleIndex);

    private sealed record UnavailableCall(
        Guid AreaId,
        DateTimeOffset SnapshotTimestamp,
        string Reason,
        Guid? SimulationRunId,
        int? CycleIndex);

    private sealed record AssessmentAddCall(
        Guid AreaId,
        Guid SensorId,
        Guid SourceEventId,
        RiskAssessment Assessment,
        Guid? SimulationRunId);

    private sealed record ProjectedCall(
        Guid SourceEventId,
        DateTimeOffset ProjectedAt,
        DateTimeOffset? AlertedAt);
}
