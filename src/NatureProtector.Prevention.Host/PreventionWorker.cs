using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
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
    IReadingEventInbox readingEventInbox,
    ReadingEventProcessingService processingService) : BackgroundService
{
    private readonly RabbitMqOptions _options = rabbitMqOptions.Value;

    /// <summary>
    /// Mantém ativo o consumidor RabbitMQ durante a vida do host.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Prevention worker started at: {Time}", DateTimeOffset.Now);

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true
        };

        var connection = factory.CreateConnection();
        var channel = connection.CreateModel();

        DeclareTopology(channel);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += (_, ea) => HandleReceivedAsync(channel, ea, stoppingToken);

        channel.BasicConsume(
            queue: NatureProtectorRabbitMqTopology.IngestionReadingsQueue,
            autoAck: false,
            consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        channel.Close();
        connection.Close();
        channel.Dispose();
        connection.Dispose();
    }

    /// <summary>
    /// Declara a topologia mínima exigida pelo fluxo de prevenção.
    /// </summary>
    private static void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(
            exchange: NatureProtectorRabbitMqTopology.ExchangeName,
            type: NatureProtectorRabbitMqTopology.ExchangeType,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: NatureProtectorRabbitMqTopology.IngestionReadingsQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueDeclare(
            queue: NatureProtectorRabbitMqTopology.ObservabilityRawQueue,
            durable: true,
            exclusive: false,
            autoDelete: false);

        foreach (var (queueName, routingKey) in NatureProtectorRabbitMqTopology.Bindings)
        {
            channel.QueueBind(
                queue: queueName,
                exchange: NatureProtectorRabbitMqTopology.ExchangeName,
                routingKey: routingKey);
        }
    }

    private async Task HandleReceivedAsync(
        IModel channel,
        BasicDeliverEventArgs ea,
        CancellationToken stoppingToken)
    {
        var ackSent = false;

        try
        {
            EventEnvelope<SensorReadingProducedPayload>? envelope;

            try
            {
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
                    stoppingToken);

                channel.BasicAck(ea.DeliveryTag, multiple: false);
                return;
            }

            if (envelope is null)
            {
                logger.LogWarning(
                    "Received null or invalid message body. DeliveryTag={DeliveryTag}",
                    ea.DeliveryTag);

                await readingEventInbox.StoreRejectedAsync(
                    ea.Body,
                    "null_envelope",
                    "The message body deserialized to a null envelope.",
                    stoppingToken);

                channel.BasicAck(ea.DeliveryTag, multiple: false);
                return;
            }

            logger.LogInformation(
                "Consumed {EventType} | EventId={EventId} | CorrelationId={CorrelationId} | Sensor={SensorName} | Metric={MetricType} | Value={Value} | EventTime={EventTime}",
                envelope.EventType,
                envelope.EventId,
                envelope.CorrelationId,
                envelope.Payload.SensorName,
                envelope.Payload.MetricType,
                envelope.Payload.Value,
                envelope.EventTime);

            var storeResult = await readingEventInbox.StoreIncomingAsync(
                envelope,
                ea.Body,
                "reading_risk_pipeline",
                stoppingToken);

            // O broker só recebe ack depois de o evento ficar materializado no
            // inbox, para que o fluxo operacional consiga retomar trabalho em caso de falha.
            channel.BasicAck(ea.DeliveryTag, multiple: false);
            ackSent = true;

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
                channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
            }
        }
    }
}
