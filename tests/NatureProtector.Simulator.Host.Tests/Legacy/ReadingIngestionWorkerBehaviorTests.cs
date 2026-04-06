using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Validation;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Tests.Helpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class ReadingIngestionWorkerBehaviorTests
{
    [Fact]
    public void HandleReceivedMessage_AcknowledgesAndPersistsAcceptedReading()
    {
        var store = new CapturingAcceptedReadingStore();
        var worker = CreateWorker(
            validator: new DelegateValidator(_ => ReadingValidationResult.Accept()),
            store: store,
            preventionOptions: new PreventionOptions { RequeueOnUnexpectedFailure = false });
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        recorder.Properties["IsOpen"] = true;
        SetPrivateField(worker, "_channel", channel);
        var deliveryTag = 42UL;

        InvokePrivateMethod(
            worker,
            "HandleReceivedMessage",
            null,
            CreateEventArgs(CreateEnvelope(), deliveryTag));

        var persisted = Assert.Single(store.PersistedRecords);
        Assert.Equal("Sensor-01", persisted.SensorName);
        Assert.Equal(SensorMetricType.Temperature, persisted.MetricType);

        var ack = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicAck"));
        Assert.Equal(deliveryTag, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.False(Assert.IsType<bool>(ack.Arguments[1]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
    }

    [Fact]
    public void HandleReceivedMessage_AcknowledgesRejectedReading_WithoutPersisting()
    {
        var store = new CapturingAcceptedReadingStore();
        var worker = CreateWorker(
            validator: new DelegateValidator(_ => ReadingValidationResult.Reject("duplicate")),
            store: store,
            preventionOptions: new PreventionOptions());
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        recorder.Properties["IsOpen"] = true;
        SetPrivateField(worker, "_channel", channel);

        InvokePrivateMethod(
            worker,
            "HandleReceivedMessage",
            null,
            CreateEventArgs(CreateEnvelope(), 7));

        Assert.Empty(store.PersistedRecords);
        Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicAck"));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
    }

    [Fact]
    public void HandleReceivedMessage_AcknowledgesInvalidJsonPayload()
    {
        var worker = CreateWorker(
            validator: new DelegateValidator(_ => ReadingValidationResult.Accept()),
            store: new CapturingAcceptedReadingStore(),
            preventionOptions: new PreventionOptions());
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        recorder.Properties["IsOpen"] = true;
        SetPrivateField(worker, "_channel", channel);
        var invalidBody = Encoding.UTF8.GetBytes("{ invalid");

        InvokePrivateMethod(
            worker,
            "HandleReceivedMessage",
            null,
            CreateEventArgs(invalidBody, 8));

        Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicAck"));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
    }

    [Fact]
    public void HandleReceivedMessage_NacksUnexpectedFailures_UsingConfiguredRequeueOption()
    {
        var worker = CreateWorker(
            validator: new DelegateValidator(_ => throw new InvalidOperationException("boom")),
            store: new CapturingAcceptedReadingStore(),
            preventionOptions: new PreventionOptions { RequeueOnUnexpectedFailure = true });
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        recorder.Properties["IsOpen"] = true;
        SetPrivateField(worker, "_channel", channel);
        const ulong deliveryTag = 9;

        InvokePrivateMethod(
            worker,
            "HandleReceivedMessage",
            null,
            CreateEventArgs(CreateEnvelope(), deliveryTag));

        var nack = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicNack"));
        Assert.Equal(deliveryTag, Assert.IsType<ulong>(nack.Arguments[0]));
        Assert.False(Assert.IsType<bool>(nack.Arguments[1]));
        Assert.True(Assert.IsType<bool>(nack.Arguments[2]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicAck");
    }

    [Fact]
    public void DeclareTopology_DeclaresExchangeQueueAndBinding()
    {
        var worker = CreateWorker(
            validator: new DelegateValidator(_ => ReadingValidationResult.Accept()),
            store: new CapturingAcceptedReadingStore(),
            preventionOptions: new PreventionOptions { QueueName = "np.custom.queue" },
            rabbitMqOptions: new RabbitMqOptions { ExchangeName = "np.custom.events" });
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        InvokePrivateMethod(worker, "DeclareTopology", channel);

        var exchangeDeclare = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "ExchangeDeclare"));
        Assert.Equal("np.custom.events", Assert.IsType<string>(exchangeDeclare.Arguments[0]));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeType, Assert.IsType<string>(exchangeDeclare.Arguments[1]));

        var queueDeclare = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "QueueDeclare"));
        Assert.Equal("np.custom.queue", Assert.IsType<string>(queueDeclare.Arguments[0]));

        var queueBind = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "QueueBind"));
        Assert.Equal("np.custom.queue", Assert.IsType<string>(queueBind.Arguments[0]));
        Assert.Equal("np.custom.events", Assert.IsType<string>(queueBind.Arguments[1]));
        Assert.Equal(RoutingKeys.SensorReadingProduced, Assert.IsType<string>(queueBind.Arguments[2]));
    }

    [Fact]
    public void CloseResources_CancelsAndDisposesOpenRabbitMqResources()
    {
        var worker = CreateWorker(
            validator: new DelegateValidator(_ => ReadingValidationResult.Accept()),
            store: new CapturingAcceptedReadingStore(),
            preventionOptions: new PreventionOptions());
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var (connection, connectionRecorder) = RecordingDispatchProxy<IConnection>.CreateProxy();
        channelRecorder.Properties["IsOpen"] = true;
        connectionRecorder.Properties["IsOpen"] = true;
        SetPrivateField(worker, "_channel", channel);
        SetPrivateField(worker, "_connection", connection);
        SetPrivateField(worker, "_consumerTag", "consumer-01");

        InvokePrivateMethod(worker, "CloseResources");

        var cancel = Assert.Single(channelRecorder.Invocations.Where(x => x.MethodName == "BasicCancel"));
        Assert.Equal("consumer-01", Assert.IsType<string>(cancel.Arguments[0]));
        Assert.Contains(channelRecorder.Invocations, x => x.MethodName == "Close");
        Assert.Contains(channelRecorder.Invocations, x => x.MethodName == "Dispose");
        Assert.Contains(connectionRecorder.Invocations, x => x.MethodName == "Close");
        Assert.Contains(connectionRecorder.Invocations, x => x.MethodName == "Dispose");
    }

    private static ReadingIngestionWorker CreateWorker(
        IReadingValidator validator,
        IAcceptedReadingStore store,
        PreventionOptions preventionOptions,
        RabbitMqOptions? rabbitMqOptions = null)
    {
        return new ReadingIngestionWorker(
            NullLogger<ReadingIngestionWorker>.Instance,
            Options.Create(rabbitMqOptions ?? new RabbitMqOptions
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "np",
                Password = "pass",
                ExchangeName = "np.events"
            }),
            Options.Create(preventionOptions),
            validator,
            store);
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

    private static BasicDeliverEventArgs CreateEventArgs(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        ulong deliveryTag)
    {
        return CreateEventArgs(
            JsonEventSerializer.SerializeToUtf8Bytes(envelope),
            deliveryTag);
    }

    private static BasicDeliverEventArgs CreateEventArgs(
        byte[] body,
        ulong deliveryTag)
    {
        return new BasicDeliverEventArgs
        {
            DeliveryTag = deliveryTag,
            Body = new ReadOnlyMemory<byte>(body)
        };
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-accepted",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 6, 22, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor-01",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 27.4,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private sealed class DelegateValidator(Func<EventEnvelope<SensorReadingProducedPayload>?, ReadingValidationResult> validate)
        : IReadingValidator
    {
        public ReadingValidationResult Validate(EventEnvelope<SensorReadingProducedPayload>? envelope)
        {
            return validate(envelope);
        }
    }

    private sealed class CapturingAcceptedReadingStore : IAcceptedReadingStore
    {
        public List<AcceptedReadingRecord> PersistedRecords { get; } = [];

        public void Persist(AcceptedReadingRecord record)
        {
            PersistedRecords.Add(record);
        }
    }
}
