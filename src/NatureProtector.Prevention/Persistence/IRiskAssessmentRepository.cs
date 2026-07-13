using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public interface IRiskAssessmentRepository
{
    Task<RiskAssessment> AddAsync(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        RiskAssessment assessment,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null);

    Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RiskAssessment>> GetLatestByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null);
}
