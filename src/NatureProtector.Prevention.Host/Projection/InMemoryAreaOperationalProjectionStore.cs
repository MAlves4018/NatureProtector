using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Host.Projection;

public sealed class InMemoryAreaOperationalProjectionStore : IAreaOperationalProjectionStore
{
    private readonly Dictionary<Guid, InMemoryAreaOperationalState> _states = new();
    private readonly Dictionary<Guid, InMemoryCellOperationalState> _cellStates = new();
    private readonly Dictionary<Guid, InMemoryAlertState> _alerts = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IReadOnlyCollection<InMemoryAreaOperationalState> States => _states.Values.ToArray();
    public IReadOnlyCollection<InMemoryCellOperationalState> CellStates => _cellStates.Values.ToArray();
    public IReadOnlyCollection<InMemoryAlertState> Alerts => _alerts.Values.ToArray();

    public async Task SaveCellAsync(
        Guid areaId,
        Guid sensorId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var updatedAt = DateTimeOffset.UtcNow;
            var severity = SeverityExtensions.FromRiskLevel(assessment.RiskLevel);

            _cellStates[sensorId] = new InMemoryCellOperationalState(
                areaId,
                sensorId,
                assessment.Timestamp,
                assessment.RiskScore,
                assessment.RiskLevel.ToString(),
                severity.ToString(),
                assessment.ExplanationSummary,
                updatedAt);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var updatedAt = DateTimeOffset.UtcNow;
            var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);
            var previousAdjustedScore = _states.TryGetValue(areaId, out var existingStateBeforeUpdate)
                ? existingStateBeforeUpdate.AggregateRiskScore
                : snapshot.AggregateRiskScore;
            var hasOpenAlert = _alerts.TryGetValue(areaId, out var existingAlert) && existingAlert.Status == "Open";
            var existingOpenAlert = hasOpenAlert
                ? existingAlert
                : null;
            var currentState = V1AlertPolicy.InferCurrentState(
                hasOpenAlert,
                previousAdjustedScore);
            var pendingState = existingStateBeforeUpdate is null
                ? V1AlertState.None
                : Enum.TryParse<V1AlertState>(existingStateBeforeUpdate.PendingAlertState, out var parsedPendingState)
                    ? parsedPendingState
                    : V1AlertState.None;
            var decision = V1AlertPolicy.EvaluateTransition(
                currentState,
                snapshot.AggregateRiskScore,
                pendingState,
                existingStateBeforeUpdate?.PendingAlertCycles ?? 0,
                snapshot.Timestamp,
                existingStateBeforeUpdate?.AlertCooldownUntil,
                TimeSpan.FromSeconds(60));
            var nextState = decision.State;

            _states[areaId] = new InMemoryAreaOperationalState(
                areaId,
                simulationRunId,
                snapshot.Timestamp,
                snapshot.AggregateRiskScore,
                snapshot.AggregateRiskLevel.ToString(),
                severity.ToString(),
                snapshot.Summary,
                assessmentCount,
                decision.PendingState.ToString(),
                decision.PendingCycles,
                decision.CooldownUntil,
                updatedAt);

            if (nextState is V1AlertState.Warning or V1AlertState.Alarm)
            {
                _alerts[areaId] = new InMemoryAlertState(
                    areaId,
                    "area-risk-high",
                    nextState.ToString(),
                    severity.ToString(),
                    "Open",
                    BuildAlertMessage(snapshot, nextState),
                    existingOpenAlert is null ? snapshot.Timestamp : existingOpenAlert.TriggeredAt,
                    updatedAt,
                    null);
            }
            else if (existingAlert is not null)
            {
                _alerts[areaId] = existingAlert with
                {
                    Status = "Resolved",
                    UpdatedAt = updatedAt,
                    ResolvedAt = updatedAt
                };
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string BuildAlertMessage(AreaRiskSnapshot snapshot, V1AlertState state)
        => $"AlertState={state}; Area risk is {snapshot.AggregateRiskLevel} with adjusted score {snapshot.AggregateRiskScore:F2}. Candidate Parameter Set V1.0 (non-official).";

    public sealed record InMemoryAreaOperationalState(
        Guid AreaId,
        Guid? SimulationRunId,
        DateTimeOffset SnapshotTimestamp,
        double AggregateRiskScore,
        string AggregateRiskLevel,
        string Severity,
        string? Summary,
        int AssessmentCount,
        string PendingAlertState,
        int PendingAlertCycles,
        DateTimeOffset? AlertCooldownUntil,
        DateTimeOffset UpdatedAt);

    public sealed record InMemoryCellOperationalState(
        Guid AreaId,
        Guid SensorId,
        DateTimeOffset SnapshotTimestamp,
        double RiskScore,
        string RiskLevel,
        string Severity,
        string? Summary,
        DateTimeOffset UpdatedAt);

    public sealed record InMemoryAlertState(
        Guid AreaId,
        string AlertCode,
        string AlertState,
        string Severity,
        string Status,
        string Message,
        DateTimeOffset TriggeredAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? ResolvedAt);
}
