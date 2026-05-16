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
 * - BaseRisk keeps the previous metric-threshold mapping.
 * - AdjustedScore applies candidate (non-calibrated) contextual factors from
 *   risk input metadata.
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

        var baseRisk = CalculateBaseRisk(input.MetricType, input.Value);
        var contextFactor = ResolveContextFactor(input.ObservationalConfidence);
        var integrityFactor = ResolveIntegrityFactor(input.OperationalIntegrity);
        var eligibilityFactor = ResolveEligibilityFactor(input.InputStatus);
        var adjustedScore = Math.Clamp(
            baseRisk * contextFactor * integrityFactor * eligibilityFactor,
            0.0,
            1.0);

        var explanation =
            $"Area={input.AreaId}; Sensor={input.SensorId}; Event={input.SourceEventId}; " +
            $"Metric={input.MetricType}; Value={input.Value:F2}; " +
            $"BaseRisk={baseRisk:F2}; AdjustedScore={adjustedScore:F2}; " +
            $"C={contextFactor:F2}; I={integrityFactor:F2}; EligibilityFactor={eligibilityFactor:F2}; " +
            "ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).";

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
    private static double CalculateBaseRisk(SensorMetricType metricType, double value)
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

    private static double ResolveEligibilityFactor(RiskInputStatus status)
    {
        return status switch
        {
            RiskInputStatus.CompleteEligible => 1.00,
            RiskInputStatus.PartialButUsable => 0.95,
            RiskInputStatus.Blocked => 0.0,
            _ => 1.00
        };
    }
}
