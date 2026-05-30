using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class RiskEligibilityServiceTests
{
    [Fact]
    public async Task CompleteEligible_AllRequiredDataPresent()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading();

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.CompleteEligible, result.Status);
        Assert.Equal(RiskEligibilityReason.Eligible, result.ReasonCode);
        Assert.Equal(ObservationalConfidenceLevel.High, result.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Intact, result.OperationalIntegrity);
        Assert.Empty(result.QualityFlags);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task PartialButUsable_DegradedButAllowed()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(operationalState: SensorOperationalState.Delayed);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.PartialButUsable, result.Status);
        Assert.Equal(RiskEligibilityReason.DelayedReading, result.ReasonCode);
        Assert.Equal(ObservationalConfidenceLevel.Medium, result.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Degraded, result.OperationalIntegrity);
        Assert.Contains("Delayed", result.QualityFlags);
    }

    [Fact]
    public async Task PartialButUsable_RetransmittedReading_UsesRetransmittedReason()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(operationalState: SensorOperationalState.Retransmitted);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.PartialButUsable, result.Status);
        Assert.Equal(RiskEligibilityReason.RetransmittedReading, result.ReasonCode);
        Assert.Contains("Duplicate", result.QualityFlags);
    }

    [Fact]
    public async Task Blocked_WhenCriticalDataMissing()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(areaId: Guid.Empty);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.MissingRequiredValue, result.ReasonCode);
        Assert.Equal(ObservationalConfidenceLevel.Low, result.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Compromised, result.OperationalIntegrity);
        Assert.Contains("MissingValue", result.QualityFlags);
    }

    [Fact]
    public async Task Blocked_WhenOperationalStateIsInvalid_UsesInvalidOperationalStateReason()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(operationalState: SensorOperationalState.Invalid);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.InvalidOperationalState, result.ReasonCode);
        Assert.Contains("SemanticMismatch", result.QualityFlags);
    }

    [Fact]
    public async Task Blocked_WhenOperationalStateIsDropped_UsesInvalidOperationalStateReason()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(operationalState: SensorOperationalState.Dropped);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.InvalidOperationalState, result.ReasonCode);
        Assert.Contains("SemanticMismatch", result.QualityFlags);
    }

    [Fact]
    public async Task Blocked_WhenMetricIsUnsupported_UsesUnsupportedMetricReason()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(
            metricType: SensorMetricType.WindDirection,
            unit: MeasurementUnit.Degrees);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.UnsupportedMetric, result.ReasonCode);
        Assert.Contains("UnsupportedMetric", result.QualityFlags);
    }

    [Fact]
    public async Task Blocked_WhenMetricEnumIsUndefined_UsesUnsupportedMetricReason()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(metricType: (SensorMetricType)999);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.UnsupportedMetric, result.ReasonCode);
        Assert.Contains("UnsupportedMetric", result.QualityFlags);
    }

    [Fact]
    public async Task Blocked_WhenMetricUnitCombinationIsUnsupported_UsesInvalidUnitReason()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Percent);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.InvalidUnit, result.ReasonCode);
        Assert.Contains("InvalidUnit", result.QualityFlags);
    }

    [Fact]
    public async Task PartialButUsable_WhenTemporalClassifierMarksStale()
    {
        var service = new RiskEligibilityService();
        var eventTime = new DateTimeOffset(2026, 4, 30, 15, 0, 0, TimeSpan.Zero);
        var reading = CreateReading(
            eventTime: eventTime,
            ingestTime: eventTime.AddSeconds(360));

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.PartialButUsable, result.Status);
        Assert.Contains("Delayed", result.QualityFlags);
        Assert.Contains("Stale", result.QualityFlags);
        var classifier = Assert.Single(result.ClassifierResults);
        Assert.Equal(ReadingTemporalClassifier.ClassifierName, classifier.ClassifierName);
    }

    [Fact]
    public async Task Blocked_WhenRangeClassifierMarksOutlier()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading(value: 80.0);

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Contains("Outlier", result.QualityFlags);
        Assert.Contains("range_clipping", result.QualityFlags);
        var classifier = Assert.Single(result.ClassifierResults);
        Assert.Equal(ReadingRangeClassifier.ClassifierName, classifier.ClassifierName);
        Assert.Equal(ClassifierAction.Block, classifier.Action);
    }


    [Fact]
    public void EligibleSingleton_HasExpectedReasonCode_AndStatus()
    {
        var result = RiskEligibilityResult.Eligible;

        Assert.True(result.IsEligible);
        Assert.Equal(RiskInputStatus.CompleteEligible, result.Status);
        Assert.Equal(RiskEligibilityReason.Eligible, result.ReasonCode);
        Assert.Null(result.Message);
    }

    [Fact]
    public void NotEligibleFactory_CreatesExpectedIneligibleResult()
    {
        var result = RiskEligibilityResult.NotEligible(
            RiskEligibilityReason.UnsupportedMetric,
            "Metric not supported by the current risk model.");

        Assert.False(result.IsEligible);
        Assert.Equal(RiskInputStatus.Blocked, result.Status);
        Assert.Equal(RiskEligibilityReason.UnsupportedMetric, result.ReasonCode);
        Assert.Equal("Metric not supported by the current risk model.", result.Message);
    }

    [Fact]
    public void EligibilityResult_CanCarryClassifierResults()
    {
        var classifierResult = ClassifierResult.Create(
            classifierName: "temporal_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Medium,
            qualityFlags: ["Delayed"],
            reasons: ["late_arrival"],
            evaluatedAt: new DateTimeOffset(2026, 5, 11, 12, 0, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");

        var result = RiskEligibilityResult.PartialButUsable(
            RiskEligibilityReason.DelayedReading,
            "Degraded but still eligible.",
            qualityFlags: ["Delayed"],
            classifierResults: [classifierResult]);

        Assert.Single(result.ClassifierResults);
        Assert.Equal("temporal_classifier", result.ClassifierResults[0].ClassifierName);
    }

    [Fact]
    public void CompleteEligible_HasEmptyClassifierResultsByDefault()
    {
        var result = RiskEligibilityResult.CompleteEligible();

        Assert.Empty(result.ClassifierResults);
    }

    [Fact]
    public void Blocked_CanCarryFailedClassifierResult()
    {
        var classifierResult = ClassifierResult.Create(
            classifierName: "semantic_classifier",
            status: ClassifierStatus.Failed,
            severity: ClassifierSeverity.High,
            qualityFlags: ["SemanticMismatch"],
            reasons: ["invalid_state"],
            evaluatedAt: new DateTimeOffset(2026, 5, 11, 12, 10, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");

        var result = RiskEligibilityResult.Blocked(
            RiskEligibilityReason.InvalidOperationalState,
            "Blocked due to invalid operational state.",
            qualityFlags: ["SemanticMismatch"],
            classifierResults: [classifierResult]);

        var carried = Assert.Single(result.ClassifierResults);
        Assert.Equal(ClassifierStatus.Failed, carried.Status);
        Assert.Equal(ClassifierSeverity.High, carried.Severity);
    }

    private static NormalizedReading CreateReading(
        Guid? areaId = null,
        SensorOperationalState operationalState = SensorOperationalState.Nominal,
        SensorMetricType metricType = SensorMetricType.Temperature,
        MeasurementUnit unit = MeasurementUnit.Celsius,
        double value = 28.4,
        DateTimeOffset? eventTime = null,
        DateTimeOffset? ingestTime = null)
    {
        var resolvedEventTime = eventTime ?? new DateTimeOffset(2026, 4, 30, 15, 0, 0, TimeSpan.Zero);
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-eligibility",
            AreaId: areaId ?? Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-03",
            MetricType: metricType,
            Value: value,
            Unit: unit,
            Latitude: 39.78,
            Longitude: -7.88,
            OperationalState: operationalState,
            EventTime: resolvedEventTime,
            IngestTime: ingestTime ?? resolvedEventTime.AddSeconds(3));
    }
}
