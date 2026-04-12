using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;

/*
 * Este publisher envia os eventos de leitura gerados para RabbitMQ.
 *
 * Rationale:
 * - O simulador não deve conter lógica específica do broker na camada de
 *   orquestração.
 * - Isolar a publicação em RabbitMQ mantém o fluxo de execução mais limpo e
 *   mais fácil de evoluir.
 *
 * Design considerations:
 * - O publisher mantém ligação e canal reutilizáveis ao longo da vida do host.
 * - A topologia é declarada quando o canal é criado, garantindo que exchange,
 *   filas e bindings existem antes da publicação.
 * - A implementação é síncrona internamente porque o cliente usado expõe essas
 *   primitivas, mas o contrato público mantém-se assíncrono por consistência.
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
    /// Publica um envelope de leitura gerado em RabbitMQ.
    /// </summary>
    /// <param name="envelope">
    /// Envelope de evento com o payload da leitura simulada.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelamento usado para encerramento gracioso.
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
    /// Garante que existe uma ligação e um canal RabbitMQ válidos.
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
    /// Declara o exchange, as filas e as bindings necessárias à plataforma.
    /// </summary>
    /// <param name="channel">
    /// Canal RabbitMQ usado para declarar a topologia.
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
    /// Liberta o canal e a ligação atualmente abertos, se existirem.
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
    /// Liberta os recursos RabbitMQ detidos por este publisher.
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
