namespace NatureProtector.Simulator.Host.Publishing;

public enum RabbitMqPublishDeliveryCertainty
{
    NotDeliveredToAnyQueue = 0,
    UnknownPossiblePartialDelivery = 1
}

public abstract class RabbitMqPublishException : InvalidOperationException
{
    protected RabbitMqPublishException(
        string message,
        string? messageId,
        string exchangeName,
        string routingKey,
        string primaryQueueName,
        RabbitMqPublishDeliveryCertainty deliveryCertainty,
        Exception? innerException = null)
        : base(message, innerException)
    {
        MessageId = messageId;
        ExchangeName = exchangeName;
        RoutingKey = routingKey;
        PrimaryQueueName = primaryQueueName;
        DeliveryCertainty = deliveryCertainty;
    }

    public string? MessageId { get; }

    public string ExchangeName { get; }

    public string RoutingKey { get; }

    public string PrimaryQueueName { get; }

    public RabbitMqPublishDeliveryCertainty DeliveryCertainty { get; }

    public bool PossiblePartialDelivery =>
        DeliveryCertainty == RabbitMqPublishDeliveryCertainty.UnknownPossiblePartialDelivery;
}

public sealed class RabbitMqUnroutableMessageException : RabbitMqPublishException
{
    public RabbitMqUnroutableMessageException(
        string message,
        string? messageId,
        string exchangeName,
        string routingKey,
        string primaryQueueName,
        ushort replyCode,
        string replyText)
        : base(
            message,
            messageId,
            exchangeName,
            routingKey,
            primaryQueueName,
            RabbitMqPublishDeliveryCertainty.NotDeliveredToAnyQueue)
    {
        ReplyCode = replyCode;
        ReplyText = replyText;
    }

    public ushort ReplyCode { get; }

    public string ReplyText { get; }
}

public sealed class RabbitMqPublishOutcomeUnknownException : RabbitMqPublishException
{
    public RabbitMqPublishOutcomeUnknownException(
        string message,
        string? messageId,
        string exchangeName,
        string routingKey,
        string primaryQueueName,
        Exception innerException)
        : base(
            message,
            messageId,
            exchangeName,
            routingKey,
            primaryQueueName,
            RabbitMqPublishDeliveryCertainty.UnknownPossiblePartialDelivery,
            innerException)
    {
    }
}
