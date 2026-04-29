using NatureProtector.Infrastructure.Influx.Configuration;

namespace NatureProtector.Infrastructure.Influx.Tests.Configuration;

public sealed class InfluxDbOptionsTests
{
    [Fact]
    public void Defaults_AreConfiguredForEnabledNonCriticalWrites_AndSectionNameIsStable()
    {
        var options = new InfluxDbOptions();

        Assert.Equal("InfluxDb", InfluxDbOptions.SectionName);
        Assert.True(options.Enabled);
        Assert.False(options.FailPipelineOnWriteError);
        Assert.Equal(string.Empty, options.Url);
        Assert.Equal(string.Empty, options.Token);
        Assert.Equal(string.Empty, options.Organization);
        Assert.Equal(string.Empty, options.Bucket);
        Assert.True(options.Writes.AcceptedReadings);
        Assert.True(options.Writes.RiskAssessments);
        Assert.True(options.Writes.AreaRiskSnapshots);
    }
}
