using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Validation;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

/*
 * This background worker consumes sensor-reading events from RabbitMQ,
 * validates them and persists the accepted ones.
 *
 * Rationale:
 * - Day 5 requires the Prevention.Host to close the ingestion loop by
 *   consuming readings, validating them and persisting accepted data.
 *
 * Design considerations:
 * - Messages are manually acknowledged only after processing is completed.
 * - Business rejections are logged and acknowledged, not requeued.
 * - Unexpected failures are negatively acknowledged and may be requeued
 *   according to configuration.
 */

namespace NatureProtector.Prevention.Host;

public sealed class ReadingIngestionWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<ReadingIngestionWorker> _logger;
    private readonly RabbitMqOptions _rabbitMqOptions;
    private readonly PreventionOptions _preventionOptions;
    private readonly IReadingValidator _validator;
    private readonly IAcceptedReadingStore _store;

    private IConnection? _connection;
    private IModel? _channel;
    private string? _consumerTag;

    public ReadingIngestionWorker(
        ILogger<ReadingIngestionWorker> logger,
        IOptions<RabbitMqOptions> rabbitMqOptions,
        IOptions<PreventionOptions> preventionOptions,
        IReadingValidator validator,
        IAcceptedReadingStore store)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(rabbitMqOptions);
        ArgumentNullException.ThrowIfNull(preventionOptions);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(store);

        _logger = logger;
        _rabbitMqOptions = rabbitMqOptions.Value
            ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
        _preventionOptions = preventionOptions.Value
            ?? throw new ArgumentNullException(nameof(preventionOptions));
        _validator = validator;
        _store = store;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reading ingestion worker starting at {Time}.",
            DateTimeOffset.UtcNow);

        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqOptions.HostName,
            Port = _rabbitMqOptions.Port,
            UserName = _rabbitMqOptions.UserName,
            Password = _rabbitMqOptions.Password
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        DeclareTopology(_channel);

        _channel.BasicQos(
            prefetchSize: 0,
            prefetchCount: _preventionOptions.PrefetchCount,
            global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += HandleReceivedMessage;

        _consumerTag = _channel.BasicConsume(
            queue: _preventionOptions.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            "RabbitMQ consumer connected | Host={Host} | Port={Port} | Queue={Queue} | Prefetch={Prefetch}",
            _rabbitMqOptions.HostName,
            _rabbitMqOptions.Port,
            _preventionOptions.QueueName,
            _preventionOptions.PrefetchCount);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Reading ingestion worker is stopping.");
        }
        finally
        {
            CloseResources();
        }
    }

    private void HandleReceivedMessage(object? sender, BasicDeliverEventArgs eventArgs)
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            var envelope = DeserializeEnvelope(eventArgs.Body);

            var validationResult = _validator.Validate(envelope);

            if (!validationResult.IsAccepted)
            {
                _logger.LogWarning(
                    "Reading rejected | EventId={EventId} | CorrelationId={CorrelationId} | Reason={Reason}",
                    envelope?.EventId,
                    envelope?.CorrelationId,
                    validationResult.RejectionReason);

                _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
                return;
            }

            var record = AcceptedReadingRecord.FromEnvelope(envelope!);
            _store.Persist(record);

            _logger.LogInformation(
                "Reading accepted and persisted | EventId={EventId} | CorrelationId={CorrelationId} | SensorId={SensorId} | Metric={Metric} | Value={Value}",
                record.EventId,
                record.CorrelationId,
                record.SensorId,
                record.MetricType,
                record.Value);

            _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Reading rejected due to invalid JSON payload.");

            _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected failure while processing reading message.");

            _channel.BasicNack(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: _preventionOptions.RequeueOnUnexpectedFailure);
        }
    }

    private static EventEnvelope<SensorReadingProducedPayload>? DeserializeEnvelope(
        ReadOnlyMemory<byte> body)
    {
        var json = Encoding.UTF8.GetString(body.Span);

        return JsonSerializer.Deserialize<EventEnvelope<SensorReadingProducedPayload>>(
            json,
            JsonOptions);
    }

    private void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(
            exchange: _rabbitMqOptions.ExchangeName,
            type: NatureProtectorRabbitMqTopology.ExchangeType,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: _preventionOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueBind(
            queue: _preventionOptions.QueueName,
            exchange: _rabbitMqOptions.ExchangeName,
            routingKey: RoutingKeys.SensorReadingProduced);
    }

    private void CloseResources()
    {
        try
        {
            if (_channel is { IsOpen: true } && !string.IsNullOrWhiteSpace(_consumerTag))
            {
                _channel.BasicCancel(_consumerTag);
            }
        }
        catch
        {
        }

        try
        {
            if (_channel is not null)
            {
                _channel.Close();
                _channel.Dispose();
            }
        }
        catch
        {
        }

        try
        {
            if (_connection is not null)
            {
                _connection.Close();
                _connection.Dispose();
            }
        }
        catch
        {
        }
    }
}