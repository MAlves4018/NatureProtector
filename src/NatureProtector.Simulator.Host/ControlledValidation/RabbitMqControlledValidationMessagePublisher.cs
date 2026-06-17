using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.Publishing;
using RabbitMQ.Client;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class RabbitMqControlledValidationMessagePublisher(
    ILogger<RabbitMqControlledValidationMessagePublisher> logger,
    IOptions<RabbitMqOptions> rabbitMqOptions) : IControlledValidationMessagePublisher, IDisposable
{
    private readonly RabbitMqOptions _options = rabbitMqOptions.Value;
    private readonly object _syncRoot = new();

    private IConnection? _connection;
    private IModel? _channel;
    private bool _disposed;

    public Task PublishAsync(
        ControlledValidationMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            EnsureChannel();

            var channel = _channel
                ?? throw new InvalidOperationException("RabbitMQ channel was not initialized.");
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.ContentEncoding = "utf-8";
            properties.MessageId = message.EventId?.ToString();
            properties.CorrelationId = message.CorrelationId;
            properties.Type = EventTypes.SensorReadingProduced;
            properties.Headers = new Dictionary<string, object>
            {
                ["controlled_validation"] = "p0",
                ["fault_case_id"] = message.FaultCase.FaultCaseId,
                ["fault_layer"] = message.FaultCase.FaultLayer.ToString(),
                ["expected_outcome"] = message.FaultCase.ExpectedOutcome.ToString(),
                ["raw_body_sha256"] = message.BodySha256
            };

            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                _options.ExchangeName,
                RoutingKeys.SensorReadingProduced,
                properties,
                message.Body,
                TimeSpan.FromSeconds(_options.PublisherConfirmTimeoutSeconds),
                $"controlled validation message {message.FaultCase.FaultCaseId}/{message.Sequence}");
        }

        logger.LogInformation(
            "Published controlled validation P0 message | FaultCaseId={FaultCaseId} | Sequence={Sequence} | EventId={EventId} | CorrelationId={CorrelationId} | BodySha256={BodySha256}",
            message.FaultCase.FaultCaseId,
            message.Sequence,
            message.EventId,
            message.CorrelationId,
            message.BodySha256);

        return Task.CompletedTask;
    }

    private void EnsureChannel()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RabbitMqControlledValidationMessagePublisher));
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

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            DeclareTopology(_channel);
            RabbitMqPublishGuarantees.EnablePublisherConfirms(_channel);

            logger.LogInformation(
                "RabbitMQ controlled validation publisher connected | Host={Host} | Port={Port} | Exchange={Exchange}",
                _options.HostName,
                _options.Port,
                _options.ExchangeName);
        }
    }

    private void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(
            exchange: _options.ExchangeName,
            type: NatureProtectorRabbitMqTopology.ExchangeType,
            durable: true,
            autoDelete: false);

        channel.QueueDeclare(
            queue: _options.IngestionReadingsQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        channel.QueueDeclare(
            queue: _options.ObservabilityRawQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        foreach (var (queueName, routingKey) in _options.GetBindings())
        {
            channel.QueueBind(
                queue: queueName,
                exchange: _options.ExchangeName,
                routingKey: routingKey);
        }
    }

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
