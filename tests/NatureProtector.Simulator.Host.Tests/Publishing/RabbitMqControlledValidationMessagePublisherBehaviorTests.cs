using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;
using NatureProtector.Simulator.Host.ControlledValidation;
using NatureProtector.Simulator.Host.Tests.Helpers;
using RabbitMQ.Client;

namespace NatureProtector.Simulator.Host.Tests.Publishing;

public sealed class RabbitMqControlledValidationMessagePublisherBehaviorTests
{
    [Fact]
    public async Task PublishAsync_UsesJsonMetadataAndControlledValidationHeaders()
    {
        var options = new RabbitMqOptions
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "np",
            Password = "pass",
            ExchangeName = NatureProtectorRabbitMqTopology.ExchangeName
        };
        using var publisher = new RabbitMqControlledValidationMessagePublisher(
            NullLogger<RabbitMqControlledValidationMessagePublisher>.Instance,
            Options.Create(options));
        var (connection, connectionRecorder) = RecordingDispatchProxy<IConnection>.CreateProxy();
        var (channel, channelRecorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var (properties, propertiesRecorder) = RecordingDispatchProxy<IBasicProperties>.CreateProxy();
        connectionRecorder.Properties["IsOpen"] = true;
        channelRecorder.Properties["IsOpen"] = true;
        channelRecorder.ReturnValues["CreateBasicProperties"] = properties;
        SetPrivateField(publisher, "_connection", connection);
        SetPrivateField(publisher, "_channel", channel);
        var message = CreateMessage();

        await publisher.PublishAsync(message, CancellationToken.None);

        var publish = Assert.Single(channelRecorder.Invocations, x => x.MethodName == "BasicPublish");
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeName, Assert.IsType<string>(publish.Arguments[0]));
        Assert.Equal(RoutingKeys.SensorReadingProduced, Assert.IsType<string>(publish.Arguments[1]));
        Assert.True(Assert.IsType<bool>(publish.Arguments[2]));
        Assert.True(Assert.IsType<bool>(propertiesRecorder.Properties["Persistent"]));
        Assert.Equal("application/json", Assert.IsType<string>(propertiesRecorder.Properties["ContentType"]));
        Assert.Equal("utf-8", Assert.IsType<string>(propertiesRecorder.Properties["ContentEncoding"]));
        Assert.Equal(message.EventId!.Value.ToString(), Assert.IsType<string>(propertiesRecorder.Properties["MessageId"]));
        Assert.Equal(message.CorrelationId, Assert.IsType<string>(propertiesRecorder.Properties["CorrelationId"]));
        Assert.Equal(EventTypes.SensorReadingProduced, Assert.IsType<string>(propertiesRecorder.Properties["Type"]));

        var headers = Assert.IsAssignableFrom<IDictionary<string, object>>(propertiesRecorder.Properties["Headers"]);
        Assert.Equal("p0", Assert.IsType<string>(headers["controlled_validation"]));
        Assert.Equal(message.FaultCase.FaultCaseId, Assert.IsType<string>(headers["fault_case_id"]));
        Assert.Equal(message.BodySha256, Assert.IsType<string>(headers["raw_body_sha256"]));

        var confirm = Assert.Single(channelRecorder.Invocations, x => x.MethodName == "WaitForConfirmsOrDie");
        Assert.Equal(
            TimeSpan.FromSeconds(options.PublisherConfirmTimeoutSeconds),
            Assert.IsType<TimeSpan>(confirm.Arguments[0]));
        Assert.Single(channelRecorder.Invocations, x => x.MethodName == "add_BasicReturn");
        Assert.Single(channelRecorder.Invocations, x => x.MethodName == "remove_BasicReturn");
    }

    private static ControlledValidationMessage CreateMessage()
    {
        var faultCase = new ValidationFaultCase(
            "TEST_FAULT",
            ControlledValidationFaultLayer.EventTransport,
            ControlledValidationExpectedOutcome.Rejected,
            "invalid_json",
            "test controlled validation message");

        return new ControlledValidationMessage(
            faultCase,
            Sequence: 1,
            Kind: ControlledValidationMessageKind.RawInvalidJson,
            EventId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            CorrelationId: "cv:test:TEST_FAULT:001",
            Body: Encoding.UTF8.GetBytes("{ invalid"),
            BodySha256: "sha256-test",
            IsSetupMessage: false);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }
}
