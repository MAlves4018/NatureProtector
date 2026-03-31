using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;

/*
 * This publisher is responsible for sending generated reading events to RabbitMQ.
 *
 * Rationale:
 * - The simulation host should not contain broker-specific publication logic
 *   inside the orchestration layer.
 * - By isolating RabbitMQ publication in a dedicated publisher, the execution
 *   flow remains cleaner and easier to evolve.
 *
 * Design considerations:
 * - The publisher keeps a connection and a channel for reuse across the host lifetime.
 * - Topology is declared when the channel is created, ensuring the required exchange,
 *   queues and bindings exist before publication starts.
 * - The implementation is intentionally synchronous internally because the RabbitMQ
 *   client used here exposes synchronous publishing primitives, but the public
 *   contract remains asynchronous for consistency with the rest of the host.
 */

namespace NatureProtector.Simulator.Host.Publishing;

public sealed class RabbitMqReadingPublisher(
    ILogger<RabbitMqReadingPublisher> logger,
    IOptions<RabbitMqOptions> rabbitMqOptions) : IReadingPublisher, IDisposable
{
    private readonly RabbitMqOptions _options = rabbitMqOptions.Value;
    private readonly object _syncRoot = new();

    private IConnection? _connection;
    private IModel? _channel;
    private bool _disposed;

    /// <summary>
    /// Publishes one generated reading envelope to RabbitMQ.
    /// </summary>
    /// <param name="envelope">
    /// Event envelope containing the generated sensor reading payload.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation token used for graceful shutdown.
    /// </param>
    public Task PublishAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureChannel();

        var channel = _channel
            ?? throw new InvalidOperationException("RabbitMQ channel was not initialized.");

        var body = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = envelope.EventId.ToString();
        properties.CorrelationId = envelope.CorrelationId;
        properties.Type = envelope.EventType;
        properties.Timestamp = new AmqpTimestamp(envelope.EventTime.ToUnixTimeSeconds());

        channel.BasicPublish(
            exchange: _options.ExchangeName,
            routingKey: RoutingKeys.SensorReadingProduced,
            basicProperties: properties,
            body: body);

        logger.LogInformation(
            "Published {EventType} to RabbitMQ | EventId={EventId} | CorrelationId={CorrelationId} | SensorId={SensorId} | SensorName={SensorName} | Value={Value}",
            envelope.EventType,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.Payload.SensorId,
            envelope.Payload.SensorName,
            envelope.Payload.Value);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Ensures that a valid RabbitMQ connection and channel are available.
    /// </summary>
    private void EnsureChannel()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqReadingPublisher));
        }

        if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
            {
                return;
            }

            DisposeConnectionObjects();

            if (string.IsNullOrWhiteSpace(_options.HostName))
            {
                throw new InvalidOperationException("RabbitMQ HostName is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_options.UserName))
            {
                throw new InvalidOperationException("RabbitMQ UserName is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_options.Password))
            {
                throw new InvalidOperationException("RabbitMQ Password is not configured.");
            }

            if (string.IsNullOrWhiteSpace(_options.ExchangeName))
            {
                throw new InvalidOperationException("RabbitMQ ExchangeName is not configured.");
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            DeclareTopology(_channel);

            logger.LogInformation(
                "RabbitMQ publisher connected | Host={Host} | Port={Port} | Exchange={Exchange}",
                _options.HostName,
                _options.Port,
                _options.ExchangeName);
        }
    }

    /// <summary>
    /// Declares the exchange, queues and bindings required by the platform.
    /// </summary>
    /// <param name="channel">
    /// RabbitMQ channel used to declare topology.
    /// </param>
    private void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(
            exchange: _options.ExchangeName,
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
                exchange: _options.ExchangeName,
                routingKey: routingKey);
        }
    }

    /// <summary>
    /// Disposes the currently open channel and connection, if any.
    /// </summary>
    private void DisposeConnectionObjects()
    {
        try
        {
            _channel?.Dispose();
        }
        finally
        {
            _channel = null;
        }

        try
        {
            _connection?.Dispose();
        }
        finally
        {
            _connection = null;
        }
    }

    /// <summary>
    /// Releases RabbitMQ resources owned by this publisher.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            DisposeConnectionObjects();
            _disposed = true;
        }
    }
}