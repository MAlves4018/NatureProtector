using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Tests.Messaging;

public sealed class MessagingConstantsTests
{
    [Fact]
    public void EventTypes_AreStable()
    {
        Assert.Equal("SensorReadingProduced", EventTypes.SensorReadingProduced);
        Assert.Equal("ReadingAccepted", EventTypes.ReadingAccepted);
        Assert.Equal("ReadingRejected", EventTypes.ReadingRejected);
        Assert.Equal("ReadingNormalized", EventTypes.ReadingNormalized);
    }

    [Fact]
    public void RoutingKeys_AreStable()
    {
        Assert.Equal("simulation.reading.produced", RoutingKeys.SensorReadingProduced);
        Assert.Equal("ingestion.reading.accepted", RoutingKeys.ReadingAccepted);
        Assert.Equal("ingestion.reading.rejected", RoutingKeys.ReadingRejected);
        Assert.Equal("ingestion.reading.normalized", RoutingKeys.ReadingNormalized);
    }


    [Fact]
    public void QueueRoles_AreStable()
    {
        Assert.Equal("PrimaryWorkQueue", RabbitMqQueueRoles.PrimaryWorkQueue);
        Assert.Equal("AuxiliaryDiagnosticQueue", RabbitMqQueueRoles.AuxiliaryDiagnosticQueue);
    }

    [Fact]
    public void Topology_DefinesExpectedExchangeQueuesAndBindings()
    {
        Assert.Equal("np.events", NatureProtectorRabbitMqTopology.ExchangeName);
        Assert.Equal("topic", NatureProtectorRabbitMqTopology.ExchangeType);
        Assert.Equal("np.ingestion.readings", NatureProtectorRabbitMqTopology.IngestionReadingsQueue);
        Assert.Equal("np.observability.raw", NatureProtectorRabbitMqTopology.ObservabilityRawQueue);

        Assert.Collection(
            NatureProtectorRabbitMqTopology.Bindings,
            binding =>
            {
                Assert.Equal(NatureProtectorRabbitMqTopology.IngestionReadingsQueue, binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            },
            binding =>
            {
                Assert.Equal(NatureProtectorRabbitMqTopology.ObservabilityRawQueue, binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            });
    }
}
