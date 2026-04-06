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

        var publish = Assert.Single(channelRecorder.Invocations.Where(x => x.MethodName == "BasicPublish"));
        Assert.Equal(options.ExchangeName, Assert.IsType<string>(publish.Arguments[0]));
        Assert.Equal(RoutingKeys.SensorReadingProduced, Assert.IsType<string>(publish.Arguments[1]));
        Assert.True(Assert.IsType<bool>(propertiesRecorder.Properties["Persistent"]));
        Assert.Equal(envelope.EventId.ToString(), Assert.IsType<string>(propertiesRecorder.Properties["MessageId"]));
        Assert.Equal(envelope.CorrelationId, Assert.IsType<string>(propertiesRecorder.Properties["CorrelationId"]));
        Assert.Equal(envelope.EventType, Assert.IsType<string>(propertiesRecorder.Properties["Type"]));

        var timestamp = Assert.IsType<AmqpTimestamp>(propertiesRecorder.Properties["Timestamp"]);
        Assert.Equal(envelope.EventTime.ToUnixTimeSeconds(), timestamp.UnixTime);
    }

    [Fact]
    public void DeclareTopology_DeclaresExchangeQueuesAndBindings()
    {
        using var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events"
        });
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        InvokePrivateMethod(publisher, "DeclareTopology", channel);

        var exchangeDeclare = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "ExchangeDeclare"));
        Assert.Equal("np.events", Assert.IsType<string>(exchangeDeclare.Arguments[0]));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeType, Assert.IsType<string>(exchangeDeclare.Arguments[1]));

        var queueDeclares = recorder.Invocations.Where(x => x.MethodName == "QueueDeclare").ToList();
        Assert.Equal(2, queueDeclares.Count);
        Assert.Contains(queueDeclares, x => Equals(x.Arguments[0], NatureProtectorRabbitMqTopology.IngestionReadingsQueue));
        Assert.Contains(queueDeclares, x => Equals(x.Arguments[0], NatureProtectorRabbitMqTopology.ObservabilityRawQueue));

        var queueBinds = recorder.Invocations.Where(x => x.MethodName == "QueueBind").ToList();
        Assert.Equal(NatureProtectorRabbitMqTopology.Bindings.Count(), queueBinds.Count);
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

        Assert.Single(channelRecorder.Invocations.Where(x => x.MethodName == "Dispose"));
        Assert.Single(connectionRecorder.Invocations.Where(x => x.MethodName == "Dispose"));
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
