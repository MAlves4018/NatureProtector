using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class PreventionOptionsTests
{
    [Fact]
    public void Defaults_AreAlignedWithCurrentResidualConfiguration()
    {
        var options = new PreventionOptions();

        Assert.Equal("Prevention", PreventionOptions.SectionName);
        Assert.Equal(NatureProtectorRabbitMqTopology.IngestionReadingsQueue, options.QueueName);
        Assert.Equal((ushort)10, options.PrefetchCount);
        Assert.True(options.RequeueOnUnexpectedFailure);
        Assert.Equal("data/accepted-readings.ndjson", options.AcceptedReadingsPath);
    }
}
