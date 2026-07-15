using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class UnavailableRuntimeObservabilityService : IRuntimeObservabilityService
{
    private const string RabbitMqSource = "RabbitMQ Management API";
    private readonly IReadOnlyList<RabbitMqQueueDefinition> _queueDefinitions;

    public UnavailableRuntimeObservabilityService(
        string availabilityMessage,
        RabbitMqOptions? rabbitMqOptions = null)
    {
        AvailabilityMessage = availabilityMessage;
        _queueDefinitions = (rabbitMqOptions ?? new RabbitMqOptions())
            .GetQueueDefinitions()
            .ToArray();
    }

    public bool IsAvailable => false;

    public string AvailabilityMessage { get; }

    public Task<RuntimeOperationalHealthResponse> GetOperationalHealthAsync(CancellationToken cancellationToken)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var rabbitMq = BuildUnavailableRabbitMq(observedAt);
        return Task.FromResult(new RuntimeOperationalHealthResponse(
            observedAt,
            [
                new RuntimeOperationalHealthComponentResponse(
                    "Backoffice.Api",
                    RuntimeOperationalHealthStatus.Healthy,
                    observedAt,
                    "HTTP request reached authenticated controller.",
                    "current request",
                    observedAt,
                    null,
                    null,
                    "runtime-observability",
                    null),
                new RuntimeOperationalHealthComponentResponse(
                    "ControlPlane",
                    RuntimeOperationalHealthStatus.NotInstrumented,
                    observedAt,
                    "Runtime observability service is unavailable.",
                    "BackofficeApi:ControlPlaneEnabled",
                    null,
                    null,
                    null,
                    "runtime-observability",
                    AvailabilityMessage)
            ],
            rabbitMq,
            [new RuntimeLimitationResponse("runtime_observability_unavailable", AvailabilityMessage)]));
    }

    public Task<RabbitMqMetricsResponse> GetRabbitMqMetricsAsync(CancellationToken cancellationToken)
        => Task.FromResult(BuildUnavailableRabbitMq(DateTimeOffset.UtcNow));

    public Task<RuntimeEvidenceCatalogResponse> ListEvidenceAsync(CancellationToken cancellationToken)
        => Task.FromResult(new RuntimeEvidenceCatalogResponse(
            DateTimeOffset.UtcNow,
            [],
            [new RuntimeLimitationResponse("runtime_observability_unavailable", AvailabilityMessage)]));

    public Task<RuntimeEvidenceContentResponse?> GetEvidenceContentAsync(string evidenceId, CancellationToken cancellationToken)
        => Task.FromResult<RuntimeEvidenceContentResponse?>(null);

    private RabbitMqMetricsResponse BuildUnavailableRabbitMq(DateTimeOffset observedAt)
    {
        var queues = _queueDefinitions
            .Select(definition => new RabbitMqQueueMetricResponse(
                definition.QueueName,
                definition.QueueRole,
                definition.Enabled,
                definition.ConsumerRequired,
                definition.BlocksRuntimeHealth,
                null,
                null,
                null,
                null,
                observedAt,
                RabbitMqSource,
                definition.Enabled
                    ? RuntimeMetricCollectionStatus.Unavailable
                    : RuntimeMetricCollectionStatus.NotApplicable,
                definition.Enabled
                    ? AvailabilityMessage
                    : "Queue is disabled by configuration."))
            .ToArray();

        return new RabbitMqMetricsResponse(
            observedAt,
            RabbitMqSource,
            RuntimeMetricCollectionStatus.Unavailable,
            queues,
            [new RuntimeLimitationResponse("rabbitmq_metrics_unavailable", AvailabilityMessage)]);
    }
}
