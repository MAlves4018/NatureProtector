using NatureProtector.Infrastructure.Influx.Configuration;

namespace NatureProtector.Infrastructure.Influx.Tests.Configuration;

public sealed class InfluxDbOptionsTests
{
    [Fact]
    public void Defaults_AreEmptyStrings_AndSectionNameIsStable()
    {
        var options = new InfluxDbOptions();

        Assert.Equal("InfluxDb", InfluxDbOptions.SectionName);
        Assert.Equal(string.Empty, options.Url);
        Assert.Equal(string.Empty, options.Token);
        Assert.Equal(string.Empty, options.Organization);
        Assert.Equal(string.Empty, options.Bucket);
    }
}
