using NatureProtector.Core.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Host.Projection;

public enum CycleObservationOutcome
{
    Eligible,
    Blocked
}

public sealed record FinalizedCycleProjection(
    Guid SimulationRunId,
    int CycleIndex,
    Guid AreaId,
    AreaRiskSnapshot? Snapshot,
    int EligibleCount,
    bool IsOperational,
    IReadOnlyList<Guid> EligibleEventIds,
    string? AggregationReason = null);

public interface ICycleProjectionCoordinator
{
    Task<IReadOnlyList<FinalizedCycleProjection>> RecordAsync(
        Guid simulationRunId,
        int cycleIndex,
        Guid areaId,
        Guid sensorId,
        Guid eventId,
        DateTimeOffset eventTime,
        MetricOrigin origin,
        CycleObservationOutcome outcome,
        RiskAssessment? assessment,
        CancellationToken cancellationToken);
}
