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
        IBasicProperties properties,
        ReadOnlyMemory<byte> body,
        TimeSpan confirmTimeout,
        string messageDescription)
    {
        BasicReturnEventArgs? returned = null;

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
        finally
        {
            channel.BasicReturn -= OnBasicReturn;
        }

        if (returned is not null)
        {
            throw new InvalidOperationException(
                $"RabbitMQ returned unroutable {messageDescription}. " +
                $"ReplyCode={returned.ReplyCode} ReplyText='{returned.ReplyText}' " +
                $"Exchange='{returned.Exchange}' RoutingKey='{returned.RoutingKey}'.");
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
