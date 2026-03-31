using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed class InMemoryAreaRiskSnapshotRepository : IAreaRiskSnapshotRepository
{
    private readonly Dictionary<Guid, AreaRiskSnapshot> _items = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task SaveAsync(
        Guid areaId,
        AreaRiskSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            _items[areaId] = snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AreaRiskSnapshot?> GetLatestAsync(
        Guid areaId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return _items.TryGetValue(areaId, out var snapshot)
                ? snapshot
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }
}