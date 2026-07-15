using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NatureProtector.Simulator.Host.Publishing;

internal static class RabbitMqPublishGuarantees
{
    public static void EnablePublisherConfirms(IModel channel)
    {
        channel.ConfirmSelect();
    }

    public static void PublishMandatoryAndWaitForConfirm(
        IModel channel,
        string exchangeName,
        string routingKey,
        string primaryQueueName,
        IBasicProperties properties,
        ReadOnlyMemory<byte> body,
        TimeSpan confirmTimeout,
        string messageDescription)
    {
        BasicReturnEventArgs? returned = null;
        Exception? confirmFailure = null;

        void OnBasicReturn(object? _, BasicReturnEventArgs args)
        {
            if (IsMatchingReturnedMessage(args, properties))
            {
                returned = args;
            }
        }

        channel.BasicReturn += OnBasicReturn;

        try
        {
            channel.BasicPublish(
                exchange: exchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body);
            channel.WaitForConfirmsOrDie(confirmTimeout);
        }
        catch (Exception ex)
        {
            confirmFailure = ex;
        }
        finally
        {
            channel.BasicReturn -= OnBasicReturn;
        }

        if (returned is not null)
        {
            throw new RabbitMqUnroutableMessageException(
                $"RabbitMQ returned unroutable {messageDescription}. " +
                $"ReplyCode={returned.ReplyCode} ReplyText='{returned.ReplyText}' " +
                $"Exchange='{returned.Exchange}' RoutingKey='{returned.RoutingKey}'. " +
                $"The message was not routed to any queue, including primary queue '{primaryQueueName}'.",
                properties.MessageId,
                exchangeName,
                routingKey,
                primaryQueueName,
                returned.ReplyCode,
                returned.ReplyText);
        }

        if (confirmFailure is not null)
        {
            throw new RabbitMqPublishOutcomeUnknownException(
                $"RabbitMQ did not positively confirm {messageDescription}. " +
                $"Exchange='{exchangeName}' RoutingKey='{routingKey}' PrimaryQueue='{primaryQueueName}'. " +
                "Delivery is ambiguous: one or more queues may already have accepted the message. " +
                "A retry must preserve the same MessageId/EventId so the consumer inbox can deduplicate it.",
                properties.MessageId,
                exchangeName,
                routingKey,
                primaryQueueName,
                confirmFailure);
        }
    }

    private static bool IsMatchingReturnedMessage(
        BasicReturnEventArgs args,
        IBasicProperties expectedProperties)
    {
        if (!string.IsNullOrWhiteSpace(expectedProperties.MessageId))
        {
            return string.Equals(
                args.BasicProperties.MessageId,
                expectedProperties.MessageId,
                StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(expectedProperties.CorrelationId))
        {
            return string.Equals(
                args.BasicProperties.CorrelationId,
                expectedProperties.CorrelationId,
                StringComparison.Ordinal);
        }

        return true;
    }
}
