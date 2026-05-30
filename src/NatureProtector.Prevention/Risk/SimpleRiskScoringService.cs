using NatureProtector.Core.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

/*
 * This service converts accepted readings into baseline and adjusted risk
 * assessments.
 *
 * Rationale:
 * - C5A introduces explicit BaseRisk and AdjustedScore while preserving the
 *   legacy RiskScore compatibility path used across the current pipeline.
 * - Scoring remains isolated so future model evolution does not force changes
 *   in orchestration components.
 *
 * Design considerations:
 * - BaseRisk follows the V1 candidate baseline:
 *   0.50 * meteorology + 0.20 * dryness + 0.30 * territory.
 * - AdjustedScore applies candidate (non-calibrated) confidence and integrity
 *   factors from risk input metadata.
 * - Blocked inputs are never converted into numeric risk assessments.
 */
public sealed class SimpleRiskScoringService : ISimpleRiskScoringService
{
    /// <summary>
    /// Creates a risk assessment from one eligible risk input.
    /// </summary>
    public RiskAssessment CreateAssessment(RiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.InputStatus == RiskInputStatus.Blocked)
        {
            throw new InvalidOperationException(
                "Blocked risk inputs cannot be converted into numeric assessments.");
        }

        var meteorology = CalculateMeteorologicalComponent(input);
        var dryness = CalculateDrynessComponent(input);
        var territory = CandidateParameterSetV1.ClampNormalized(input.TerritorialContext.TerritoryComponent);
        var baseRisk = CandidateParameterSetV1.ClampNormalized(
            (CandidateParameterSetV1.MeteorologyWeight * meteorology) +
            (CandidateParameterSetV1.DroughtWeight * dryness) +
            (CandidateParameterSetV1.TerritoryWeight * territory));
        var contextFactor = CandidateParameterSetV1.ResolveConfidenceFactor(input.ObservationalConfidence);
        var integrityFactor = CandidateParameterSetV1.ResolveIntegrityFactor(input.OperationalIntegrity);
        var adjustedScore = CandidateParameterSetV1.ClampNormalized(baseRisk * contextFactor * integrityFactor);
        var dominantDriver = DetermineDominantDriver(
            meteorology,
            dryness,
            territory,
            contextFactor,
            integrityFactor);
        var calculationStatus = DetermineCalculationStatus(input);
        var limitations = BuildLimitations(input);

        var explanation =
            $"Area={input.AreaId}; Sensor={input.SensorId}; Event={input.SourceEventId}; " +
            $"Metric={input.MetricType}; Value={input.Value:F2}; InputStatus={input.InputStatus}; " +
            $"M={meteorology:F2}; D={dryness:F2}; T={territory:F2}; " +
            $"H={input.TerritorialContext.HazardComponent:F2}; " +
            $"F={input.TerritorialContext.FuelComponent:F2}; " +
            $"G={input.TerritorialContext.GeomorphologyComponent:F2}; " +
            $"BaseRisk={baseRisk:F2}; AdjustedScore={adjustedScore:F2}; " +
            $"C={contextFactor:F2}; I={integrityFactor:F2}; " +
            $"TerritorySource={input.TerritorialContext.Source}; " +
            $"TerritoryLimitation={input.TerritorialContext.Limitation ?? "none"}; " +
            $"FWI={FormatOptional(input.FireWeatherIndexContext.FireWeatherIndex)}; " +
            $"NormalizedFWI={FormatOptional(input.FireWeatherIndexContext.NormalizedFireWeatherIndex)}; " +
            $"FWIStatus={input.FireWeatherIndexContext.CalculationStatus}; " +
            $"KBDI={FormatOptional(input.FireWeatherIndexContext.KeetchByramDroughtIndex)}; " +
            $"NormalizedKBDI={FormatOptional(input.FireWeatherIndexContext.NormalizedKeetchByramDroughtIndex)}; " +
            $"KBDIStatus={input.FireWeatherIndexContext.KbdiCalculationStatus}; " +
            $"FireIndexProvenance={input.FireWeatherIndexContext.Provenance}; " +
            $"DominantDriver={dominantDriver}; CalculationStatus={calculationStatus}; " +
            $"Limitations={limitations ?? "none"}; " +
            $"Score100={CandidateParameterSetV1.ToScore100(adjustedScore)}; " +
            $"ParameterSet={input.ParameterSetVersion} (non-calibrated).";

        return new RiskAssessment(
            id: Guid.NewGuid(),
            timestamp: input.EventTime,
            baseRisk: baseRisk,
            adjustedScore: adjustedScore,
            explanationSummary: explanation,
            meteorologyComponent: meteorology,
            droughtComponent: dryness,
            territoryComponent: territory,
            hazardComponent: CandidateParameterSetV1.ClampNormalized(input.TerritorialContext.HazardComponent),
            fuelComponent: CandidateParameterSetV1.ClampNormalized(input.TerritorialContext.FuelComponent),
            geomorphologyComponent: CandidateParameterSetV1.ClampNormalized(input.TerritorialContext.GeomorphologyComponent),
            confidenceFactor: contextFactor,
            integrityFactor: integrityFactor,
            dominantDriver: dominantDriver,
            parameterSetVersion: input.ParameterSetVersion,
            calculationStatus: calculationStatus,
            limitations: limitations);
    }

    /// <summary>
    /// Calculates baseline risk in the range [0, 1] from metric/value.
    /// </summary>
    private static double CalculateMeteorologicalComponent(RiskInput input)
    {
        var weightedComponents = new List<(double Weight, double Score)>();

        if (input.Metrics.TemperatureCelsius is { } temperature)
        {
            weightedComponents.Add((CandidateParameterSetV1.TemperatureMetricWeight, CalculateMetricRisk(SensorMetricType.Temperature, temperature)));
        }

        if (input.Metrics.RelativeHumidityPercent is { } humidity)
        {
            weightedComponents.Add((CandidateParameterSetV1.HumidityMetricWeight, CalculateMetricRisk(SensorMetricType.Humidity, humidity)));
        }

        if (input.Metrics.WindSpeedMetersPerSecond is { } windSpeed)
        {
            weightedComponents.Add((CandidateParameterSetV1.WindMetricWeight, CalculateMetricRisk(SensorMetricType.WindSpeed, windSpeed)));
        }

        if (weightedComponents.Count == 0)
        {
            return ResolveWithFireWeatherIndex(
                CalculateMetricRisk(input.MetricType, input.Value),
                input.FireWeatherIndexContext);
        }

        var weightTotal = weightedComponents.Sum(item => item.Weight);
        var metricComponent = CandidateParameterSetV1.ClampNormalized(
            weightedComponents.Sum(item => item.Weight * item.Score) / weightTotal);
        return ResolveWithFireWeatherIndex(metricComponent, input.FireWeatherIndexContext);
    }

    private static double CalculateDrynessComponent(RiskInput input)
    {
        if (IsUsable(input.FireWeatherIndexContext.KbdiCalculationStatus) &&
            input.FireWeatherIndexContext.NormalizedKeetchByramDroughtIndex is { } normalizedKbdi)
        {
            return CandidateParameterSetV1.ClampNormalized(normalizedKbdi);
        }

        if (input.DailyCellState is null)
        {
            return CandidateParameterSetV1.MetricFallbackDryness;
        }

        var dryness = CandidateParameterSetV1.MetricFallbackDryness;

        if (input.DailyCellState.AntecedentState.Contains("dry", StringComparison.OrdinalIgnoreCase) ||
            input.DailyCellState.DroughtContext.Contains("dry", StringComparison.OrdinalIgnoreCase))
        {
            dryness = CandidateParameterSetV1.DryAntecedentDryness;
        }

        if (input.DailyCellState.DailyPrecipitationMillimeters is > 0.0)
        {
            dryness -= Math.Min(
                input.DailyCellState.DailyPrecipitationMillimeters.Value /
                CandidateParameterSetV1.PrecipitationReductionReferenceMillimeters,
                CandidateParameterSetV1.MaximumPrecipitationDrynessReduction);
        }

        return CandidateParameterSetV1.ClampNormalized(dryness);
    }

    private static double ResolveWithFireWeatherIndex(
        double metricComponent,
        FireWeatherIndexContext fireWeatherIndexContext)
    {
        if (!IsUsable(fireWeatherIndexContext.CalculationStatus) ||
            !fireWeatherIndexContext.NormalizedFireWeatherIndex.HasValue)
        {
            return metricComponent;
        }

        var fwiComponent = CandidateParameterSetV1.ClampNormalized(
            fireWeatherIndexContext.NormalizedFireWeatherIndex.Value);
        return CandidateParameterSetV1.ClampNormalized(
            (CandidateParameterSetV1.FireWeatherIndexMetricBlendWeight * metricComponent) +
            (CandidateParameterSetV1.FireWeatherIndexBlendWeight * fwiComponent));
    }

    private static string DetermineDominantDriver(
        double meteorology,
        double dryness,
        double territory,
        double confidenceFactor,
        double integrityFactor)
    {
        if (confidenceFactor < 0.95 || integrityFactor < 0.95)
        {
            return "QualityPenalty";
        }

        var drivers = new[]
        {
            ("Meteorology", CandidateParameterSetV1.MeteorologyWeight * meteorology),
            ("Drought", CandidateParameterSetV1.DroughtWeight * dryness),
            ("Territory", CandidateParameterSetV1.TerritoryWeight * territory)
        }
        .OrderByDescending(item => item.Item2)
        .ToArray();

        return Math.Abs(drivers[0].Item2 - drivers[1].Item2) <= 0.03
            ? "Mixed"
            : drivers[0].Item1;
    }

    private static string DetermineCalculationStatus(RiskInput input)
    {
        var fwiComplete = IsUsable(input.FireWeatherIndexContext.CalculationStatus);
        var kbdiComplete = IsUsable(input.FireWeatherIndexContext.KbdiCalculationStatus);

        if (input.InputStatus == RiskInputStatus.PartialButUsable)
        {
            return "PartialButUsable";
        }

        if (fwiComplete && kbdiComplete)
        {
            return input.FireWeatherIndexContext.CalculationStatus == FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults ||
                input.FireWeatherIndexContext.KbdiCalculationStatus == KbdiCalculationStatus.CompleteWithCandidateDefaults ||
                input.FireWeatherIndexContext.KbdiCalculationStatus == KbdiCalculationStatus.LimitedAntecedentHistory
                ? "CompleteWithCandidateDefaults"
                : "Complete";
        }

        return "CandidateFallback";
    }

    private static string? BuildLimitations(RiskInput input)
    {
        var limitations = new List<string>();

        if (!string.IsNullOrWhiteSpace(input.TerritorialContext.Limitation))
        {
            limitations.Add(input.TerritorialContext.Limitation);
        }

        if (!string.IsNullOrWhiteSpace(input.FireWeatherIndexContext.Limitations))
        {
            limitations.AddRange(input.FireWeatherIndexContext.Limitations.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (!IsUsable(input.FireWeatherIndexContext.CalculationStatus))
        {
            limitations.Add($"FWI={input.FireWeatherIndexContext.CalculationStatus}");
        }

        if (!IsUsable(input.FireWeatherIndexContext.KbdiCalculationStatus))
        {
            limitations.Add($"KBDI={input.FireWeatherIndexContext.KbdiCalculationStatus}");
        }

        return limitations.Count == 0 ? null : string.Join("; ", limitations);
    }

    private static string FormatOptional(double? value)
    {
        return value.HasValue ? value.Value.ToString("F3") : "absent";
    }

    private static bool IsUsable(FireWeatherIndexCalculationStatus status)
    {
        return status is FireWeatherIndexCalculationStatus.Complete or
            FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults;
    }

    private static bool IsUsable(KbdiCalculationStatus status)
    {
        return status is KbdiCalculationStatus.Complete or
            KbdiCalculationStatus.CompleteWithCandidateDefaults or
            KbdiCalculationStatus.LimitedAntecedentHistory or
            KbdiCalculationStatus.CalculatedFromHistory or
            KbdiCalculationStatus.ReferenceImported;
    }

    private static double CalculateMetricRisk(SensorMetricType metricType, double value)
    {
        var baseRisk = metricType switch
        {
            SensorMetricType.Temperature => value switch
            {
                < 20.0 => 0.10,
                < 25.0 => 0.20,
                < 30.0 => 0.40,
                < 35.0 => 0.65,
                < 40.0 => 0.85,
                _ => 1.00
            },

            SensorMetricType.Humidity => value switch
            {
                >= 70.0 => 0.05,
                >= 50.0 => 0.20,
                >= 35.0 => 0.40,
                >= 20.0 => 0.70,
                _ => 0.95
            },

            SensorMetricType.WindSpeed => value switch
            {
                < 5.0 => 0.10,
                < 10.0 => 0.30,
                < 15.0 => 0.55,
                < 20.0 => 0.75,
                _ => 0.95
            },

            _ => 0.20
        };

        return CandidateParameterSetV1.ClampNormalized(baseRisk);
    }

}
