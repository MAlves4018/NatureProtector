using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Tests.Helpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NatureProtector.Simulator.Host.Tests.Publishing;

public sealed class RabbitMqPublishGuaranteesTests
{
    [Fact]
    public void EnablePublisherConfirms_CallsConfirmSelect()
    {
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        RabbitMqPublishGuarantees.EnablePublisherConfirms(channel);

        Assert.Single(recorder.Invocations, invocation => invocation.MethodName == "ConfirmSelect");
    }

    [Fact]
    public void PublishMandatoryAndWaitForConfirm_Throws_WhenReturnedMessageMatchesMessageId()
    {
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var properties = CreateProperties(messageId: "message-1", correlationId: "corr-1");
        var returnedProperties = CreateProperties(messageId: "message-1", correlationId: "different-corr");
        channelRecorder.Callbacks["BasicPublish"] = _ => RaiseReturn(
            channelRecorder,
            channel,
            returnedProperties,
            replyCode: 312,
            replyText: "NO_ROUTE",
            exchange: "np.events",
            routingKey: "sensor.reading");

        var exception = Assert.Throws<RabbitMqUnroutableMessageException>(() =>
            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                "np.events",
                "sensor.reading",
                "np.ingestion.readings",
                properties,
                ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(2),
                "sensor reading"));

        Assert.Contains("unroutable sensor reading", exception.Message);
        Assert.Contains("ReplyCode=312", exception.Message);
        Assert.Contains("RoutingKey='sensor.reading'", exception.Message);
        Assert.Equal(RabbitMqPublishDeliveryCertainty.NotDeliveredToAnyQueue, exception.DeliveryCertainty);
        Assert.False(exception.PossiblePartialDelivery);
        Assert.Equal("np.ingestion.readings", exception.PrimaryQueueName);
        Assert.Equal((ushort)312, exception.ReplyCode);
        Assert.Single(channelRecorder.Invocations, invocation => invocation.MethodName == "WaitForConfirmsOrDie");
        Assert.Single(channelRecorder.Invocations, invocation => invocation.MethodName == "remove_BasicReturn");
    }

    [Fact]
    public void PublishMandatoryAndWaitForConfirm_DoesNotThrow_WhenReturnedMessageIdDiffers()
    {
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var properties = CreateProperties(messageId: "message-1", correlationId: "corr-1");
        var returnedProperties = CreateProperties(messageId: "other-message", correlationId: "corr-1");
        channelRecorder.Callbacks["BasicPublish"] = _ => RaiseReturn(
            channelRecorder,
            channel,
            returnedProperties,
            replyCode: 312,
            replyText: "NO_ROUTE",
            exchange: "np.events",
            routingKey: "sensor.reading");

        RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
            channel,
            "np.events",
            "sensor.reading",
            "np.ingestion.readings",
            properties,
            ReadOnlyMemory<byte>.Empty,
            TimeSpan.FromSeconds(2),
            "sensor reading");

        Assert.Single(channelRecorder.Invocations, invocation => invocation.MethodName == "WaitForConfirmsOrDie");
        Assert.Single(channelRecorder.Invocations, invocation => invocation.MethodName == "remove_BasicReturn");
    }

    [Fact]
    public void PublishMandatoryAndWaitForConfirm_MatchesCorrelationId_WhenMessageIdIsMissing()
    {
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var properties = CreateProperties(messageId: null, correlationId: "corr-1");
        var returnedProperties = CreateProperties(messageId: "ignored-message", correlationId: "corr-1");
        channelRecorder.Callbacks["BasicPublish"] = _ => RaiseReturn(
            channelRecorder,
            channel,
            returnedProperties,
            replyCode: 312,
            replyText: "NO_ROUTE",
            exchange: "np.events",
            routingKey: "sensor.reading");

        Assert.Throws<RabbitMqUnroutableMessageException>(() =>
            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                "np.events",
                "sensor.reading",
                "np.ingestion.readings",
                properties,
                ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(2),
                "sensor reading"));
    }

    [Fact]
    public void PublishMandatoryAndWaitForConfirm_MatchesAnyReturnedMessage_WhenIdsAreMissing()
    {
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var properties = CreateProperties(messageId: null, correlationId: null);
        var returnedProperties = CreateProperties(messageId: "returned-message", correlationId: "returned-corr");
        channelRecorder.Callbacks["BasicPublish"] = _ => RaiseReturn(
            channelRecorder,
            channel,
            returnedProperties,
            replyCode: 312,
            replyText: "NO_ROUTE",
            exchange: "np.events",
            routingKey: "sensor.reading");

        Assert.Throws<RabbitMqUnroutableMessageException>(() =>
            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                "np.events",
                "sensor.reading",
                "np.ingestion.readings",
                properties,
                ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(2),
                "sensor reading"));
    }

    [Fact]
    public void PublishMandatoryAndWaitForConfirm_WrapsConfirmFailure_AsAmbiguousPossiblePartialDelivery()
    {
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var properties = CreateProperties(messageId: "message-ambiguous", correlationId: "corr-ambiguous");
        var confirmFailure = new InvalidOperationException("simulated basic.nack");
        channelRecorder.Callbacks["WaitForConfirmsOrDie"] = _ => throw confirmFailure;

        var exception = Assert.Throws<RabbitMqPublishOutcomeUnknownException>(() =>
            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                "np.events",
                "sensor.reading",
                "np.ingestion.readings",
                properties,
                ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(2),
                "sensor reading"));

        Assert.Same(confirmFailure, exception.InnerException);
        Assert.Equal("message-ambiguous", exception.MessageId);
        Assert.Equal("np.events", exception.ExchangeName);
        Assert.Equal("sensor.reading", exception.RoutingKey);
        Assert.Equal("np.ingestion.readings", exception.PrimaryQueueName);
        Assert.Equal(
            RabbitMqPublishDeliveryCertainty.UnknownPossiblePartialDelivery,
            exception.DeliveryCertainty);
        Assert.True(exception.PossiblePartialDelivery);
        Assert.Contains("one or more queues may already have accepted", exception.Message);
        Assert.Contains("preserve the same MessageId/EventId", exception.Message);
        Assert.Single(channelRecorder.Invocations, invocation => invocation.MethodName == "remove_BasicReturn");
    }

    [Fact]
    public void PublishMandatoryAndWaitForConfirm_PrioritizesDefiniteBasicReturn_OverConfirmFailure()
    {
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var properties = CreateProperties(messageId: "message-returned", correlationId: "corr-returned");
        channelRecorder.Callbacks["BasicPublish"] = _ => RaiseReturn(
            channelRecorder,
            channel,
            CreateProperties(messageId: "message-returned", correlationId: "corr-returned"),
            replyCode: 312,
            replyText: "NO_ROUTE",
            exchange: "np.events",
            routingKey: "sensor.reading");
        channelRecorder.Callbacks["WaitForConfirmsOrDie"] = _ =>
            throw new InvalidOperationException("channel closed after return");

        var exception = Assert.Throws<RabbitMqUnroutableMessageException>(() =>
            RabbitMqPublishGuarantees.PublishMandatoryAndWaitForConfirm(
                channel,
                "np.events",
                "sensor.reading",
                "np.ingestion.readings",
                properties,
                ReadOnlyMemory<byte>.Empty,
                TimeSpan.FromSeconds(2),
                "sensor reading"));

        Assert.False(exception.PossiblePartialDelivery);
        Assert.Equal(RabbitMqPublishDeliveryCertainty.NotDeliveredToAnyQueue, exception.DeliveryCertainty);
    }

    private static IBasicProperties CreateProperties(string? messageId, string? correlationId)
    {
        var (properties, recorder) = RecordingDispatchProxy<IBasicProperties>.CreateProxy();
        recorder.Properties["MessageId"] = messageId;
        recorder.Properties["CorrelationId"] = correlationId;
        return properties;
    }

    private static void RaiseReturn(
        RecordingDispatchProxy<IModel> channelRecorder,
        IModel channel,
        IBasicProperties basicProperties,
        ushort replyCode,
        string replyText,
        string exchange,
        string routingKey)
        => channelRecorder.RaiseEvent(
            "BasicReturn",
            channel,
            new BasicReturnEventArgs
            {
                BasicProperties = basicProperties,
                Body = ReadOnlyMemory<byte>.Empty,
                Exchange = exchange,
                ReplyCode = replyCode,
                ReplyText = replyText,
                RoutingKey = routingKey
            });
}
