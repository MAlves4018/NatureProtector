using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public interface IAreaRiskSnapshotRepository
{
    Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken);

    Task<AreaRiskSnapshot?> GetLatestAsync(
        Guid areaId,
        CancellationToken cancellationToken);
}