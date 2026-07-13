namespace NatureProtector.Shared.Messaging;

public static class RabbitMqQueueRoles
{
    public const string PrimaryWorkQueue = "PrimaryWorkQueue";
    public const string AuxiliaryDiagnosticQueue = "AuxiliaryDiagnosticQueue";
}

public sealed record RabbitMqQueueDefinition(
    string QueueName,
    string RoutingKey,
    string QueueRole,
    bool Enabled,
    bool ConsumerRequired,
    bool BlocksRuntimeHealth);
