using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.IntegrationTests.TestInfrastructure;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using RabbitMQ.Client;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerRabbitMqOperationalAuditTests
{
    private const string AuditEnvironmentVariable = "NP_RUN_OPERATIONAL_AUDIT_PHASE1";

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task UnconsumedRawQueue_AccumulatesOneCopyPerPublishedReading()
    {
        if (!IsAuditEnabled())
        {
            return;
        }

        var exchangeName = $"np.audit.raw-growth.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseOptions,
            CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection("np-phase1-raw-growth");
        using var inspectionChannel = connection.CreateModel();
        using var publisher = CreatePublisher(options);

        var envelopes = Enumerable.Range(0, 6)
            .Select(index => CreateEnvelope(index))
            .ToArray();

        foreach (var envelope in envelopes)
        {
            await publisher.PublishAsync(envelope, CancellationToken.None);
        }

        foreach (var envelope in envelopes)
        {
            var ingestionCopy = await WaitForMessageAsync(
                inspectionChannel,
                options.IngestionReadingsQueueName,
                envelope.EventId);
            inspectionChannel.BasicAck(ingestionCopy.DeliveryTag, multiple: false);
        }

        var ingestionState = inspectionChannel.QueueDeclarePassive(
            options.IngestionReadingsQueueName);
        var rawState = inspectionChannel.QueueDeclarePassive(
            options.ObservabilityRawQueueName);

        Assert.Equal(0u, ingestionState.MessageCount);
        Assert.Equal(0u, ingestionState.ConsumerCount);
        Assert.Equal((uint)envelopes.Length, rawState.MessageCount);
        Assert.Equal(0u, rawState.ConsumerCount);

        Console.WriteLine(
            "PHASE1_RAW_GROWTH_REPRODUCED " +
            $"published={envelopes.Length} " +
            $"ingestion_ready={ingestionState.MessageCount} " +
            $"raw_ready={rawState.MessageCount} " +
            $"raw_consumers={rawState.ConsumerCount}");
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task RawRejectPublish_CanNackPublisherAfterIngestionAcceptedTheReading()
    {
        if (!IsAuditEnabled())
        {
            return;
        }

        var exchangeName = $"np.audit.partial-nack.{Guid.NewGuid():N}";
        var policyName = $"np-audit-raw-reject-{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseOptions,
            CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection("np-phase1-partial-nack");
        using var inspectionChannel = connection.CreateModel();
        using var publisher = CreatePublisher(options);

        var topologyEnvelope = CreateEnvelope(-1);
        await publisher.PublishAsync(topologyEnvelope, CancellationToken.None);
        await AcknowledgeBothCopiesAsync(inspectionChannel, options, topologyEnvelope.EventId);

        await virtualHost.SetQueuePolicyAsync(
            policyName,
            options.ObservabilityRawQueueName,
            new Dictionary<string, object>
            {
                ["max-length"] = 1,
                ["overflow"] = "reject-publish"
            },
            cancellationToken: CancellationToken.None);

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            var acceptedEnvelope = CreateEnvelope(1);
            await publisher.PublishAsync(acceptedEnvelope, CancellationToken.None);
            var acceptedIngestionCopy = await WaitForMessageAsync(
                inspectionChannel,
                options.IngestionReadingsQueueName,
                acceptedEnvelope.EventId);
            inspectionChannel.BasicAck(acceptedIngestionCopy.DeliveryTag, multiple: false);

            var nackedEnvelope = CreateEnvelope(2);
            var publishFailure = await Record.ExceptionAsync(() =>
                publisher.PublishAsync(nackedEnvelope, CancellationToken.None));

            Assert.NotNull(publishFailure);

            var partiallyAcceptedIngestionCopy = await WaitForMessageAsync(
                inspectionChannel,
                options.IngestionReadingsQueueName,
                nackedEnvelope.EventId);
            inspectionChannel.BasicAck(
                partiallyAcceptedIngestionCopy.DeliveryTag,
                multiple: false);

            var rawState = inspectionChannel.QueueDeclarePassive(
                options.ObservabilityRawQueueName);
            var ingestionState = inspectionChannel.QueueDeclarePassive(
                options.IngestionReadingsQueueName);

            Assert.Equal(1u, rawState.MessageCount);
            Assert.Equal(0u, ingestionState.MessageCount);

            Console.WriteLine(
                "PHASE1_PARTIAL_NACK_REPRODUCED " +
                $"exception={publishFailure.GetType().FullName} " +
                $"nacked_event={nackedEnvelope.EventId} " +
                "ingestion_accepted=true raw_accepted=false");
        }
        finally
        {
            await virtualHost.ClearPolicyAsync(
                policyName,
                CancellationToken.None);
        }
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task MandatoryPublish_SucceedsWhenOnlyIngestionBindingExists()
    {
        if (!IsAuditEnabled())
        {
            return;
        }

        var exchangeName = $"np.audit.ingestion-only.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseOptions,
            CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection("np-phase1-ingestion-only");
        using var inspectionChannel = connection.CreateModel();
        using var publisher = CreatePublisher(options);

        var topologyEnvelope = CreateEnvelope(-1);
        await publisher.PublishAsync(topologyEnvelope, CancellationToken.None);
        await AcknowledgeBothCopiesAsync(inspectionChannel, options, topologyEnvelope.EventId);

        inspectionChannel.QueueUnbind(
            options.ObservabilityRawQueueName,
            options.ExchangeName,
            RoutingKeys.SensorReadingProduced);

        var envelope = CreateEnvelope(1);
        await publisher.PublishAsync(envelope, CancellationToken.None);

        var ingestionCopy = await WaitForMessageAsync(
            inspectionChannel,
            options.IngestionReadingsQueueName,
            envelope.EventId);
        inspectionChannel.BasicAck(ingestionCopy.DeliveryTag, multiple: false);
        var rawState = inspectionChannel.QueueDeclarePassive(
            options.ObservabilityRawQueueName);

        Assert.Equal(0u, rawState.MessageCount);

        Console.WriteLine(
            "PHASE1_MANDATORY_PARTIAL_ROUTING_REPRODUCED " +
            "remaining_binding=ingestion publish_succeeded=true raw_copy=false");
    }

    [Fact]
    [Trait("Category", "DockerIntegration")]
    [Trait("Purpose", "OperationalAudit")]
    public async Task MandatoryPublish_SucceedsWhenOnlyRawBindingExists()
    {
        if (!IsAuditEnabled())
        {
            return;
        }

        var exchangeName = $"np.audit.raw-only.{Guid.NewGuid():N}";
        var baseOptions = DockerIntegrationSettings.CreateRabbitMqOptions(exchangeName);
        await using var virtualHost = await TemporaryRabbitMqVirtualHost.CreateAsync(
            baseOptions,
            CancellationToken.None);
        var options = virtualHost.CreateOptions(exchangeName);
        var factory = virtualHost.CreateConnectionFactory();

        using var connection = factory.CreateConnection("np-phase1-raw-only");
        using var inspectionChannel = connection.CreateModel();
        using var publisher = CreatePublisher(options);

        var topologyEnvelope = CreateEnvelope(-1);
        await publisher.PublishAsync(topologyEnvelope, CancellationToken.None);
        await AcknowledgeBothCopiesAsync(inspectionChannel, options, topologyEnvelope.EventId);

        inspectionChannel.QueueUnbind(
            options.IngestionReadingsQueueName,
            options.ExchangeName,
            RoutingKeys.SensorReadingProduced);

        var envelope = CreateEnvelope(1);
        await publisher.PublishAsync(envelope, CancellationToken.None);

        var rawCopy = await WaitForMessageAsync(
            inspectionChannel,
            options.ObservabilityRawQueueName,
            envelope.EventId);
        inspectionChannel.BasicAck(rawCopy.DeliveryTag, multiple: false);
        var ingestionState = inspectionChannel.QueueDeclarePassive(
            options.IngestionReadingsQueueName);

        Assert.Equal(0u, ingestionState.MessageCount);

        Console.WriteLine(
            "PHASE1_MANDATORY_WRONG_DESTINATION_REPRODUCED " +
            "remaining_binding=raw publish_succeeded=true ingestion_copy=false");
    }

    private static RabbitMqReadingPublisher CreatePublisher(RabbitMqOptions options)
    {
        return new RabbitMqReadingPublisher(
            NullLogger<RabbitMqReadingPublisher>.Instance,
            Options.Create(options));
    }

    private static bool IsAuditEnabled()
    {
        var enabled = string.Equals(
            Environment.GetEnvironmentVariable(AuditEnvironmentVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!enabled)
        {
            Console.WriteLine(
                $"SKIPPED_ENV_REQUIRED: set {AuditEnvironmentVariable}=true " +
                "to execute destructive-local operational characterization tests.");
        }

        return enabled;
    }

    private static async Task AcknowledgeBothCopiesAsync(
        IModel channel,
        RabbitMqOptions options,
        Guid eventId)
    {
        var ingestionCopy = await WaitForMessageAsync(
            channel,
            options.IngestionReadingsQueueName,
            eventId);
        channel.BasicAck(ingestionCopy.DeliveryTag, multiple: false);

        var rawCopy = await WaitForMessageAsync(
            channel,
            options.ObservabilityRawQueueName,
            eventId);
        channel.BasicAck(rawCopy.DeliveryTag, multiple: false);
    }

    private static async Task<BasicGetResult> WaitForMessageAsync(
        IModel channel,
        string queueName,
        Guid eventId)
    {
        for (var attempt = 0; attempt < 80; attempt++)
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

        throw new TimeoutException(
            $"RabbitMQ message {eventId} was not observed on queue {queueName}.");
    }

    private static bool BodyHasEventId(ReadOnlyMemory<byte> body, Guid eventId)
    {
        try
        {
            var envelope = JsonEventSerializer.Deserialize<
                EventEnvelope<SensorReadingProducedPayload>>(body.ToArray());
            return envelope?.EventId == eventId;
        }
        catch
        {
            return false;
        }
    }

    private static EventEnvelope<SensorReadingProducedPayload> CreateEnvelope(int sequence)
    {
        var eventId = Guid.NewGuid();
        var timestamp = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero)
            .AddSeconds(sequence);

        return new EventEnvelope<SensorReadingProducedPayload>(
            SchemaVersion: "1.0",
            EventId: eventId,
            CorrelationId: $"phase1-rabbitmq-{sequence}-{eventId:N}",
            Producer: "NatureProtector.OperationalAudit",
            EventType: EventTypes.SensorReadingProduced,
            AreaId: Guid.NewGuid(),
            EventTime: timestamp,
            IngestTime: timestamp,
            Payload: new SensorReadingProducedPayload(
                SimulationRunId: Guid.NewGuid(),
                SensorId: Guid.NewGuid(),
                SensorName: $"Phase1-Sensor-{sequence}",
                MetricType: SensorMetricType.Temperature,
                Unit: MeasurementUnit.Celsius,
                Value: 30 + sequence,
                Latitude: 39.8,
                Longitude: -7.9,
                OperationalState: SensorOperationalState.Nominal));
    }
}
