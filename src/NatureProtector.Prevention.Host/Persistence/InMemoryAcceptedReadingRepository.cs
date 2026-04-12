using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Persistence;

public sealed class InMemoryAcceptedReadingRepository : IAcceptedReadingRepository
{
    private readonly List<EventEnvelope<SensorReadingProducedPayload>> _items = [];
    private readonly HashSet<Guid> _seenEventIds = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AddAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (!_seenEventIds.Add(envelope.EventId))
            {
                return;
            }

            _items.Add(envelope);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return _items.ToList().AsReadOnly();
        }
        finally
        {
            _gate.Release();
        }
    }
}
