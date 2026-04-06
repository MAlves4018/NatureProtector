using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;

namespace NatureProtector.Simulator.Host.Tests.Fakes;

internal sealed class CollectingReadingPublisher : IReadingPublisher
{
    private readonly Action<EventEnvelope<SensorReadingProducedPayload>>? _onPublish;

    public CollectingReadingPublisher(Action<EventEnvelope<SensorReadingProducedPayload>>? onPublish = null)
    {
        _onPublish = onPublish;
    }

    public List<EventEnvelope<SensorReadingProducedPayload>> Published { get; } = [];

    public Task PublishAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Published.Add(envelope);
        _onPublish?.Invoke(envelope);

        return Task.CompletedTask;
    }
}
