using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Host.Projection;

public interface IAreaOperationalProjectionStore
{
    Task SaveCellAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken);

    Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null);
}
