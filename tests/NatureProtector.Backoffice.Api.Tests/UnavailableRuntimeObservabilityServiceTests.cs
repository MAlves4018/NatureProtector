using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class UnavailableRuntimeObservabilityServiceTests
{
    [Fact]
    public async Task UnavailableService_ReportsExplicitRuntimeLimitations()
    {
        var service = new UnavailableRuntimeObservabilityService("Runtime disabled for tests.");

        Assert.False(service.IsAvailable);
        Assert.Equal("Runtime disabled for tests.", service.AvailabilityMessage);

        var health = await service.GetOperationalHealthAsync(CancellationToken.None);
        Assert.Contains(health.Components, component =>
            component.Component == "Backoffice.Api" &&
            component.Status == RuntimeOperationalHealthStatus.Healthy);
        Assert.Contains(health.Components, component =>
            component.Component == "ControlPlane" &&
            component.Status == RuntimeOperationalHealthStatus.NotInstrumented &&
            component.Limitation == "Runtime disabled for tests.");
        Assert.Contains(health.Limitations, limitation =>
            limitation.Code == "runtime_observability_unavailable" &&
            limitation.Message == "Runtime disabled for tests.");

        var rabbitMq = await service.GetRabbitMqMetricsAsync(CancellationToken.None);
        Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, rabbitMq.CollectionStatus);
        Assert.Contains(rabbitMq.Limitations, limitation =>
            limitation.Code == "rabbitmq_metrics_unavailable" &&
            limitation.Message == "Runtime disabled for tests.");
        Assert.Equal(
            NatureProtectorRabbitMqTopology.Bindings
                .Select(binding => binding.QueueName)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            rabbitMq.Queues.Count);
        Assert.All(rabbitMq.Queues, queue =>
        {
            Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, queue.CollectionStatus);
            Assert.Equal("Runtime disabled for tests.", queue.Limitation);
        });

        var evidence = await service.ListEvidenceAsync(CancellationToken.None);
        Assert.Empty(evidence.Items);
        Assert.Contains(evidence.Limitations, limitation =>
            limitation.Code == "runtime_observability_unavailable" &&
            limitation.Message == "Runtime disabled for tests.");

        Assert.Null(await service.GetEvidenceContentAsync("summary", CancellationToken.None));
    }
}
