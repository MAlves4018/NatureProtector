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
}
