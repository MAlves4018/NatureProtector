using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;

namespace NatureProtector.Simulator.Host.Tests.Publishing;

public sealed class ConsoleReadingPublisherTests
{
    [Fact]
    public async Task PublishAsync_Throws_WhenEnvelopeIsNull()
    {
        var publisher = new ConsoleReadingPublisher(NullLogger<ConsoleReadingPublisher>.Instance);

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync(
            envelope: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("envelope", ex.ParamName);
    }

    [Fact]
    public async Task PublishAsync_Throws_WhenCancellationIsRequested()
    {
        var publisher = new ConsoleReadingPublisher(NullLogger<ConsoleReadingPublisher>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(
            envelope: CreateEnvelope(),
            cancellationToken: cts.Token));
    }

    [Fact]
    public async Task PublishAsync_WritesSerializedJsonToConsole()
    {
        var publisher = new ConsoleReadingPublisher(NullLogger<ConsoleReadingPublisher>.Instance);
        var envelope = CreateEnvelope();
        var originalOut = Console.Out;
        using var writer = new StringWriter();

        Console.SetOut(writer);

        try
        {
            await publisher.PublishAsync(envelope, CancellationToken.None);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains(envelope.EventId.ToString(), output);
        Assert.Contains("\"eventType\":\"SensorReadingProduced\"", output);
        Assert.Contains("\"sensorName\":\"Sensor-01\"", output);
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
            EventTime: new DateTimeOffset(2026, 4, 6, 19, 0, 0, TimeSpan.Zero),
            IngestTime: null,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: "Sensor-01",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 28.4,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }
}
