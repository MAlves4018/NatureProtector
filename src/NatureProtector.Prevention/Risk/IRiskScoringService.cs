using NatureProtector.Core.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

/// <summary>
/// Contract for converting one accepted reading into one operational risk assessment.
/// </summary>
public interface IRiskScoringService
{
    RiskAssessment CreateAssessment(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        SensorMetricType metricType,
        double value,
        DateTimeOffset assessedAt);
}
