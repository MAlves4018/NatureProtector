using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

/*
 * Esta fronteira separa a leitura já aceite da decisão de elegibilidade para
 * o motor de risco.
 *
 * Design note:
 * - A baseline atual mantém compatibilidade, mas já distingue leituras
 *   completas, degradadas (ainda utilizáveis) e bloqueadas.
 * - O objetivo desta camada é criar um ponto explícito para futuras regras de
 *   suporte de métricas, unidades, janelas temporais e requisitos de dados,
 *   sem acoplar essas decisões ao envelope ou ao scoring service.
 */
public sealed class RiskEligibilityService : IRiskEligibilityService
{
    public Task<RiskEligibilityResult> EvaluateAsync(
        NormalizedReading reading,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);
        cancellationToken.ThrowIfCancellationRequested();

        if (reading.AreaId == Guid.Empty ||
            reading.SensorId == Guid.Empty ||
            double.IsNaN(reading.Value) ||
            double.IsInfinity(reading.Value))
        {
            return Task.FromResult(RiskEligibilityResult.Blocked(
                RiskEligibilityReason.MissingRequiredValue,
                "Reading is missing critical data required for risk assessment.",
                ["MissingValue"]));
        }

        if (!Enum.IsDefined(reading.OperationalState) ||
            reading.OperationalState is SensorOperationalState.Invalid or SensorOperationalState.Dropped)
        {
            return Task.FromResult(RiskEligibilityResult.Blocked(
                RiskEligibilityReason.InvalidOperationalState,
                "Operational state is not eligible for risk processing.",
                ["SemanticMismatch"]));
        }

        if (!Enum.IsDefined(reading.MetricType) ||
            reading.MetricType == SensorMetricType.WindDirection)
        {
            return Task.FromResult(RiskEligibilityResult.Blocked(
                RiskEligibilityReason.UnsupportedMetric,
                "Metric is not supported by the current risk model.",
                ["UnsupportedMetric"]));
        }

        if (!Enum.IsDefined(reading.Unit) ||
            !IsSupportedMetricUnit(reading.MetricType, reading.Unit))
        {
            return Task.FromResult(RiskEligibilityResult.Blocked(
                RiskEligibilityReason.InvalidUnit,
                "Metric/unit combination is not supported by the current risk model.",
                ["InvalidUnit"]));
        }

        if (reading.OperationalState is SensorOperationalState.Delayed or SensorOperationalState.Retransmitted)
        {
            var isDelayed = reading.OperationalState == SensorOperationalState.Delayed;
            var qualityFlag = isDelayed ? "Delayed" : "Duplicate";
            var reasonCode = isDelayed
                ? RiskEligibilityReason.DelayedReading
                : RiskEligibilityReason.RetransmittedReading;

            return Task.FromResult(RiskEligibilityResult.PartialButUsable(
                reasonCode,
                "Reading is degraded but still usable for risk assessment.",
                [qualityFlag]));
        }

        return Task.FromResult(RiskEligibilityResult.CompleteEligible());
    }

    private static bool IsSupportedMetricUnit(
        SensorMetricType metricType,
        MeasurementUnit unit)
    {
        return metricType switch
        {
            SensorMetricType.Temperature => unit == MeasurementUnit.Celsius,
            SensorMetricType.Humidity => unit == MeasurementUnit.Percent,
            SensorMetricType.WindSpeed => unit == MeasurementUnit.MetersPerSecond,
            _ => false
        };
    }
}
