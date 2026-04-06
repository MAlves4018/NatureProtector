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
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeName, options.ExchangeName);
    }
}
