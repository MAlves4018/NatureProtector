using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Tests.Configuration;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void Defaults_AreAlignedWithCurrentLocalBaseline()
    {
        var options = new RabbitMqOptions();

        Assert.Equal("RabbitMq", RabbitMqOptions.SectionName);
        Assert.Equal("localhost", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("np", options.UserName);
        Assert.Equal("np_dev_pass", options.Password);
        Assert.Equal("/", options.VirtualHost);
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeName, options.ExchangeName);
        Assert.Equal(10, options.PublisherConfirmTimeoutSeconds);
        Assert.Equal(NatureProtectorRabbitMqTopology.IngestionReadingsQueue, options.IngestionReadingsQueueName);
        Assert.Equal(NatureProtectorRabbitMqTopology.ObservabilityRawQueue, options.ObservabilityRawQueueName);
        Assert.Equal(
            NatureProtectorRabbitMqTopology.Bindings,
            options.GetBindings());
    }

    [Fact]
    public void GetBindings_UsesConfiguredQueueNames_ForIsolatedTestTopology()
    {
        var options = new RabbitMqOptions
        {
            IngestionReadingsQueueName = "np.it.ingestion",
            ObservabilityRawQueueName = "np.it.raw"
        };

        Assert.Collection(
            options.GetBindings(),
            binding =>
            {
                Assert.Equal("np.it.ingestion", binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            },
            binding =>
            {
                Assert.Equal("np.it.raw", binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            });
    }
}
