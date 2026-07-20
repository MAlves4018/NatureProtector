using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using NatureProtector.Shared.Observability;
using RabbitMQ.Client;
using System.Diagnostics;

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
        using var activity = SimulatorHostTelemetry.ActivitySource.StartActivity("natureprotector.simulator.publish");
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag(TelemetryTags.EventId, envelope.EventId);
        activity?.SetTag(TelemetryTags.CorrelationId, envelope.CorrelationId);
        activity?.SetTag(TelemetryTags.AreaId, envelope.AreaId);
        activity?.SetTag(TelemetryTags.SimulationRunId, envelope.Payload.SimulationRunId);
        activity?.SetTag(TelemetryTags.SensorId, envelope.Payload.SensorId);
        activity?.SetTag(TelemetryTags.SensorName, envelope.Payload.SensorName);
        activity?.SetTag(TelemetryTags.MetricType, envelope.Payload.MetricType);

        lock (_syncRoot)
        {
            EnsureChannel();

            var channel = _channel
                ?? throw new InvalidOperationException("RabbitMQ channel was not initialized.");

            var publishedAt = DateTimeOffset.UtcNow;
            var publishedEnvelope = envelope with { PublishedAt = publishedAt };
            var body = JsonEventSerializer.SerializeToUtf8Bytes(publishedEnvelope);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.ContentEncoding = "utf-8";
            properties.MessageId = envelope.EventId.ToString();
            properties.CorrelationId = envelope.CorrelationId;
            properties.Type = envelope.EventType;
            properties.Timestamp = new AmqpTimestamp(publishedAt.ToUnixTimeSeconds());

            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                _options.ExchangeName,
                RoutingKeys.SensorReadingProduced,
                _options.IngestionReadingsQueueName,
                properties,
                body,
                TimeSpan.FromSeconds(_options.PublisherConfirmTimeoutSeconds),
                $"reading event {publishedEnvelope.EventId}");
        }

        logger.LogInformation(
            "Published {EventType} to RabbitMQ | EventId={EventId} | CorrelationId={CorrelationId} | SensorId={SensorId} | SensorName={SensorName} | Value={Value}",
            envelope.EventType,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.Payload.SensorId,
            envelope.Payload.SensorName,
            envelope.Payload.Value);

        stopwatch.Stop();
        SimulatorHostTelemetry.PublishedMessages.Add(1);
        SimulatorHostTelemetry.PublishDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);

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

            if (_options.PublisherConfirmTimeoutSeconds <= 0)
            {
                throw new InvalidOperationException(
                    "RabbitMQ PublisherConfirmTimeoutSeconds must be greater than zero.");
            }

            var factory = CreateConnectionFactory(_options);

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            DeclareTopology(_channel);
            RabbitMqPublishGuarantees.EnablePublisherConfirms(_channel);

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

        foreach (var queueName in _options.GetQueueNames())
        {
            channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false);
        }

        foreach (var (queueName, routingKey) in _options.GetBindings())
        {
            channel.QueueBind(
                queue: queueName,
                exchange: _options.ExchangeName,
                routingKey: routingKey);
        }
    }

    internal static ConnectionFactory CreateConnectionFactory(RabbitMqOptions options)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost
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
