using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class RiskInputTests
{
    [Fact]
    public void FromNormalizedReading_UsesRiskRelevantFields()
    {
        var reading = new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-01",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-02",
            MetricType: SensorMetricType.Temperature,
            Value: 33.4,
            Unit: MeasurementUnit.Celsius,
            Latitude: 39.75,
            Longitude: -7.92,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: new DateTimeOffset(2026, 4, 30, 10, 30, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 30, 10, 30, 5, TimeSpan.Zero));

        var input = RiskInput.FromNormalizedReading(reading);

        Assert.Equal(reading.AreaId, input.AreaId);
        Assert.Equal(reading.SensorId, input.SensorId);
        Assert.Equal(reading.EventId, input.SourceEventId);
        Assert.Equal(reading.MetricType, input.MetricType);
        Assert.Equal(reading.Value, input.Value);
        Assert.Equal(reading.Unit, input.Unit);
        Assert.Equal(reading.EventTime, input.EventTime);
        Assert.Equal(RiskInputStatus.CompleteEligible, input.InputStatus);
        Assert.Equal(RiskEligibilityReason.Eligible, input.EligibilityReason);
        Assert.Empty(input.QualityFlags);
        Assert.Empty(input.ClassifierResults);
    }

    [Fact]
    public void FromNormalizedReading_WithEligibility_CarriesStatusFlagsAndClassifiers()
    {
        var classifierResult = ClassifierResult.Create(
            classifierName: "temporal_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Medium,
            qualityFlags: ["Delayed"],
            reasons: ["late_arrival"],
            evaluatedAt: new DateTimeOffset(2026, 5, 12, 11, 30, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");
        var reading = new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-02",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-06",
            MetricType: SensorMetricType.WindSpeed,
            Value: 14.0,
            Unit: MeasurementUnit.MetersPerSecond,
            Latitude: 39.70,
            Longitude: -7.90,
            OperationalState: SensorOperationalState.Delayed,
            EventTime: new DateTimeOffset(2026, 4, 30, 10, 45, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 30, 10, 45, 4, TimeSpan.Zero))
        {
            QualityFlags = ["OutOfOrder"]
        };
        var eligibility = RiskEligibilityResult.PartialButUsable(
            RiskEligibilityReason.DelayedReading,
            "Reading is delayed but still usable.",
            qualityFlags: ["Delayed", "OutOfOrder"],
            classifierResults: [classifierResult]);

        var input = RiskInput.FromNormalizedReading(reading, eligibility);

        Assert.Equal(RiskInputStatus.PartialButUsable, input.InputStatus);
        Assert.Equal(RiskEligibilityReason.DelayedReading, input.EligibilityReason);
        Assert.Equal(ObservationalConfidenceLevel.Medium, input.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Degraded, input.OperationalIntegrity);
        Assert.Equal(["OutOfOrder", "Delayed"], input.QualityFlags);
        var carried = Assert.Single(input.ClassifierResults);
        Assert.Equal(classifierResult.ClassifierName, carried.ClassifierName);
    }

    [Fact]
    public void FromNormalizedReading_WithEligibilityFallsBackToReadingClassifiers_WhenEligibilityHasNone()
    {
        var readingClassifier = ClassifierResult.Create(
            classifierName: "semantic_classifier",
            status: ClassifierStatus.Warning,
            severity: ClassifierSeverity.Low,
            qualityFlags: ["ReadingFlag"],
            reasons: ["reading_reason"],
            evaluatedAt: new DateTimeOffset(2026, 5, 12, 11, 30, 0, TimeSpan.Zero),
            ruleSetVersion: "v1.0");
        var reading = CreateReading() with
        {
            ClassifierResults = [readingClassifier]
        };
        var eligibility = RiskEligibilityResult.PartialButUsable(
            RiskEligibilityReason.DelayedReading,
            "Reading is delayed but still usable.");

        var input = RiskInput.FromNormalizedReading(reading, eligibility);

        var carried = Assert.Single(input.ClassifierResults);
        Assert.Equal(readingClassifier.ClassifierName, carried.ClassifierName);
        Assert.Equal(RiskInputStatus.PartialButUsable, input.InputStatus);
        Assert.Equal(RiskEligibilityReason.DelayedReading, input.EligibilityReason);
    }

    [Fact]
    public void FromNormalizedReading_WithEligibilityNormalizesAndDeduplicatesQualityFlags()
    {
        var reading = CreateReading() with
        {
            QualityFlags = [" Delayed ", "", "OutOfOrder", "Delayed", "   "]
        };
        var eligibility = RiskEligibilityResult.PartialButUsable(
            RiskEligibilityReason.DelayedReading,
            "Reading is delayed but still usable.",
            qualityFlags: ["OutOfOrder", " SensorDegraded ", "Delayed"]);

        var input = RiskInput.FromNormalizedReading(reading, eligibility);

        Assert.Equal(["Delayed", "OutOfOrder", "SensorDegraded"], input.QualityFlags);
        Assert.Equal(RiskInputStatus.PartialButUsable, input.InputStatus);
    }

    [Fact]
    public void FromNormalizedReading_NullFlagsAndClassifiers_ReturnsEmptyCollections()
    {
        var reading = CreateReading() with
        {
            QualityFlags = null!,
            ClassifierResults = null!
        };

        var input = RiskInput.FromNormalizedReading(reading);

        Assert.Empty(input.QualityFlags);
        Assert.Empty(input.ClassifierResults);
        Assert.Equal(RiskInputStatus.CompleteEligible, input.InputStatus);
    }

    [Theory]
    [InlineData(RiskInputStatus.Blocked, RiskEligibilityReason.UnsupportedMetric)]
    [InlineData(RiskInputStatus.PartialButUsable, RiskEligibilityReason.DelayedReading)]
    [InlineData(RiskInputStatus.CompleteEligible, RiskEligibilityReason.Eligible)]
    public void FromNormalizedReading_WithEligibilityPreservesStatusSemantics(
        RiskInputStatus expectedStatus,
        RiskEligibilityReason expectedReason)
    {
        var reading = CreateReading();
        var eligibility = expectedStatus switch
        {
            RiskInputStatus.Blocked => RiskEligibilityResult.Blocked(
                expectedReason,
                "Blocked for risk scoring."),
            RiskInputStatus.PartialButUsable => RiskEligibilityResult.PartialButUsable(
                expectedReason,
                "Partial but usable."),
            _ => RiskEligibilityResult.CompleteEligible("Complete.")
        };

        var input = RiskInput.FromNormalizedReading(reading, eligibility);

        Assert.Equal(expectedStatus, input.InputStatus);
        Assert.Equal(expectedReason, input.EligibilityReason);
    }

    private static NormalizedReading CreateReading()
    {
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-risk-input",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-06",
            MetricType: SensorMetricType.Temperature,
            Value: 31.0,
            Unit: MeasurementUnit.Celsius,
            Latitude: 39.70,
            Longitude: -7.90,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: new DateTimeOffset(2026, 4, 30, 10, 45, 0, TimeSpan.Zero),
            IngestTime: null);
    }
}
