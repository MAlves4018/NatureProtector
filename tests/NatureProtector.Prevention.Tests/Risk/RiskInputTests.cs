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
        var source = Assert.Single(input.SourceReadings);
        Assert.Equal(reading.EventId, source.EventId);
        Assert.Equal(reading.Value, source.Value);
        Assert.Equal(33.4, input.Metrics.TemperatureCelsius);
        Assert.Null(input.Metrics.RelativeHumidityPercent);
        Assert.Equal(input.EventTime, input.ValidFrom);
        Assert.Equal(input.EventTime, input.ValidTo);
        Assert.Empty(input.QualityFlags);
        Assert.Empty(input.ClassifierResults);
    }

    [Fact]
    public void FromNormalizedReading_WithDailyState_AttachesContextWithoutResultFields()
    {
        var gridCellId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var configurationVersionId = Guid.NewGuid();
        var reading = CreateReading();
        var dailyState = new DailyCellState(
            areaId: reading.AreaId,
            sensorId: reading.SensorId,
            day: reading.EventTime,
            antecedentState: "runtime-observed",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "test",
            lastUpdatedAt: reading.EventTime,
            maxTemperatureCelsius: 31.0,
            lastSourceEventId: reading.EventId,
            gridCellId: gridCellId,
            simulationRunId: runId,
            configurationVersionId: configurationVersionId,
            latestHumidityPercent: 35.0,
            latestWindSpeedMetersPerSecond: 7.0,
            fireWeatherIndex: 65.377,
            keetchByramDroughtIndex: 650.106,
            fireIndexProvenance: "imported_reference");

        var input = RiskInput.FromNormalizedReading(
            reading,
            RiskEligibilityResult.Eligible,
            dailyState,
            runId,
            gridCellId,
            configurationVersionId);

        Assert.Equal(runId, input.SimulationRunId);
        Assert.Equal(gridCellId, input.GridCellId);
        Assert.Equal(configurationVersionId, input.ConfigurationVersionId);
        Assert.Equal(DailyCellStateStatus.Present, input.DailyCellStateStatus);
        Assert.Same(dailyState, input.DailyCellState);
        Assert.True(input.Metrics.IsCompleteV1);
        Assert.Equal(gridCellId, input.TerritorialContext.GridCellId);
        Assert.Equal(dailyState.FireWeatherIndex, input.FireWeatherIndexContext.FireWeatherIndex);
        Assert.Equal(dailyState.KeetchByramDroughtIndex, input.FireWeatherIndexContext.KeetchByramDroughtIndex);
        Assert.Equal(dailyState.FireIndexProvenance, input.FireWeatherIndexContext.Provenance);
        Assert.DoesNotContain(
            typeof(RiskInput).GetProperties().Select(property => property.Name),
            name => name is "BaseRisk" or "AdjustedScore" or "RiskScore" or "RiskLevel" or "AlertState" or "OperationalProjection");
    }

    [Fact]
    public void FromNormalizedReading_WithoutDailyState_MarksMissingContextExplicitly()
    {
        var reading = CreateReading();

        var input = RiskInput.FromNormalizedReading(
            reading,
            RiskEligibilityResult.Eligible,
            dailyCellState: null,
            simulationRunId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            configurationVersionId: Guid.NewGuid());

        Assert.Equal(DailyCellStateStatus.Missing, input.DailyCellStateStatus);
        Assert.Null(input.DailyCellState);
        Assert.False(input.FireWeatherIndexContext.HasAnyIndex);
        Assert.Equal("absent", input.FireWeatherIndexContext.Provenance);
        Assert.Contains(RiskInput.MissingDailyCellStateFlag, input.QualityFlags);
        Assert.Contains(QualityFlag.DailyCellStateMissing, input.TypedQualityFlags);
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
    public void FromWindow_AggregatesSourceReadingsAndCanonicalMetrics()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 4, 30, 10, 45, 0, TimeSpan.Zero);
        var readings = new[]
        {
            CreateReading(areaId, sensorId, SensorMetricType.Temperature, MeasurementUnit.Celsius, 32.0, start),
            CreateReading(areaId, sensorId, SensorMetricType.Humidity, MeasurementUnit.Percent, 24.0, start.AddSeconds(10)),
            CreateReading(areaId, sensorId, SensorMetricType.WindSpeed, MeasurementUnit.MetersPerSecond, 6.0, start.AddSeconds(20))
        };

        var input = RiskInput.FromWindow(
            readings,
            RiskEligibilityResult.Eligible,
            dailyCellState: null,
            simulationRunId: Guid.NewGuid(),
            gridCellId: Guid.NewGuid(),
            configurationVersionId: Guid.NewGuid());

        Assert.Equal(RiskInputStatus.CompleteEligible, input.InputStatus);
        Assert.Equal(32.0, input.Metrics.TemperatureCelsius);
        Assert.Equal(24.0, input.Metrics.RelativeHumidityPercent);
        Assert.Equal(6.0, input.Metrics.WindSpeedMetersPerSecond);
        Assert.Equal(start, input.ValidFrom);
        Assert.Equal(start.AddSeconds(20), input.ValidTo);
        Assert.Equal(3, input.SourceReadings.Count);
    }

    [Fact]
    public void FromWindow_MarksPartialWithLowCoverage_WhenOnlyOneMetricIsAvailable()
    {
        var input = RiskInput.FromWindow(
            [CreateReading()],
            RiskEligibilityResult.Eligible,
            dailyCellState: null,
            simulationRunId: null,
            gridCellId: null,
            configurationVersionId: null);

        Assert.Equal(RiskInputStatus.PartialButUsable, input.InputStatus);
        Assert.Equal(ObservationalConfidenceLevel.Low, input.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Compromised, input.OperationalIntegrity);
        Assert.Contains(RiskInput.MissingDailyCellStateFlag, input.QualityFlags);
        Assert.Contains(RiskInput.LowCoverageFlag, input.QualityFlags);
    }

    [Fact]
    public void FromWindow_MarksPartialAndPenalizesConfidence_WhenTwoMetricsAreAvailable()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 4, 30, 10, 45, 0, TimeSpan.Zero);

        var input = RiskInput.FromWindow(
            [
                CreateReading(areaId, sensorId, SensorMetricType.Temperature, MeasurementUnit.Celsius, 32.0, start),
                CreateReading(areaId, sensorId, SensorMetricType.WindSpeed, MeasurementUnit.MetersPerSecond, 6.0, start.AddSeconds(20))
            ],
            RiskEligibilityResult.Eligible,
            dailyCellState: null,
            simulationRunId: null,
            gridCellId: null,
            configurationVersionId: null);

        Assert.Equal(RiskInputStatus.PartialButUsable, input.InputStatus);
        Assert.Equal(ObservationalConfidenceLevel.Medium, input.ObservationalConfidence);
        Assert.Equal(OperationalIntegrityLevel.Degraded, input.OperationalIntegrity);
        Assert.Contains(RiskInput.MissingDailyCellStateFlag, input.QualityFlags);
        Assert.DoesNotContain(RiskInput.LowCoverageFlag, input.QualityFlags);
    }

    [Fact]
    public void FromWindow_MarksBlockedWhenNoRiskMetricIsAvailable()
    {
        var reading = CreateReading(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SensorMetricType.WindDirection,
            MeasurementUnit.Degrees,
            180.0,
            DateTimeOffset.UtcNow);

        var input = RiskInput.FromWindow(
            [reading],
            RiskEligibilityResult.Eligible,
            dailyCellState: null,
            simulationRunId: null,
            gridCellId: null,
            configurationVersionId: null);

        Assert.Equal(RiskInputStatus.Blocked, input.InputStatus);
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

    private static NormalizedReading CreateReading(
        Guid areaId,
        Guid sensorId,
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value,
        DateTimeOffset eventTime)
    {
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-risk-input-window",
            AreaId: areaId,
            SensorId: sensorId,
            SensorName: "Sensor-PT-Window",
            MetricType: metricType,
            Value: value,
            Unit: unit,
            Latitude: 39.70,
            Longitude: -7.90,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: eventTime,
            IngestTime: null);
    }
}
