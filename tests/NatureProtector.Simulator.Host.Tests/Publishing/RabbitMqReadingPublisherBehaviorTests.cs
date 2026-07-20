using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Tests.Helpers;
using RabbitMQ.Client;

namespace NatureProtector.Simulator.Host.Tests.Publishing;

public sealed class RabbitMqReadingPublisherBehaviorTests
{
    [Fact]
    public async Task PublishAsync_UsesInjectedOpenChannel_AndSetsMessageMetadata()
    {
        var options = new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events"
        };
        using var publisher = CreatePublisher(options);
        var (connection, connectionRecorder) = RecordingDispatchProxy<IConnection>.CreateProxy();
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var (properties, propertiesRecorder) = RecordingDispatchProxy<IBasicProperties>.CreateProxy();
        connectionRecorder.Properties["IsOpen"] = true;
        channelRecorder.Properties["IsOpen"] = true;
        channelRecorder.ReturnValues["CreateBasicProperties"] = properties;
        SetPrivateField(publisher, "_connection", connection);
        SetPrivateField(publisher, "_channel", channel);
        var envelope = CreateEnvelope();

        await publisher.PublishAsync(envelope, CancellationToken.None);

        var publish = Assert.Single(channelRecorder.Invocations, x => x.MethodName == "BasicPublish");
        Assert.Equal(options.ExchangeName, Assert.IsType<string>(publish.Arguments[0]));
        Assert.Equal(RoutingKeys.SensorReadingProduced, Assert.IsType<string>(publish.Arguments[1]));
        Assert.True(Assert.IsType<bool>(publish.Arguments[2]));
        Assert.True(Assert.IsType<bool>(propertiesRecorder.Properties["Persistent"]));
        Assert.Equal("application/json", Assert.IsType<string>(propertiesRecorder.Properties["ContentType"]));
        Assert.Equal("utf-8", Assert.IsType<string>(propertiesRecorder.Properties["ContentEncoding"]));
        Assert.Equal(envelope.EventId.ToString(), Assert.IsType<string>(propertiesRecorder.Properties["MessageId"]));
        Assert.Equal(envelope.CorrelationId, Assert.IsType<string>(propertiesRecorder.Properties["CorrelationId"]));
        Assert.Equal(envelope.EventType, Assert.IsType<string>(propertiesRecorder.Properties["Type"]));

        var timestamp = Assert.IsType<AmqpTimestamp>(propertiesRecorder.Properties["Timestamp"]);
        Assert.True(timestamp.UnixTime >= envelope.EventTime.ToUnixTimeSeconds());
        var publishedBody = Assert.IsType<ReadOnlyMemory<byte>>(publish.Arguments[4]);
        var publishedEnvelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(publishedBody);
        Assert.NotNull(publishedEnvelope);
        Assert.Equal(envelope.EventId, publishedEnvelope.EventId);
        Assert.NotNull(publishedEnvelope.PublishedAt);
        Assert.True(publishedEnvelope.PublishedAt >= envelope.EventTime);

        var confirm = Assert.Single(channelRecorder.Invocations, x => x.MethodName == "WaitForConfirmsOrDie");
        Assert.Equal(
            TimeSpan.FromSeconds(options.PublisherConfirmTimeoutSeconds),
            Assert.IsType<TimeSpan>(confirm.Arguments[0]));
        Assert.Single(channelRecorder.Invocations, x => x.MethodName == "add_BasicReturn");
        Assert.Single(channelRecorder.Invocations, x => x.MethodName == "remove_BasicReturn");
    }

    [Fact]
    public void DeclareTopology_DeclaresOnlyPrimaryQueue_WhenRawIsDisabled()
    {
        using var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events",
            IngestionReadingsQueueName = "np.it.ingestion",
            ObservabilityRawQueueName = "np.it.raw"
        });
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        InvokePrivateMethod(publisher, "DeclareTopology", channel);

        var exchangeDeclare = Assert.Single(recorder.Invocations, x => x.MethodName == "ExchangeDeclare");
        Assert.Equal("np.events", Assert.IsType<string>(exchangeDeclare.Arguments[0]));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeType, Assert.IsType<string>(exchangeDeclare.Arguments[1]));

        var queueDeclare = Assert.Single(recorder.Invocations, x => x.MethodName == "QueueDeclare");
        Assert.Equal("np.it.ingestion", Assert.IsType<string>(queueDeclare.Arguments[0]));

        var queueBind = Assert.Single(recorder.Invocations, x => x.MethodName == "QueueBind");
        Assert.Equal("np.it.ingestion", Assert.IsType<string>(queueBind.Arguments[0]));
    }

    [Fact]
    public void DeclareTopology_DeclaresRawQueue_WhenExplicitlyEnabled()
    {
        using var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events",
            IngestionReadingsQueueName = "np.it.ingestion",
            ObservabilityRawEnabled = true,
            ObservabilityRawQueueName = "np.it.raw"
        });
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        InvokePrivateMethod(publisher, "DeclareTopology", channel);

        var queueDeclares = recorder.Invocations.Where(x => x.MethodName == "QueueDeclare").ToList();
        Assert.Equal(2, queueDeclares.Count);
        Assert.Contains(queueDeclares, x => Equals(x.Arguments[0], "np.it.ingestion"));
        Assert.Contains(queueDeclares, x => Equals(x.Arguments[0], "np.it.raw"));

        var queueBinds = recorder.Invocations.Where(x => x.MethodName == "QueueBind").ToList();
        Assert.Equal(2, queueBinds.Count);
        Assert.Contains(queueBinds, x => Equals(x.Arguments[0], "np.it.ingestion"));
        Assert.Contains(queueBinds, x => Equals(x.Arguments[0], "np.it.raw"));
    }

    [Fact]
    public void Dispose_DisposesInjectedConnectionObjects_OnlyOnce()
    {
        var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events"
        });
        var (connection, connectionRecorder) = RecordingDispatchProxy<IConnection>.CreateProxy();
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        SetPrivateField(publisher, "_connection", connection);
        SetPrivateField(publisher, "_channel", channel);

        publisher.Dispose();
        publisher.Dispose();

        Assert.Single(channelRecorder.Invocations, x => x.MethodName == "Dispose");
        Assert.Single(connectionRecorder.Invocations, x => x.MethodName == "Dispose");
    }

    private static RabbitMqReadingPublisher CreatePublisher(RabbitMqOptions options)
    {
        return new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));
    }

    private static void InvokePrivateMethod(
        object target,
        string methodName,
        params object?[] args)
    {
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");

        method.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CorrelationId: "corr-publish",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            EventTime: new DateTimeOffset(2026, 4, 6, 23, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                SensorId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                SensorName: "Publisher-Sensor",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 26.3,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }
}
