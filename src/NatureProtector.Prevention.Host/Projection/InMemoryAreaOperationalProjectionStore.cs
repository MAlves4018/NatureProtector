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
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var updatedAt = DateTimeOffset.UtcNow;
            var severity = SeverityExtensions.FromRiskLevel(snapshot.AggregateRiskLevel);

            _states[areaId] = new InMemoryAreaOperationalState(
                areaId,
                snapshot.Timestamp,
                snapshot.AggregateRiskScore,
                snapshot.AggregateRiskLevel.ToString(),
                severity.ToString(),
                snapshot.Summary,
                assessmentCount,
                updatedAt);

            if (snapshot.AggregateRiskLevel.IsHighOrAbove())
            {
                _alerts[areaId] = new InMemoryAlertState(
                    areaId,
                    "area-risk-high",
                    severity.ToString(),
                    "Open",
                    BuildAlertMessage(snapshot),
                    _alerts.TryGetValue(areaId, out var existing) ? existing.TriggeredAt : snapshot.Timestamp,
                    updatedAt,
                    null);
            }
            else if (_alerts.TryGetValue(areaId, out var existingAlert))
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

    private static string BuildAlertMessage(AreaRiskSnapshot snapshot)
        => $"Area risk is {snapshot.AggregateRiskLevel} with score {snapshot.AggregateRiskScore:F2}.";

    public sealed record InMemoryAreaOperationalState(
        Guid AreaId,
        DateTimeOffset SnapshotTimestamp,
        double AggregateRiskScore,
        string AggregateRiskLevel,
        string Severity,
        string? Summary,
        int AssessmentCount,
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
        string Severity,
        string Status,
        string Message,
        DateTimeOffset TriggeredAt,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? ResolvedAt);
}
