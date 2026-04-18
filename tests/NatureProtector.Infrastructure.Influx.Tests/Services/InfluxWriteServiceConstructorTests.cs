using Microsoft.Extensions.Options;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace NatureProtector.Infrastructure.Influx.Tests.Services;

public sealed class InfluxWriteServiceConstructorTests
{
    [Fact]
    public void Ctor_Throws_WhenOptionsArgumentIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new InfluxWriteService(null!,NullLogger<InfluxWriteService>.Instance));

        Assert.Equal("options", ex.ParamName);
    }

    [Theory]
    [InlineData("", "token", "org", "bucket", "InfluxDb:Url is required.")]
    [InlineData("http://localhost:8086", "", "org", "bucket", "InfluxDb:Token is required.")]
    [InlineData("http://localhost:8086", "token", "", "bucket", "InfluxDb:Organization is required.")]
    [InlineData("http://localhost:8086", "token", "org", "", "InfluxDb:Bucket is required.")]
    public void Ctor_Throws_WhenRequiredOptionIsMissing(
        string url,
        string token,
        string organization,
        string bucket,
        string expectedMessage)
    {
        var options = Options.Create(new InfluxDbOptions
        {
            Url = url,
            Token = token,
            Organization = organization,
            Bucket = bucket
        });

        var ex = Assert.Throws<InvalidOperationException>(() => new InfluxWriteService(options,NullLogger<InfluxWriteService>.Instance));

        Assert.Equal(expectedMessage, ex.Message);
    }
}
