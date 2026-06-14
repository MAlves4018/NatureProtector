using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public interface IRuntimeObservabilityService
{
    bool IsAvailable { get; }

    string AvailabilityMessage { get; }

    Task<RuntimeOperationalHealthResponse> GetOperationalHealthAsync(CancellationToken cancellationToken);

    Task<RabbitMqMetricsResponse> GetRabbitMqMetricsAsync(CancellationToken cancellationToken);

    Task<RuntimeEvidenceCatalogResponse> ListEvidenceAsync(CancellationToken cancellationToken);

    Task<RuntimeEvidenceContentResponse?> GetEvidenceContentAsync(string evidenceId, CancellationToken cancellationToken);
}
