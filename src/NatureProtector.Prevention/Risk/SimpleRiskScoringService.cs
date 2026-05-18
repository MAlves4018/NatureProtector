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
        var territory = Math.Clamp(input.TerritorialContext.StructuralHazardScore, 0.0, 1.0);
        var baseRisk = Math.Clamp((0.50 * meteorology) + (0.20 * dryness) + (0.30 * territory), 0.0, 1.0);
        var contextFactor = ResolveContextFactor(input.ObservationalConfidence);
        var integrityFactor = ResolveIntegrityFactor(input.OperationalIntegrity);
        var adjustedScore = Math.Clamp(
            baseRisk * contextFactor * integrityFactor,
            0.0,
            1.0);

        var explanation =
            $"Area={input.AreaId}; Sensor={input.SensorId}; Event={input.SourceEventId}; " +
            $"Metric={input.MetricType}; Value={input.Value:F2}; InputStatus={input.InputStatus}; " +
            $"M={meteorology:F2}; D={dryness:F2}; T={territory:F2}; " +
            $"BaseRisk={baseRisk:F2}; AdjustedScore={adjustedScore:F2}; " +
            $"C={contextFactor:F2}; I={integrityFactor:F2}; " +
            $"FWI={FormatOptional(input.FireWeatherIndexContext.FireWeatherIndex)}; " +
            $"KBDI={FormatOptional(input.FireWeatherIndexContext.KeetchByramDroughtIndex)}; " +
            $"FireIndexProvenance={input.FireWeatherIndexContext.Provenance}; " +
            $"ParameterSet={input.ParameterSetVersion} (non-calibrated).";

        return new RiskAssessment(
            id: Guid.NewGuid(),
            timestamp: input.EventTime,
            baseRisk: baseRisk,
            adjustedScore: adjustedScore,
            explanationSummary: explanation);
    }

    /// <summary>
    /// Calculates baseline risk in the range [0, 1] from metric/value.
    /// </summary>
    private static double CalculateMeteorologicalComponent(RiskInput input)
    {
        var weightedComponents = new List<(double Weight, double Score)>();

        if (input.Metrics.TemperatureCelsius is { } temperature)
        {
            weightedComponents.Add((0.40, CalculateMetricRisk(SensorMetricType.Temperature, temperature)));
        }

        if (input.Metrics.RelativeHumidityPercent is { } humidity)
        {
            weightedComponents.Add((0.35, CalculateMetricRisk(SensorMetricType.Humidity, humidity)));
        }

        if (input.Metrics.WindSpeedMetersPerSecond is { } windSpeed)
        {
            weightedComponents.Add((0.25, CalculateMetricRisk(SensorMetricType.WindSpeed, windSpeed)));
        }

        if (weightedComponents.Count == 0)
        {
            return ResolveWithFireWeatherIndex(
                CalculateMetricRisk(input.MetricType, input.Value),
                input.FireWeatherIndexContext.FireWeatherIndex);
        }

        var weightTotal = weightedComponents.Sum(item => item.Weight);
        var metricComponent = Math.Clamp(
            weightedComponents.Sum(item => item.Weight * item.Score) / weightTotal,
            0.0,
            1.0);
        return ResolveWithFireWeatherIndex(metricComponent, input.FireWeatherIndexContext.FireWeatherIndex);
    }

    private static double CalculateDrynessComponent(RiskInput input)
    {
        if (input.FireWeatherIndexContext.KeetchByramDroughtIndex is { } kbdi)
        {
            return Math.Clamp(kbdi / 800.0, 0.0, 1.0);
        }

        if (input.DailyCellState is null)
        {
            return 0.50;
        }

        var dryness = 0.50;

        if (input.DailyCellState.AntecedentState.Contains("dry", StringComparison.OrdinalIgnoreCase) ||
            input.DailyCellState.DroughtContext.Contains("dry", StringComparison.OrdinalIgnoreCase))
        {
            dryness = 0.70;
        }

        if (input.DailyCellState.DailyPrecipitationMillimeters is > 0.0)
        {
            dryness -= Math.Min(input.DailyCellState.DailyPrecipitationMillimeters.Value / 20.0, 0.30);
        }

        return Math.Clamp(dryness, 0.0, 1.0);
    }

    private static double ResolveWithFireWeatherIndex(double metricComponent, double? fireWeatherIndex)
    {
        if (!fireWeatherIndex.HasValue)
        {
            return metricComponent;
        }

        var fwiComponent = Math.Clamp(fireWeatherIndex.Value / 80.0, 0.0, 1.0);
        return Math.Clamp((0.70 * metricComponent) + (0.30 * fwiComponent), 0.0, 1.0);
    }

    private static string FormatOptional(double? value)
    {
        return value.HasValue ? value.Value.ToString("F3") : "absent";
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

        return Math.Clamp(baseRisk, 0.0, 1.0);
    }

    private static double ResolveContextFactor(ObservationalConfidenceLevel confidence)
    {
        // Candidate factors for V1 experimentation (not official calibration).
        return confidence switch
        {
            ObservationalConfidenceLevel.High => 1.00,
            ObservationalConfidenceLevel.Medium => 0.97,
            ObservationalConfidenceLevel.Low => 0.93,
            _ => 1.00
        };
    }

    private static double ResolveIntegrityFactor(OperationalIntegrityLevel integrity)
    {
        // Candidate factors for V1 experimentation (not official calibration).
        return integrity switch
        {
            OperationalIntegrityLevel.Intact => 1.00,
            OperationalIntegrityLevel.Degraded => 0.90,
            OperationalIntegrityLevel.Compromised => 0.80,
            _ => 1.00
        };
    }

}
