using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Host.Projection;

public sealed record AreaProjectionWriteResult(
    DateTimeOffset ProjectedAt,
    DateTimeOffset? AlertedAt);

public interface IAreaOperationalProjectionStore
{
    Task SaveCellAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken);

    Task<AreaProjectionWriteResult> SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null,
        int? cycleIndex = null);

    Task MarkUnavailableAsync(
        Guid areaId,
        DateTimeOffset snapshotTimestamp,
        string reason,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null,
        int? cycleIndex = null);
}
