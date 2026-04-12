using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed class InMemoryRiskAssessmentRepository : IRiskAssessmentRepository
{
    private readonly Dictionary<Guid, List<StoredAssessment>> _items = new();
    private readonly Dictionary<Guid, Dictionary<Guid, StoredAssessment>> _latestByArea = new();
    private readonly HashSet<Guid> _seenSourceEvents = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long _nextSequence;

    public async Task AddAsync(
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_seenSourceEvents.Contains(sourceEventId))
            {
                return;
            }

            if (!_items.TryGetValue(areaId, out var list))
            {
                list = [];
                _items[areaId] = list;
            }

            var stored = new StoredAssessment(
                sensorId,
                sourceEventId,
                assessment,
                ++_nextSequence);

            _seenSourceEvents.Add(sourceEventId);
            list.Add(stored);

            if (!_latestByArea.TryGetValue(areaId, out var latestBySensor))
            {
                latestBySensor = [];
                _latestByArea[areaId] = latestBySensor;
            }

            if (!latestBySensor.TryGetValue(sensorId, out var currentLatest) || IsMoreRecent(stored, currentLatest))
            {
                latestBySensor[sensorId] = stored;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<RiskAssessment>> GetByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_items.TryGetValue(areaId, out var list))
            {
                return Array.Empty<RiskAssessment>();
            }

            return list
                .OrderBy(x => x.Assessment.Timestamp)
                .ThenBy(x => x.Sequence)
                .Select(x => x.Assessment)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<RiskAssessment>> GetLatestByAreaAsync(
        Guid areaId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_latestByArea.TryGetValue(areaId, out var latestBySensor))
            {
                return Array.Empty<RiskAssessment>();
            }

            return latestBySensor.Values
                .OrderBy(x => x.Assessment.Timestamp)
                .ThenBy(x => x.Sequence)
                .Select(x => x.Assessment)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsMoreRecent(StoredAssessment candidate, StoredAssessment current)
    {
        return candidate.Assessment.Timestamp > current.Assessment.Timestamp ||
            (candidate.Assessment.Timestamp == current.Assessment.Timestamp &&
             candidate.Sequence > current.Sequence);
    }

    private sealed record StoredAssessment(
        Guid SensorId,
        Guid SourceEventId,
        RiskAssessment Assessment,
        long Sequence);
}
