using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;

namespace NatureProtector.Simulator.Host.Tests.Publishing;

public sealed class RabbitMqReadingPublisherTests
{
    [Fact]
    public async Task PublishAsync_Throws_WhenEnvelopeIsNull()
    {
        using var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events"
        });

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync(
            envelope: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("envelope", ex.ParamName);
    }

    [Fact]
    public async Task PublishAsync_Throws_WhenCancellationIsRequested()
    {
        using var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events"
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => publisher.PublishAsync(
            envelope: CreateEnvelope(),
            cancellationToken: cts.Token));
    }

    [Fact]
    public async Task PublishAsync_Throws_WhenPublisherWasDisposed()
    {
        var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = "localhost",
            UserName = "np",
            Password = "pass",
            ExchangeName = "np.events"
        });
        publisher.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => publisher.PublishAsync(
            envelope: CreateEnvelope(),
            cancellationToken: CancellationToken.None));
    }

    [Theory]
    [InlineData("", "np", "pass", "np.events", "RabbitMQ HostName is not configured.")]
    [InlineData("localhost", "", "pass", "np.events", "RabbitMQ UserName is not configured.")]
    [InlineData("localhost", "np", "", "np.events", "RabbitMQ Password is not configured.")]
    [InlineData("localhost", "np", "pass", "", "RabbitMQ ExchangeName is not configured.")]
    public async Task PublishAsync_Throws_WhenRequiredOptionIsMissing(
        string hostName,
        string userName,
        string password,
        string exchangeName,
        string expectedMessage)
    {
        using var publisher = CreatePublisher(new RabbitMqOptions
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            ExchangeName = exchangeName
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(
            envelope: CreateEnvelope(),
            cancellationToken: CancellationToken.None));

        Assert.Equal(expectedMessage, ex.Message);
    }

    private static RabbitMqReadingPublisher CreatePublisher(RabbitMqOptions options)
    {
        return new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));
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
