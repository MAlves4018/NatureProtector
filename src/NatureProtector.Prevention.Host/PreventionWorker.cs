using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace NatureProtector.Prevention.Host;

public sealed class PreventionWorker(
    ILogger<PreventionWorker> logger,
    IOptions<RabbitMqOptions> rabbitMqOptions,
    ReadingRiskPipeline readingRiskPipeline) : BackgroundService
{
    private readonly RabbitMqOptions _options = rabbitMqOptions.Value;

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
        try
        {
            var envelope = JsonEventSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(ea.Body);

            if (envelope is null)
            {
                logger.LogWarning(
                    "Received null or invalid message body. DeliveryTag={DeliveryTag}",
                    ea.DeliveryTag);

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

            await readingRiskPipeline.ProcessAcceptedReadingAsync(
                envelope,
                stoppingToken);

            channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to consume message. DeliveryTag={DeliveryTag} Body={Body}",
                ea.DeliveryTag,
                Encoding.UTF8.GetString(ea.Body.ToArray()));

            channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }
}
