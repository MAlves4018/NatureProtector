using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerRabbitMqPublisherTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task RabbitMqReadingPublisher_PublishesSerializablePersistentEnvelope_OnRealRabbitMq()
    {
        var exchangeName = $"np.it.{Guid.NewGuid():N}";
        var envelope = CreateEnvelope();
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(
            exchangeName,
            observabilityRawEnabled: true);
        var defaultVhostFactory = new ConnectionFactory
        {
            HostName = baseOptions.HostName,
            Port = baseOptions.Port,
            UserName = baseOptions.UserName,
            Password = baseOptions.Password,
            VirtualHost = "/"
        };
        var canonicalQueuesBefore = ReadCanonicalQueueStates(defaultVhostFactory);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(baseOptions, CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        using var publisher = new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));

        await publisher.PublishAsync(envelope, CancellationToken.None);

        var result = await WaitForMessageAsync(channel, options.IngestionReadingsQueueName, envelope.EventId);
        channel.BasicAck(result.DeliveryTag, multiple: false);
        var observabilityCopy = await WaitForMessageAsync(channel, options.ObservabilityRawQueueName, envelope.EventId);
        channel.BasicAck(observabilityCopy.DeliveryTag, multiple: false);
        var roundTrip = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            result.Body.ToArray());

        Assert.NotNull(roundTrip);
        Assert.Equal(envelope.EventId, roundTrip.EventId);
        Assert.Equal(envelope.CorrelationId, roundTrip.CorrelationId);
        Assert.Equal(envelope.Payload.SensorId, roundTrip.Payload.SensorId);
        Assert.Equal("application/json", result.BasicProperties.ContentType);
        Assert.Equal("utf-8", result.BasicProperties.ContentEncoding);
        Assert.Equal(envelope.EventId.ToString(), result.BasicProperties.MessageId);
        Assert.Equal(envelope.CorrelationId, result.BasicProperties.CorrelationId);
        Assert.Equal(EventTypes.SensorReadingProduced, result.BasicProperties.Type);
        Assert.True(result.BasicProperties.Persistent);

        Assert.Equal(canonicalQueuesBefore, ReadCanonicalQueueStates(defaultVhostFactory));

        await virtualHost.DeleteAsync(CancellationToken.None);
        Assert.False(await virtualHost.ExistsAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task RabbitMqReadingPublisher_DoesNotCreateRawQueue_WhenDisabled_OnRealRabbitMq()
    {
        var exchangeName = $"np.it.{Guid.NewGuid():N}";
        var envelope = CreateEnvelope();
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseOptions,
            CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        using var publisher = new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));

        await publisher.PublishAsync(envelope, CancellationToken.None);

        var ingestionCopy = await WaitForMessageAsync(
            channel,
            options.IngestionReadingsQueueName,
            envelope.EventId);
        channel.BasicAck(ingestionCopy.DeliveryTag, multiple: false);

        using var rawProbeChannel = connection.CreateModel();
        var missingRawQueue = Assert.Throws<OperationInterruptedException>(() =>
            rawProbeChannel.QueueDeclarePassive(options.ObservabilityRawQueueName));

        Assert.NotNull(missingRawQueue.ShutdownReason);
        Assert.Equal((ushort)404, missingRawQueue.ShutdownReason.ReplyCode);

        await virtualHost.DeleteAsync(CancellationToken.None);
        Assert.False(await virtualHost.ExistsAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task RabbitMqReadingPublisher_Throws_WhenMandatoryMessageIsUnroutable_OnRealRabbitMq()
    {
        var exchangeName = $"np.it.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(
            exchangeName,
            observabilityRawEnabled: true);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(baseOptions, CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        using var publisher = new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));
        var setupEnvelope = CreateEnvelope();

        await publisher.PublishAsync(setupEnvelope, CancellationToken.None);
        await AcknowledgePublishedCopiesAsync(channel, options, setupEnvelope.EventId);

        channel.QueueDelete(options.IngestionReadingsQueueName, ifUnused: false, ifEmpty: false);
        channel.QueueDelete(options.ObservabilityRawQueueName, ifUnused: false, ifEmpty: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            publisher.PublishAsync(CreateEnvelope(), CancellationToken.None));

        Assert.Contains("unroutable", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RoutingKey", ex.Message, StringComparison.Ordinal);

        await virtualHost.DeleteAsync(CancellationToken.None);
        Assert.False(await virtualHost.ExistsAsync(CancellationToken.None));
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task RabbitMqReadingPublisher_RecreatesClosedChannelAndConnection_OnRealRabbitMq()
    {
        var exchangeName = $"np.it.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(
            exchangeName,
            observabilityRawEnabled: true);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(baseOptions, CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        using var publisher = new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));
        var firstEnvelope = CreateEnvelope();

        await publisher.PublishAsync(firstEnvelope, CancellationToken.None);
        await AcknowledgePublishedCopiesAsync(channel, options, firstEnvelope.EventId);

        GetPrivateField<IModel>(publisher, "_channel").Close();
        var secondEnvelope = CreateEnvelope();

        await publisher.PublishAsync(secondEnvelope, CancellationToken.None);
        await AcknowledgePublishedCopiesAsync(channel, options, secondEnvelope.EventId);

        GetPrivateField<IConnection>(publisher, "_connection").Close();
        var thirdEnvelope = CreateEnvelope();

        await publisher.PublishAsync(thirdEnvelope, CancellationToken.None);
        await AcknowledgePublishedCopiesAsync(channel, options, thirdEnvelope.EventId);

        await virtualHost.DeleteAsync(CancellationToken.None);
        Assert.False(await virtualHost.ExistsAsync(CancellationToken.None));
    }

    private static async Task AcknowledgePublishedCopiesAsync(
        IModel channel,
        RabbitMqOptions options,
        Guid eventId)
    {
        var ingestionCopy = await WaitForMessageAsync(channel, options.IngestionReadingsQueueName, eventId);
        channel.BasicAck(ingestionCopy.DeliveryTag, multiple: false);
        var observabilityCopy = await WaitForMessageAsync(channel, options.ObservabilityRawQueueName, eventId);
        channel.BasicAck(observabilityCopy.DeliveryTag, multiple: false);
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(
        IModel channel,
        string queueName,
        Guid eventId)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var result = channel.BasicGet(queueName, autoAck: false);
            if (result is not null && BodyHasEventId(result.Body, eventId))
            {
                return result;
            }

            if (result is not null)
            {
                channel.BasicNack(result.DeliveryTag, multiple: false, requeue: true);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"RabbitMQ message {eventId} was not observed on queue {queueName}.");
    }

    private static bool BodyHasEventId(ReadOnlyMemory<byte> body, Guid eventId)
    {
        try
        {
            var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(body.ToArray());
            if (envelope is null)
            {
                return false;
            }

            return envelope.EventId == eventId;
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyDictionary<string, QueueState?> ReadCanonicalQueueStates(ConnectionFactory factory)
    {
        return new Dictionary<string, QueueState?>
        {
            [NatureProtectorRabbitMqTopology.IngestionReadingsQueue] =
                TryReadQueueState(factory, NatureProtectorRabbitMqTopology.IngestionReadingsQueue),
            [NatureProtectorRabbitMqTopology.ObservabilityRawQueue] =
                TryReadQueueState(factory, NatureProtectorRabbitMqTopology.ObservabilityRawQueue)
        };
    }

    private static QueueState? TryReadQueueState(ConnectionFactory factory, string queueName)
    {
        try
        {
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            var declaration = channel.QueueDeclarePassive(queueName);
            return new QueueState(declaration.MessageCount, declaration.ConsumerCount);
        }
        catch (OperationInterruptedException)
        {
            return null;
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName)
        where T : class
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        return Assert.IsAssignableFrom<T>(field.GetValue(target));
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        var eventId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 4, 6, 12, 0, 0, TimeSpan.Zero);

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: eventId,
            CorrelationId: $"docker-it-{eventId:N}",
            Producer: "NatureProtector.IntegrationTests",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: timestamp,
            IngestTime: timestamp.AddSeconds(1),
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Docker-IT-Sensor",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 33.5,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private sealed record QueueState(uint MessageCount, uint ConsumerCount);
}
