using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class UnavailableRuntimeObservabilityServiceTests
{
    [Fact]
    public async Task UnavailableService_ReportsEffectiveTopologyAndExplicitRuntimeLimitations()
    {
        var options = new RabbitMqOptions
        {
            IngestionReadingsQueueName = "np.custom.ingestion",
            ObservabilityRawQueueName = "np.custom.raw",
            ObservabilityRawEnabled = false
        };
        var service = new UnavailableRuntimeObservabilityService(
            "Runtime disabled for tests.",
            options);

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
        Assert.Collection(
            rabbitMq.Queues,
            primary =>
            {
                Assert.Equal("np.custom.ingestion", primary.QueueName);
                Assert.Equal(RabbitMqQueueRoles.PrimaryWorkQueue, primary.QueueRole);
                Assert.True(primary.Enabled);
                Assert.True(primary.ConsumerRequired);
                Assert.True(primary.BlocksRuntimeHealth);
                Assert.Equal(RuntimeMetricCollectionStatus.Unavailable, primary.CollectionStatus);
                Assert.Equal("Runtime disabled for tests.", primary.Limitation);
            },
            auxiliary =>
            {
                Assert.Equal("np.custom.raw", auxiliary.QueueName);
                Assert.Equal(RabbitMqQueueRoles.AuxiliaryDiagnosticQueue, auxiliary.QueueRole);
                Assert.False(auxiliary.Enabled);
                Assert.Equal(RuntimeMetricCollectionStatus.NotApplicable, auxiliary.CollectionStatus);
                Assert.Equal("Queue is disabled by configuration.", auxiliary.Limitation);
            });

        var evidence = await service.ListEvidenceAsync(CancellationToken.None);
        Assert.Empty(evidence.Items);
        Assert.Contains(evidence.Limitations, limitation =>
            limitation.Code == "runtime_observability_unavailable" &&
            limitation.Message == "Runtime disabled for tests.");

        Assert.Null(await service.GetEvidenceContentAsync("summary", CancellationToken.None));
    }
}
