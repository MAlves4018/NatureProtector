using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public interface IAreaRiskSnapshotRepository
{
    Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null);

    Task<AreaRiskSnapshot?> GetLatestAsync(
        Guid areaId,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null);
}
