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

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class ReadingIngestionWorkerTests
{
    [Fact]
    public void Ctor_Throws_WhenLoggerIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ReadingIngestionWorker(
            logger: null!,
            rabbitMqOptions: Options.Create(new RabbitMqOptions()),
            preventionOptions: Options.Create(new PreventionOptions()),
            validator: new StubValidator(),
            store: new StubStore()));

        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenRabbitMqOptionsIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ReadingIngestionWorker(
            logger: NullLogger<ReadingIngestionWorker>.Instance,
            rabbitMqOptions: null!,
            preventionOptions: Options.Create(new PreventionOptions()),
            validator: new StubValidator(),
            store: new StubStore()));

        Assert.Equal("rabbitMqOptions", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenPreventionOptionsIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ReadingIngestionWorker(
            logger: NullLogger<ReadingIngestionWorker>.Instance,
            rabbitMqOptions: Options.Create(new RabbitMqOptions()),
            preventionOptions: null!,
            validator: new StubValidator(),
            store: new StubStore()));

        Assert.Equal("preventionOptions", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenValidatorIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ReadingIngestionWorker(
            logger: NullLogger<ReadingIngestionWorker>.Instance,
            rabbitMqOptions: Options.Create(new RabbitMqOptions()),
            preventionOptions: Options.Create(new PreventionOptions()),
            validator: null!,
            store: new StubStore()));

        Assert.Equal("validator", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenStoreIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new ReadingIngestionWorker(
            logger: NullLogger<ReadingIngestionWorker>.Instance,
            rabbitMqOptions: Options.Create(new RabbitMqOptions()),
            preventionOptions: Options.Create(new PreventionOptions()),
            validator: new StubValidator(),
            store: null!));

        Assert.Equal("store", ex.ParamName);
    }

    [Fact]
    public void DeserializeEnvelope_ReturnsEnvelope_ForValidJson()
    {
        var method = typeof(ReadingIngestionWorker).GetMethod(
            "DeserializeEnvelope",
            BindingFlags.Static | BindingFlags.NonPublic);
        var envelope = CreateEnvelope();
        var body = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        var deserialized = method!.Invoke(null, [new ReadOnlyMemory<byte>(body)])
            as EventEnvelope<SensorReadingProducedPayload>;

        Assert.NotNull(deserialized);
        Assert.Equal(envelope.EventId, deserialized!.EventId);
        Assert.Equal(envelope.Payload.SensorId, deserialized.Payload.SensorId);
    }

    [Fact]
    public void DeserializeEnvelope_ThrowsJsonException_ForInvalidJson()
    {
        var method = typeof(ReadingIngestionWorker).GetMethod(
            "DeserializeEnvelope",
            BindingFlags.Static | BindingFlags.NonPublic);
        var body = Encoding.UTF8.GetBytes("{ invalid");

        var ex = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, [new ReadOnlyMemory<byte>(body)]));

        Assert.IsType<System.Text.Json.JsonException>(ex.InnerException);
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope()
    {
        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-001",
            Producer: "NatureProtector.Simulator.Host",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: new DateTimeOffset(2026, 4, 6, 21, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor-01",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 29.0,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }

    private sealed class StubValidator : IReadingValidator
    {
        public ReadingValidationResult Validate(EventEnvelope<SensorReadingProducedPayload>? envelope)
        {
            return ReadingValidationResult.Accept();
        }
    }

    private sealed class StubStore : IAcceptedReadingStore
    {
        public void Persist(AcceptedReadingRecord record)
        {
        }
    }
}
