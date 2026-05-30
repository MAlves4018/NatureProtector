using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class SimpleRiskScoringServiceTests
{
    private readonly SimpleRiskScoringService _service = new();

    public static TheoryData<double, double> TemperatureCases => new()
    {
        { 19.9, 0.30 },
        { 20.0, 0.35 },
        { 25.0, 0.45 },
        { 30.0, 0.575 },
        { 35.0, 0.675 },
        { 40.0, 0.75 }
    };

    public static TheoryData<double, double> HumidityCases => new()
    {
        { 70.0, 0.275 },
        { 50.0, 0.35 },
        { 35.0, 0.45 },
        { 20.0, 0.60 },
        { 19.9, 0.725 }
    };

    public static TheoryData<double, double> WindCases => new()
    {
        { 4.9, 0.30 },
        { 5.0, 0.40 },
        { 10.0, 0.525 },
        { 15.0, 0.625 },
        { 20.0, 0.725 }
    };

    [Theory]
    [MemberData(nameof(TemperatureCases))]
    public void CreateAssessment_MapsTemperatureThresholds(double value, double expectedScore)
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.Temperature,
            value: value));

        Assert.Equal(expectedScore, assessment.BaseRisk, precision: 3);
        Assert.Equal(expectedScore, assessment.AdjustedScore, precision: 3);
        Assert.Equal(expectedScore, assessment.RiskScore, precision: 3);
        Assert.Equal(CandidateParameterSetV1.ToScore100(assessment.AdjustedScore), assessment.Score100);
    }

    [Theory]
    [MemberData(nameof(HumidityCases))]
    public void CreateAssessment_MapsHumidityThresholds(double value, double expectedScore)
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.Humidity,
            value: value));

        Assert.Equal(expectedScore, assessment.BaseRisk, precision: 3);
        Assert.Equal(expectedScore, assessment.AdjustedScore, precision: 3);
        Assert.Equal(expectedScore, assessment.RiskScore, precision: 3);
    }

    [Theory]
    [MemberData(nameof(WindCases))]
    public void CreateAssessment_MapsWindSpeedThresholds(double value, double expectedScore)
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.WindSpeed,
            value: value));

        Assert.Equal(expectedScore, assessment.BaseRisk, precision: 3);
        Assert.Equal(expectedScore, assessment.AdjustedScore, precision: 3);
        Assert.Equal(expectedScore, assessment.RiskScore, precision: 3);
    }

    [Fact]
    public void CreateAssessment_UsesFallbackScore_ForUnsupportedMetric()
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.WindDirection,
            value: 180.0,
            unit: MeasurementUnit.Degrees));

        Assert.Equal(0.35, assessment.BaseRisk, precision: 3);
        Assert.Equal(0.35, assessment.AdjustedScore, precision: 3);
        Assert.Equal(0.35, assessment.RiskScore, precision: 3);
    }

    [Fact]
    public void CreateAssessment_CreatesAssessmentWithExpectedMetadata()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var assessedAt = DateTimeOffset.UtcNow;

        var assessment = _service.CreateAssessment(new RiskInput(
            AreaId: areaId,
            SensorId: sensorId,
            SourceEventId: eventId,
            MetricType: SensorMetricType.Temperature,
            Value: 32.4,
            Unit: MeasurementUnit.Celsius,
            EventTime: assessedAt));

        Assert.NotEqual(Guid.Empty, assessment.Id);
        Assert.Equal(assessedAt, assessment.Timestamp);
        Assert.Contains(areaId.ToString(), assessment.ExplanationSummary);
        Assert.Contains(sensorId.ToString(), assessment.ExplanationSummary);
        Assert.Contains(eventId.ToString(), assessment.ExplanationSummary);
        Assert.Contains(nameof(SensorMetricType.Temperature), assessment.ExplanationSummary);
        Assert.Contains("M=", assessment.ExplanationSummary);
        Assert.Contains("D=", assessment.ExplanationSummary);
        Assert.Contains("T=", assessment.ExplanationSummary);
        Assert.Contains("H=", assessment.ExplanationSummary);
        Assert.Contains("F=", assessment.ExplanationSummary);
        Assert.Contains("G=", assessment.ExplanationSummary);
        Assert.Contains("BaseRisk=", assessment.ExplanationSummary);
        Assert.Contains("AdjustedScore=", assessment.ExplanationSummary);
        Assert.Contains("Score100=", assessment.ExplanationSummary);
        Assert.Contains("C=", assessment.ExplanationSummary);
        Assert.Contains("I=", assessment.ExplanationSummary);
        Assert.Contains("DominantDriver=", assessment.ExplanationSummary);
        Assert.Contains("CalculationStatus=", assessment.ExplanationSummary);
        Assert.Contains("TerritorySource=", assessment.ExplanationSummary);
        Assert.Contains("Candidate Parameter Set V1.0", assessment.ExplanationSummary);
        Assert.Equal("Meteorology", assessment.DominantDriver);
        Assert.Equal("Candidate Parameter Set V1.0", assessment.ParameterSetVersion);
        Assert.Equal("CandidateFallback", assessment.CalculationStatus);
    }

    [Fact]
    public void CreateAssessment_AdjustsScore_ForPartialButUsableInput()
    {
        var input = new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 35.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: DateTimeOffset.UtcNow)
        {
            InputStatus = RiskInputStatus.PartialButUsable,
            ObservationalConfidence = ObservationalConfidenceLevel.Medium,
            OperationalIntegrity = OperationalIntegrityLevel.Degraded
        };

        var assessment = _service.CreateAssessment(input);

        var expectedBaseRisk = 0.675;
        var expectedAdjusted = expectedBaseRisk * 0.97 * 0.90;
        Assert.Equal(expectedBaseRisk, assessment.BaseRisk, precision: 3);
        Assert.Equal(expectedAdjusted, assessment.AdjustedScore, precision: 3);
        Assert.Equal(assessment.AdjustedScore, assessment.RiskScore, precision: 6);
        Assert.Equal("QualityPenalty", assessment.DominantDriver);
        Assert.Equal(0.97, assessment.ConfidenceFactor, precision: 3);
        Assert.Equal(0.90, assessment.IntegrityFactor, precision: 3);
        Assert.Contains("InputStatus=PartialButUsable", assessment.ExplanationSummary);
    }

    [Fact]
    public void CreateAssessment_CalculatesV1Components_WhenCanonicalMetricsArePresent()
    {
        var dailyState = new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: DateTimeOffset.UtcNow,
            antecedentState: "dry",
            candidateParameterSetVersion: "Candidate Parameter Set V1.0",
            provenance: "test",
            lastUpdatedAt: DateTimeOffset.UtcNow,
            dailyPrecipitationMillimeters: 0.0,
            maxTemperatureCelsius: 35.0,
            latestHumidityPercent: 20.0,
            latestWindSpeedMetersPerSecond: 20.0,
            droughtContext: "dry");
        var input = CreateRiskInput(SensorMetricType.Temperature, 35.0) with
        {
            Metrics = new RiskInputMetricSet(35.0, 20.0, 20.0),
            DailyCellState = dailyState,
            TerritorialContext = new TerritorialRiskContext(Guid.NewGuid(), "test", 0.80)
        };

        var assessment = _service.CreateAssessment(input);

        Assert.Equal(0.791, assessment.BaseRisk, precision: 3);
        Assert.Equal(assessment.BaseRisk, assessment.AdjustedScore, precision: 6);
        Assert.Equal(0.8225, assessment.MeteorologyComponent, precision: 3);
        Assert.Equal(0.80, assessment.TerritoryComponent, precision: 3);
        Assert.Equal(0.80, assessment.HazardComponent, precision: 3);
        Assert.Equal(0.50, assessment.FuelComponent, precision: 3);
        Assert.Equal(0.50, assessment.GeomorphologyComponent, precision: 3);
        Assert.Contains("M=", assessment.ExplanationSummary);
        Assert.Contains("D=", assessment.ExplanationSummary);
        Assert.Contains("T=", assessment.ExplanationSummary);
    }

    [Fact]
    public void CreateAssessment_UsesImportedFwiAndKbdi_WhenDailyIndexContextExists()
    {
        var input = CreateRiskInput(SensorMetricType.Temperature, 35.0) with
        {
            Metrics = new RiskInputMetricSet(35.0, 20.0, 20.0),
            FireWeatherIndexContext = new FireWeatherIndexContext(
                FireWeatherIndex: 65.377,
                KeetchByramDroughtIndex: 650.106,
                Provenance: "imported_reference",
                CalculationStatus: FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults,
                KbdiStatus: KbdiCalculationStatus.CompleteWithCandidateDefaults,
                Limitations: "antecedent_fwi_codes_candidate_defaults;antecedent_kbdi_candidate_default")
        };

        var assessment = _service.CreateAssessment(input);

        Assert.True(assessment.BaseRisk > 0.70);
        Assert.Equal("CompleteWithCandidateDefaults", assessment.CalculationStatus);
        Assert.Contains("antecedent_fwi_codes_candidate_defaults", assessment.Limitations);
        Assert.Contains("antecedent_kbdi_candidate_default", assessment.Limitations);
        Assert.Contains("FWI=", assessment.ExplanationSummary);
        Assert.Contains("KBDI=", assessment.ExplanationSummary);
        Assert.Contains("FireIndexProvenance=imported_reference", assessment.ExplanationSummary);
    }

    [Fact]
    public void CreateAssessment_MarksFwiAndKbdiAbsent_WhenNoIndexContextExists()
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 35.0));

        Assert.Contains("FWI=absent", assessment.ExplanationSummary);
        Assert.Contains("KBDI=absent", assessment.ExplanationSummary);
        Assert.Contains("FireIndexProvenance=absent", assessment.ExplanationSummary);
    }

    [Fact]
    public void CreateAssessment_Throws_WhenInputIsBlocked()
    {
        var blockedInput = new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: SensorMetricType.Temperature,
            Value: 30.0,
            Unit: MeasurementUnit.Celsius,
            EventTime: DateTimeOffset.UtcNow)
        {
            InputStatus = RiskInputStatus.Blocked,
            EligibilityReason = RiskEligibilityReason.MissingRequiredValue
        };

        var ex = Assert.Throws<InvalidOperationException>(() => _service.CreateAssessment(blockedInput));
        Assert.Contains("Blocked risk inputs", ex.Message);
    }

    private static RiskInput CreateRiskInput(
        SensorMetricType metricType,
        double value,
        MeasurementUnit unit = MeasurementUnit.Celsius)
    {
        return new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: metricType,
            Value: value,
            Unit: unit,
            EventTime: DateTimeOffset.UtcNow);
    }
}
