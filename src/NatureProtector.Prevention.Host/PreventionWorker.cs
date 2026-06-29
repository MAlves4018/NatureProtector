using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Shared.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace NatureProtector.Prevention.Host;

/*
 * Este worker consome eventos do broker e entrega-os ao fluxo de prevenção.
 *
 * Rationale:
 * - O consumidor RabbitMQ precisa de ficar separado do processamento de risco
 *   para manter a fronteira entre transporte e negócio.
 * - O worker também é responsável por decidir quando um evento deve ser
 *   rejeitado logo à entrada e quando deve seguir para o inbox.
 *
 * Design considerations:
 * - A fila é consumida com ack manual para controlar melhor o momento em que o
 *   broker considera o evento tratado.
 * - Mensagens inválidas são registadas como rejeitadas antes do ack.
 * - O ack só é enviado depois de o evento ficar materializado no inbox, o que
 *   reduz o risco de perda entre broker e processamento operacional.
 */

public sealed class PreventionWorker(
    ILogger<PreventionWorker> logger,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    IOptions<PreventionHostOptions> preventionHostOptions,
    IReadingEventInbox readingEventInbox,
    ReadingEventProcessingService processingService) : BackgroundService
{
    private const string SupportedSchemaVersion = "1.0";

    private readonly RabbitMqOptions _options = rabbitMqOptions.Value;
    private readonly PreventionHostOptions _preventionHostOptions = preventionHostOptions.Value;

    /// <summary>
    /// Mantém ativo o consumidor RabbitMQ durante a vida do host.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Prevention worker started at: {Time}", DateTimeOffset.Now);

        var factory = CreateConnectionFactory(_options);

        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        try
        {
            DeclareTopology(channel, _options);

            // Um prefetch baixo limita o backlog invisivel de mensagens por
            // materializar quando o inbox ou a base de dados abrandam.
            channel.BasicQos(
                prefetchSize: 0,
                prefetchCount: _preventionHostOptions.ConsumerPrefetchCount,
                global: false);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += (_, ea) => HandleReceivedAsync(channel, ea, stoppingToken);

            channel.BasicConsume(
                queue: _options.IngestionReadingsQueueName,
                autoAck: false,
                consumer: consumer);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown path.
        }
        finally
        {
            if (channel.IsOpen)
            {
                channel.Close();
            }

            if (connection.IsOpen)
            {
                connection.Close();
            }

            channel.Dispose();
            connection.Dispose();
        }
    }

    /// <summary>
    /// Declara a topologia mínima exigida pelo fluxo de prevenção.
    /// </summary>
    private static void DeclareTopology(IModel channel, RabbitMqOptions options)
    {
        channel.ExchangeDeclare(
            exchange: options.ExchangeName,
            type: NatureProtectorRabbitMqTopology.ExchangeType,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: options.IngestionReadingsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueDeclare(
            queue: options.ObservabilityRawQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        foreach (var (queueName, routingKey) in options.GetBindings())
        {
            channel.QueueBind(
                queue: queueName,
                exchange: options.ExchangeName,
                routingKey: routingKey);
        }
    }

    internal static ConnectionFactory CreateConnectionFactory(RabbitMqOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.HostName))
        {
            throw new InvalidOperationException("RabbitMQ HostName is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.UserName))
        {
            throw new InvalidOperationException("RabbitMQ UserName is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException("RabbitMQ Password is not configured.");
        }

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            DispatchConsumersAsync = true
        };

        if (options.TlsEnabled)
        {
            if (string.IsNullOrWhiteSpace(options.TlsServerName))
            {
                throw new InvalidOperationException("RabbitMQ TlsServerName is required when TLS is enabled.");
            }

            if (string.IsNullOrWhiteSpace(options.TlsCertificateAuthorityPath))
            {
                throw new InvalidOperationException("RabbitMQ TlsCertificateAuthorityPath is required when TLS is enabled.");
            }

            var validator = PrivateCertificateAuthorityValidator.Create(options.TlsCertificateAuthorityPath);
            factory.Ssl = new SslOption
            {
                Enabled = true,
                ServerName = options.TlsServerName,
                CertificateValidationCallback = validator is null ? null : validator.Validate
            };
        }

        return factory;
    }

    private async Task HandleReceivedAsync(
        IModel channel,
        BasicDeliverEventArgs ea,
        CancellationToken stoppingToken)
    {
        using var receiveActivity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.receive");
        receiveActivity?.SetTag(TelemetryTags.Stage, "broker_receive");
        PreventionHostTelemetry.ReceivedEvents.Add(1);
        var ackSent = false;

        try
        {
            EventEnvelope<SensorReadingProducedPayload>? envelope = null;

            try
            {
                using var deserializeActivity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.deserialize");
                envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(ea.Body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Received invalid JSON payload. DeliveryTag={DeliveryTag}",
                    ea.DeliveryTag);

                await readingEventInbox.StoreRejectedAsync(
                    ea.Body,
                    "invalid_json",
                    "The message body could not be deserialized into a sensor reading envelope.",
                    null,
                    stoppingToken);
                PreventionHostTelemetry.RejectedEvents.Add(1, new TagList { { TelemetryTags.RejectionCode, "invalid_json" } });

                channel.BasicAck(ea.DeliveryTag, multiple: false);
                PreventionHostTelemetry.AckedEvents.Add(1, new TagList { { TelemetryTags.Outcome, "rejected" } });
                return;
            }

            if (envelope is null)
            {
                await RejectBeforeInboxAsync(
                    channel,
                    ea,
                    rejectionCode: "null_envelope",
                    rejectionReason: "The message body deserialized to a null envelope.",
                    stoppingToken);
                return;
            }

            if (!TryValidateEnvelope(envelope, out var validationFailure))
            {
                await RejectBeforeInboxAsync(
                    channel,
                    ea,
                    validationFailure.Code,
                    validationFailure.Reason,
                    stoppingToken,
                    envelope);
                return;
            }

            PreventionHostTelemetry.ValidatedEvents.Add(1);
            receiveActivity?.SetTag(TelemetryTags.EventId, envelope.EventId);
            receiveActivity?.SetTag(TelemetryTags.CorrelationId, envelope.CorrelationId);
            receiveActivity?.SetTag(TelemetryTags.AreaId, envelope.AreaId);
            receiveActivity?.SetTag(TelemetryTags.SensorId, envelope.Payload.SensorId);
            receiveActivity?.SetTag(TelemetryTags.SensorName, envelope.Payload.SensorName);
            receiveActivity?.SetTag(TelemetryTags.MetricType, envelope.Payload.MetricType);

            logger.LogInformation(
                "Accepted event contract | EventId={EventId} | EventType={EventType} | SchemaVersion={SchemaVersion} | CorrelationId={CorrelationId} | Sensor={SensorName} | Metric={MetricType} | Value={Value} | OperationalState={OperationalState} | EventTime={EventTime}",
                envelope.EventId,
                envelope.EventType,
                envelope.SchemaVersion,
                envelope.CorrelationId,
                envelope.Payload.SensorName,
                envelope.Payload.MetricType,
                envelope.Payload.Value,
                envelope.Payload.OperationalState,
                envelope.EventTime);

            var storeResult = await readingEventInbox.StoreIncomingAsync(
                envelope,
                ea.Body,
                "reading_risk_pipeline",
                stoppingToken);

            // O broker só recebe ack depois de o evento ficar materializado no
            // inbox, para que o fluxo operacional consiga retomar trabalho em caso de falha.
            using var ackActivity = PreventionHostTelemetry.ActivitySource.StartActivity("natureprotector.prevention.broker.ack");
            channel.BasicAck(ea.DeliveryTag, multiple: false);
            ackSent = true;
            PreventionHostTelemetry.AckedEvents.Add(1, new TagList { { TelemetryTags.Outcome, "stored" } });

            if (!storeResult.ShouldProcessNow || storeResult.Lease is null)
            {
                logger.LogInformation(
                    "Skipping duplicate inbox event | EventId={EventId} | InboxEventId={InboxEventId} | Status={Status}",
                    envelope.EventId,
                    storeResult.InboxEventId,
                    storeResult.Status);
                return;
            }

            await processingService.ProcessAsync(
                envelope,
                storeResult.Lease,
                stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                ackSent
                    ? "Failed after broker ack while dispatching to inbox processing. DeliveryTag={DeliveryTag} Body={Body}"
                    : "Failed to consume message. DeliveryTag={DeliveryTag} Body={Body}",
                ea.DeliveryTag,
                Encoding.UTF8.GetString(ea.Body.ToArray()));

            if (!ackSent)
            {
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        }
    }

    private async Task RejectBeforeInboxAsync(
        IModel channel,
        BasicDeliverEventArgs ea,
        string rejectionCode,
        string rejectionReason,
        CancellationToken stoppingToken,
        EventEnvelope<SensorReadingProducedPayload>? envelope = null)
    {
        logger.LogWarning(
            "Rejected event before inbox materialization | DeliveryTag={DeliveryTag} | RejectionCode={RejectionCode} | Reason={Reason} | EventId={EventId} | EventType={EventType} | SchemaVersion={SchemaVersion} | AreaId={AreaId} | SensorId={SensorId}",
            ea.DeliveryTag,
            rejectionCode,
            rejectionReason,
            envelope?.EventId,
            envelope?.EventType,
            envelope?.SchemaVersion,
            envelope?.AreaId,
            envelope?.Payload?.SensorId);
        PreventionHostTelemetry.RejectedEvents.Add(1, new TagList { { TelemetryTags.RejectionCode, rejectionCode } });

        await readingEventInbox.StoreRejectedAsync(
            ea.Body,
            rejectionCode,
            rejectionReason,
            envelope is null
                ? null
                : new RejectedEventMetadata(
                    EventId: envelope.EventId,
                    CorrelationId: envelope.CorrelationId,
                    Producer: envelope.Producer,
                    EventType: envelope.EventType,
                    AreaId: envelope.AreaId,
                    SchemaVersion: envelope.SchemaVersion,
                    SensorId: envelope.Payload?.SensorId,
                    SensorName: envelope.Payload?.SensorName,
                    MetricType: envelope.Payload is null ? null : envelope.Payload.MetricType.ToString(),
                    OperationalState: envelope.Payload is null ? null : envelope.Payload.OperationalState.ToString(),
                    Stage: "pre_inbox_validation",
                    DeliveryTag: ea.DeliveryTag),
            stoppingToken);

        channel.BasicAck(ea.DeliveryTag, multiple: false);
        PreventionHostTelemetry.AckedEvents.Add(1, new TagList { { TelemetryTags.Outcome, "rejected" } });
    }

    private static bool TryValidateEnvelope(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        out EnvelopeValidationFailure failure)
    {
        if (envelope.EventId == Guid.Empty)
        {
            failure = new EnvelopeValidationFailure(
                "invalid_event_id",
                "EventId must not be an empty GUID.");
            return false;
        }

        if (!string.Equals(envelope.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            failure = new EnvelopeValidationFailure(
                "unsupported_schema_version",
                $"SchemaVersion '{envelope.SchemaVersion}' is not supported.");
            return false;
        }

        if (!string.Equals(envelope.EventType, EventTypes.SensorReadingProduced, StringComparison.Ordinal))
        {
            failure = new EnvelopeValidationFailure(
                "unsupported_event_type",
                $"EventType '{envelope.EventType}' is not supported by the prevention consumer.");
            return false;
        }

        if (envelope.Payload is null)
        {
            failure = new EnvelopeValidationFailure(
                "missing_payload",
                "Payload is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId))
        {
            failure = new EnvelopeValidationFailure(
                "missing_correlation_id",
                "CorrelationId is required.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(envelope.Producer))
        {
            failure = new EnvelopeValidationFailure(
                "missing_producer",
                "Producer is required.");
            return false;
        }

        if (envelope.AreaId == Guid.Empty)
        {
            failure = new EnvelopeValidationFailure(
                "invalid_area_id",
                "AreaId must not be an empty GUID.");
            return false;
        }

        if (envelope.EventTime == default)
        {
            failure = new EnvelopeValidationFailure(
                "invalid_event_time",
                "EventTime is required.");
            return false;
        }

        var payload = envelope.Payload;

        if (!Enum.IsDefined(payload.MetricType))
        {
            failure = new EnvelopeValidationFailure(
                "invalid_metric_type",
                $"MetricType '{payload.MetricType:D}' is not defined.");
            return false;
        }

        if (!Enum.IsDefined(payload.Unit))
        {
            failure = new EnvelopeValidationFailure(
                "invalid_measurement_unit",
                $"Unit '{payload.Unit:D}' is not defined.");
            return false;
        }

        if (!Enum.IsDefined(payload.OperationalState))
        {
            failure = new EnvelopeValidationFailure(
                "invalid_operational_state",
                $"OperationalState '{payload.OperationalState:D}' is not defined.");
            return false;
        }

        if (payload.SimulationRunId == Guid.Empty)
        {
            failure = new EnvelopeValidationFailure(
                "missing_simulation_run_id",
                "SimulationRunId must not be an empty GUID.");
            return false;
        }

        if (payload.SensorId == Guid.Empty)
        {
            failure = new EnvelopeValidationFailure(
                "missing_sensor_id",
                "SensorId must not be an empty GUID.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload.SensorName))
        {
            failure = new EnvelopeValidationFailure(
                "missing_sensor_name",
                "SensorName is required.");
            return false;
        }

        if (payload.Latitude is < -90 or > 90 || payload.Longitude is < -180 or > 180)
        {
            failure = new EnvelopeValidationFailure(
                "invalid_coordinates",
                "Latitude or Longitude is outside the supported range.");
            return false;
        }

        if (payload.OperationalState == SensorOperationalState.Invalid)
        {
            failure = new EnvelopeValidationFailure(
                "invalid_operational_state",
                "OperationalState 'Invalid' is rejected before the accepted-risk pipeline.");
            return false;
        }

        failure = EnvelopeValidationFailure.None;
        return true;
    }

    private sealed record EnvelopeValidationFailure(string Code, string Reason)
    {
        public static readonly EnvelopeValidationFailure None = new(string.Empty, string.Empty);
    }
}
