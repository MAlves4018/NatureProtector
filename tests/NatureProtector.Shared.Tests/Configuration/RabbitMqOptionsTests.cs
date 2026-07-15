using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Shared.Tests.Configuration;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void Defaults_AreAlignedWithCurrentLocalBaseline()
    {
        var options = new RabbitMqOptions();

        Assert.Equal("RabbitMq", RabbitMqOptions.SectionName);
        Assert.Equal("localhost", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("np", options.UserName);
        Assert.Equal("np_dev_pass", options.Password);
        Assert.Equal("/", options.VirtualHost);
        Assert.False(options.TlsEnabled);
        Assert.Null(options.TlsServerName);
        Assert.Null(options.TlsCertificateAuthorityPath);
        Assert.Equal("http", options.ManagementScheme);
        Assert.Null(options.ManagementHost);
        Assert.Equal(15672, options.ManagementPort);
        Assert.Null(options.ManagementUserName);
        Assert.Null(options.ManagementPassword);
        Assert.Null(options.ManagementCertificateAuthorityPath);
        Assert.False(options.ManagementCheckCertificateRevocation);
        Assert.False(options.ManagementAllowInsecureHttp);
        Assert.Equal(5, options.ManagementTimeoutSeconds);
        Assert.Equal("localhost", options.GetEffectiveManagementHost());
        Assert.Equal("np", options.GetEffectiveManagementUserName());
        Assert.Equal("np_dev_pass", options.GetEffectiveManagementPassword());
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeName, options.ExchangeName);
        Assert.Equal(10, options.PublisherConfirmTimeoutSeconds);
        Assert.Equal(NatureProtectorRabbitMqTopology.IngestionReadingsQueue, options.IngestionReadingsQueueName);
        Assert.False(options.ObservabilityRawEnabled);
        Assert.Equal(NatureProtectorRabbitMqTopology.ObservabilityRawQueue, options.ObservabilityRawQueueName);
        Assert.Collection(
            options.GetQueueNames(),
            queueName => Assert.Equal(
                NatureProtectorRabbitMqTopology.IngestionReadingsQueue,
                queueName));
        Assert.Collection(
            options.GetBindings(),
            binding =>
            {
                Assert.Equal(
                    NatureProtectorRabbitMqTopology.IngestionReadingsQueue,
                    binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            });
    }

    [Fact]
    public void GetQueueDefinitions_SeparatesPrimaryAndAuxiliaryRoles()
    {
        var options = new RabbitMqOptions();

        Assert.Collection(
            options.GetQueueDefinitions(),
            primary =>
            {
                Assert.Equal(NatureProtectorRabbitMqTopology.IngestionReadingsQueue, primary.QueueName);
                Assert.Equal(RabbitMqQueueRoles.PrimaryWorkQueue, primary.QueueRole);
                Assert.True(primary.Enabled);
                Assert.True(primary.ConsumerRequired);
                Assert.True(primary.BlocksRuntimeHealth);
            },
            auxiliary =>
            {
                Assert.Equal(NatureProtectorRabbitMqTopology.ObservabilityRawQueue, auxiliary.QueueName);
                Assert.Equal(RabbitMqQueueRoles.AuxiliaryDiagnosticQueue, auxiliary.QueueRole);
                Assert.False(auxiliary.Enabled);
                Assert.False(auxiliary.ConsumerRequired);
                Assert.False(auxiliary.BlocksRuntimeHealth);
            });
    }

    [Fact]
    public void GetBindings_UsesConfiguredPrimaryQueueName_WhenRawIsDisabled()
    {
        var options = new RabbitMqOptions
        {
            IngestionReadingsQueueName = "np.it.ingestion",
            ObservabilityRawQueueName = "np.it.raw",
            ObservabilityRawEnabled = false
        };

        Assert.Collection(
            options.GetQueueNames(),
            queueName => Assert.Equal("np.it.ingestion", queueName));
        Assert.Collection(
            options.GetBindings(),
            binding =>
            {
                Assert.Equal("np.it.ingestion", binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            });
    }

    [Fact]
    public void GetBindings_IncludesConfiguredRawQueue_WhenExplicitlyEnabled()
    {
        var options = new RabbitMqOptions
        {
            IngestionReadingsQueueName = "np.it.ingestion",
            ObservabilityRawEnabled = true,
            ObservabilityRawQueueName = "np.it.raw"
        };

        Assert.Collection(
            options.GetQueueNames(),
            queueName => Assert.Equal("np.it.ingestion", queueName),
            queueName => Assert.Equal("np.it.raw", queueName));
        Assert.Collection(
            options.GetEnabledQueueDefinitions(),
            primary => Assert.Equal(RabbitMqQueueRoles.PrimaryWorkQueue, primary.QueueRole),
            auxiliary => Assert.Equal(RabbitMqQueueRoles.AuxiliaryDiagnosticQueue, auxiliary.QueueRole));
        Assert.Collection(
            options.GetBindings(),
            binding =>
            {
                Assert.Equal("np.it.ingestion", binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            },
            binding =>
            {
                Assert.Equal("np.it.raw", binding.QueueName);
                Assert.Equal(RoutingKeys.SensorReadingProduced, binding.RoutingKey);
            });
    }

    [Fact]
    public void Management_values_use_explicit_overrides_when_configured()
    {
        var options = new RabbitMqOptions
        {
            HostName = "amqp.internal",
            UserName = "app-user",
            Password = "app-password",
            ManagementHost = "management.internal",
            ManagementUserName = "monitor-user",
            ManagementPassword = "monitor-password"
        };

        Assert.Equal("management.internal", options.GetEffectiveManagementHost());
        Assert.Equal("monitor-user", options.GetEffectiveManagementUserName());
        Assert.Equal("monitor-password", options.GetEffectiveManagementPassword());
    }
}
