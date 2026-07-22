using FsCheck.Xunit;
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
    public void CreateAssessment_BlendsUsableFwiWithMetricComponent()
    {
        var input = CreateRiskInput(SensorMetricType.Temperature, 30.0) with
        {
            FireWeatherIndexContext = new FireWeatherIndexContext(
                FireWeatherIndex: 8.0,
                KeetchByramDroughtIndex: null,
                Provenance: "candidate-fwi",
                NormalizedFireWeatherIndex: 0.10,
                CalculationStatus: FireWeatherIndexCalculationStatus.Complete,
                KbdiStatus: KbdiCalculationStatus.Missing)
        };

        var assessment = _service.CreateAssessment(input);

        Assert.Equal(0.485, assessment.MeteorologyComponent, precision: 3);
        Assert.Equal(0.493, assessment.BaseRisk, precision: 3);
        Assert.Equal("CandidateFallback", assessment.CalculationStatus);
    }

    [Fact]
    public void CreateAssessment_UsesDrynessSignalsAndPrecipitationReduction()
    {
        var dryAntecedent = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 30.0) with
        {
            DailyCellState = CreateDailyState(antecedentState: "dry", droughtContext: "normal", precipitation: 0.0)
        });
        var dryContext = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 30.0) with
        {
            DailyCellState = CreateDailyState(antecedentState: "normal", droughtContext: "dry", precipitation: 0.0)
        });
        var rainAfterDryness = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 30.0) with
        {
            DailyCellState = CreateDailyState(antecedentState: "dry", droughtContext: "dry", precipitation: 10.0)
        });

        Assert.Equal(0.70, dryAntecedent.DroughtComponent, precision: 3);
        Assert.Equal(0.70, dryContext.DroughtComponent, precision: 3);
        Assert.Equal(0.40, rainAfterDryness.DroughtComponent, precision: 3);
    }

    [Fact]
    public void CreateAssessment_ReportsMixedDominantDriverAtCandidateTieBoundary()
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 25.0) with
        {
            TerritorialContext = new TerritorialRiskContext(Guid.NewGuid(), "tie-boundary", 0.5666666667)
        });

        Assert.Equal("Mixed", assessment.DominantDriver);
    }

    [Fact]
    public void CreateAssessment_DistinguishesPartialCandidateAndDefaultedCalculationStatuses()
    {
        var partial = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 35.0) with
        {
            InputStatus = RiskInputStatus.PartialButUsable,
            FireWeatherIndexContext = new FireWeatherIndexContext(
                FireWeatherIndex: 40.0,
                KeetchByramDroughtIndex: 400.0,
                Provenance: "candidate-index",
                CalculationStatus: FireWeatherIndexCalculationStatus.Complete,
                KbdiStatus: KbdiCalculationStatus.Complete)
        });
        var oneMissingIndex = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 35.0) with
        {
            FireWeatherIndexContext = new FireWeatherIndexContext(
                FireWeatherIndex: 40.0,
                KeetchByramDroughtIndex: null,
                Provenance: "candidate-index",
                CalculationStatus: FireWeatherIndexCalculationStatus.Complete,
                KbdiStatus: KbdiCalculationStatus.Missing)
        });
        var defaulted = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 35.0) with
        {
            FireWeatherIndexContext = new FireWeatherIndexContext(
                FireWeatherIndex: 40.0,
                KeetchByramDroughtIndex: 400.0,
                Provenance: "candidate-index",
                CalculationStatus: FireWeatherIndexCalculationStatus.Complete,
                KbdiStatus: KbdiCalculationStatus.LimitedAntecedentHistory)
        });

        Assert.Equal("PartialButUsable", partial.CalculationStatus);
        Assert.Equal("CandidateFallback", oneMissingIndex.CalculationStatus);
        Assert.Equal("CompleteWithCandidateDefaults", defaulted.CalculationStatus);
    }

    [Fact]
    public void CreateAssessment_PreservesTerritoryIndexAndMissingStatusLimitations()
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 35.0) with
        {
            TerritorialContext = new TerritorialRiskContext(
                Guid.NewGuid(),
                "test",
                0.50)
            {
                Limitation = "territory_candidate_default"
            },
            FireWeatherIndexContext = new FireWeatherIndexContext(
                FireWeatherIndex: null,
                KeetchByramDroughtIndex: null,
                Provenance: "candidate-index",
                CalculationStatus: FireWeatherIndexCalculationStatus.Missing,
                KbdiStatus: KbdiCalculationStatus.Missing,
                Limitations: " fwi_missing ; kbdi_missing ")
        });

        Assert.Contains("territory_candidate_default", assessment.Limitations);
        Assert.Contains("fwi_missing", assessment.Limitations);
        Assert.Contains("kbdi_missing", assessment.Limitations);
        Assert.Contains("FWI=Missing", assessment.Limitations);
        Assert.Contains("KBDI=Missing", assessment.Limitations);
        Assert.DoesNotContain(" ;", assessment.Limitations);
    }

    [Fact]
    public void CreateAssessment_MarksFwiAndKbdiAbsent_WhenNoIndexContextExists()
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, 35.0));

        Assert.Contains("FWI=absent", assessment.ExplanationSummary);
        Assert.Contains("KBDI=absent", assessment.ExplanationSummary);
        Assert.Contains("FireIndexProvenance=absent", assessment.ExplanationSummary);
        Assert.Contains("TerritoryLimitation=territorial_context_missing_candidate_defaults", assessment.ExplanationSummary);
        Assert.Contains(
            "Limitations=territorial_context_missing_candidate_defaults; FWI=Missing; KBDI=Missing",
            assessment.ExplanationSummary);
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

    [Property(MaxTest = 100)]
    public bool CreateAssessment_KeepsScoresBoundedAndDeterministic(double rawValue)
    {
        var input = CreateRiskInput(
            SensorMetricType.Temperature,
            NormalizeFinite(rawValue, -50.0, 70.0));

        var first = _service.CreateAssessment(input);
        var second = _service.CreateAssessment(input);

        return IsNormalized(first.BaseRisk) &&
            IsNormalized(first.AdjustedScore) &&
            IsNormalized(first.RiskScore) &&
            IsNormalized(first.MeteorologyComponent) &&
            IsNormalized(first.DroughtComponent) &&
            IsNormalized(first.TerritoryComponent) &&
            IsNormalized(first.HazardComponent) &&
            IsNormalized(first.FuelComponent) &&
            IsNormalized(first.GeomorphologyComponent) &&
            first.BaseRisk == second.BaseRisk &&
            first.AdjustedScore == second.AdjustedScore &&
            first.Score100 == second.Score100 &&
            first.DominantDriver == second.DominantDriver &&
            first.CalculationStatus == second.CalculationStatus;
    }

    [Property(MaxTest = 100)]
    public bool CreateAssessment_TemperatureAndWindIncrease_DoNotReduceScore(double rawA, double rawB)
    {
        var low = Math.Min(
            NormalizeFinite(rawA, -30.0, 70.0),
            NormalizeFinite(rawB, -30.0, 70.0));
        var high = Math.Max(
            NormalizeFinite(rawA, -30.0, 70.0),
            NormalizeFinite(rawB, -30.0, 70.0));

        var lowTemperature = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, low));
        var highTemperature = _service.CreateAssessment(CreateRiskInput(SensorMetricType.Temperature, high));
        var lowWind = _service.CreateAssessment(CreateRiskInput(SensorMetricType.WindSpeed, low, MeasurementUnit.MetersPerSecond));
        var highWind = _service.CreateAssessment(CreateRiskInput(SensorMetricType.WindSpeed, high, MeasurementUnit.MetersPerSecond));

        return highTemperature.BaseRisk >= lowTemperature.BaseRisk &&
            highWind.BaseRisk >= lowWind.BaseRisk;
    }

    [Property(MaxTest = 100)]
    public bool CreateAssessment_HumidityIncrease_DoesNotIncreaseScore(double rawA, double rawB)
    {
        var lowHumidity = Math.Min(
            NormalizeFinite(rawA, 0.0, 100.0),
            NormalizeFinite(rawB, 0.0, 100.0));
        var highHumidity = Math.Max(
            NormalizeFinite(rawA, 0.0, 100.0),
            NormalizeFinite(rawB, 0.0, 100.0));

        var lowHumidityAssessment = _service.CreateAssessment(CreateRiskInput(
            SensorMetricType.Humidity,
            lowHumidity,
            MeasurementUnit.Percent));
        var highHumidityAssessment = _service.CreateAssessment(CreateRiskInput(
            SensorMetricType.Humidity,
            highHumidity,
            MeasurementUnit.Percent));

        return lowHumidityAssessment.BaseRisk >= highHumidityAssessment.BaseRisk;
    }

    [Property(MaxTest = 100)]
    public bool CreateAssessment_TerritorialHazardIncrease_DoesNotReduceScore(double rawA, double rawB)
    {
        var low = Math.Min(
            NormalizeFinite(rawA, 0.0, 1.0),
            NormalizeFinite(rawB, 0.0, 1.0));
        var high = Math.Max(
            NormalizeFinite(rawA, 0.0, 1.0),
            NormalizeFinite(rawB, 0.0, 1.0));
        var baseInput = CreateRiskInput(SensorMetricType.Temperature, 30.0);
        var lowAssessment = _service.CreateAssessment(baseInput with
        {
            TerritorialContext = new TerritorialRiskContext(Guid.NewGuid(), "property-test", low)
        });
        var highAssessment = _service.CreateAssessment(baseInput with
        {
            TerritorialContext = new TerritorialRiskContext(Guid.NewGuid(), "property-test", high)
        });

        return highAssessment.BaseRisk >= lowAssessment.BaseRisk;
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

    private static DailyCellState CreateDailyState(
        string antecedentState,
        string droughtContext,
        double precipitation)
    {
        return new DailyCellState(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            day: DateTimeOffset.UtcNow,
            antecedentState: antecedentState,
            candidateParameterSetVersion: CandidateParameterSetV1.Version,
            provenance: "scoring-test",
            lastUpdatedAt: DateTimeOffset.UtcNow,
            dailyPrecipitationMillimeters: precipitation,
            droughtContext: droughtContext);
    }

    private static double NormalizeFinite(double value, double min, double max)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, min, max)
            : min;
    }

    private static bool IsNormalized(double value)
    {
        return value is >= 0.0 and <= 1.0;
    }
}
