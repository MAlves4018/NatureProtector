using NatureProtector.Core.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

/*
 * Este serviço converte leituras aceites em avaliações de risco elementares.
 *
 * Rationale:
 * - A pipeline de prevenção precisa de uma heurística explícita e testável para
 *   transformar métricas dos sensores em risco operacional.
 * - Manter esta lógica isolada facilita a futura substituição por modelos mais
 *   ricos sem mexer na orquestração da pipeline.
 *
 * Design considerations:
 * - A implementação atual usa thresholds simples por tipo de métrica.
 * - A explicação textual resume os dados usados para que o resultado fique mais
 *   auditável em logs e persistência.
 */

public sealed class SimpleRiskScoringService : ISimpleRiskScoringService
{
    /// <summary>
    /// Cria uma avaliação de risco a partir de uma leitura já aceite pela
    /// pipeline.
    /// </summary>
    public RiskAssessment CreateAssessment(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        SensorMetricType metricType,
        double value,
        DateTimeOffset assessedAt)
    {
        var score = CalculateScore(metricType, value);

        var explanation =
            $"Area={areaId}; Sensor={sensorId}; Event={sourceEventId}; " +
            $"Metric={metricType}; Value={value:F2}; Score={score:F2}.";

        return new RiskAssessment(
            id: Guid.NewGuid(),
            timestamp: assessedAt,
            riskScore: score,
            explanationSummary: explanation);
    }

    /// <summary>
    /// Calcula um score normalizado entre 0 e 1 para a métrica indicada.
    /// </summary>
    private static double CalculateScore(SensorMetricType metricType, double value)
    {
        var score = metricType switch
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

        return Math.Clamp(score, 0.0, 1.0);
    }
}
