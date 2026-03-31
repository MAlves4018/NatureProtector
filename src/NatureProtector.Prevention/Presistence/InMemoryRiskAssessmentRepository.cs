using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed class InMemoryRiskAssessmentRepository : IRiskAssessmentRepository
{
    private readonly Dictionary<Guid, List<RiskAssessment>> _items = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AddAsync(
        Guid areaId,
        RiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_items.TryGetValue(areaId, out var list))
            {
                list = [];
                _items[areaId] = list;
            }

            list.Add(assessment);
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
                .OrderBy(x => x.Timestamp)
                .ToList()
                .AsReadOnly();
        }
        finally
        {
            _gate.Release();
        }
    }
}