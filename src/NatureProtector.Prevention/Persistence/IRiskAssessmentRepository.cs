using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public interface IRiskAssessmentRepository
{
    Task AddAsync(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        RiskAssessment assessment,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RiskAssessment>> GetLatestByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken);
}
