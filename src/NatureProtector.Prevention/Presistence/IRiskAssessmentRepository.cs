using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public interface IRiskAssessmentRepository
{
    Task AddAsync(Guid areaId, RiskAssessment assessment, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken);
}