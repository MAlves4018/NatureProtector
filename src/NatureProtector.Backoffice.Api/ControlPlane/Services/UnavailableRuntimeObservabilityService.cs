using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class UnavailableRuntimeObservabilityService(string availabilityMessage) : IRuntimeObservabilityService
{
    public bool IsAvailable => false;

    public string AvailabilityMessage { get; } = availabilityMessage;

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
        var queues = NatureProtectorRabbitMqTopology.Bindings
            .Select(binding => binding.QueueName)
            .Distinct(StringComparer.Ordinal)
            .Select(queue => new RabbitMqQueueMetricResponse(
                queue,
                null,
                null,
                null,
                null,
                observedAt,
                "RabbitMQ Management HTTP API",
                RuntimeMetricCollectionStatus.Unavailable,
                AvailabilityMessage))
            .ToArray();

        return new RabbitMqMetricsResponse(
            observedAt,
            "RabbitMQ Management HTTP API",
            RuntimeMetricCollectionStatus.Unavailable,
            queues,
            [new RuntimeLimitationResponse("rabbitmq_metrics_unavailable", AvailabilityMessage)]);
    }
}
