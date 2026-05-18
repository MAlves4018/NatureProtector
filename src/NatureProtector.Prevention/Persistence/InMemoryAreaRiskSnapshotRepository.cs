using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed class InMemoryAreaRiskSnapshotRepository : IAreaRiskSnapshotRepository
{
    private readonly Dictionary<(Guid AreaId, Guid? SimulationRunId), AreaRiskSnapshot> _items = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        int assessmentCount,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            _items[(areaId, simulationRunId)] = snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AreaRiskSnapshot?> GetLatestAsync(
        Guid areaId,
        CancellationToken cancellationToken,
        Guid? simulationRunId = null)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (simulationRunId.HasValue)
            {
                return _items.TryGetValue((areaId, simulationRunId), out var runSnapshot)
                    ? runSnapshot
                    : null;
            }

            return _items
                .Where(item => item.Key.AreaId == areaId)
                .Select(item => item.Value)
                .OrderByDescending(item => item.Timestamp)
                .FirstOrDefault();
        }
        finally
        {
            _gate.Release();
        }
    }
}
