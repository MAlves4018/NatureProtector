using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "np";
    public string Password { get; init; } = "np_dev_pass";
    public string VirtualHost { get; init; } = "/";
    public bool TlsEnabled { get; init; }
    public string? TlsServerName { get; init; }
    public string? TlsCertificateAuthorityPath { get; init; }
    public string ExchangeName { get; init; } = "np.events";
    public int PublisherConfirmTimeoutSeconds { get; init; } = 10;
    public string IngestionReadingsQueueName { get; init; } =
        NatureProtectorRabbitMqTopology.IngestionReadingsQueue;
    public bool ObservabilityRawEnabled { get; init; }
    public string ObservabilityRawQueueName { get; init; } =
        NatureProtectorRabbitMqTopology.ObservabilityRawQueue;

    public IReadOnlyCollection<RabbitMqQueueDefinition> GetQueueDefinitions()
        =>
        [
            new RabbitMqQueueDefinition(
                IngestionReadingsQueueName,
                RoutingKeys.SensorReadingProduced,
                RabbitMqQueueRoles.PrimaryWorkQueue,
                Enabled: true,
                ConsumerRequired: true,
                BlocksRuntimeHealth: true),
            new RabbitMqQueueDefinition(
                ObservabilityRawQueueName,
                RoutingKeys.SensorReadingProduced,
                RabbitMqQueueRoles.AuxiliaryDiagnosticQueue,
                Enabled: ObservabilityRawEnabled,
                ConsumerRequired: false,
                BlocksRuntimeHealth: false)
        ];

    public IReadOnlyCollection<RabbitMqQueueDefinition> GetEnabledQueueDefinitions()
        => GetQueueDefinitions()
            .Where(definition => definition.Enabled)
            .ToArray();

    public IReadOnlyCollection<string> GetQueueNames()
        => GetEnabledQueueDefinitions()
            .Select(definition => definition.QueueName)
            .ToArray();

    public IReadOnlyCollection<(string QueueName, string RoutingKey)> GetBindings()
        => GetEnabledQueueDefinitions()
            .Select(definition => (definition.QueueName, definition.RoutingKey))
            .ToArray();
}
