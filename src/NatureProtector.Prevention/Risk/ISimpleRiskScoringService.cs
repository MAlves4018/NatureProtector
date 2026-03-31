using NatureProtector.Core.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public interface ISimpleRiskScoringService
{
    RiskAssessment CreateAssessment(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        SensorMetricType metricType,
        double value,
        DateTimeOffset assessedAt);
}