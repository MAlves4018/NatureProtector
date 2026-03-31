namespace NatureProtector.Shared.Messaging;

public static class NatureProtectorRabbitMqTopology
{
    public const string ExchangeName = "np.events";
    public const string ExchangeType = "topic";

    public const string IngestionReadingsQueue = "np.ingestion.readings";
    public const string ObservabilityRawQueue = "np.observability.raw";

    public static readonly (string QueueName, string RoutingKey)[] Bindings =
    [
        (IngestionReadingsQueue, RoutingKeys.SensorReadingProduced),
        (ObservabilityRawQueue, RoutingKeys.SensorReadingProduced)
    ];
}