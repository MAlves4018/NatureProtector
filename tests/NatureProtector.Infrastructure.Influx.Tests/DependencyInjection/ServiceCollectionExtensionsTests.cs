using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.DependencyInjection;
using NatureProtector.Infrastructure.Influx.Services;

namespace NatureProtector.Infrastructure.Influx.Tests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfluxPersistence_RegistersWriteService_AndBindsOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InfluxDbOptions.SectionName}:Url"] = "http://localhost:8086",
                [$"{InfluxDbOptions.SectionName}:Token"] = "token",
                [$"{InfluxDbOptions.SectionName}:Organization"] = "org",
                [$"{InfluxDbOptions.SectionName}:Bucket"] = "bucket"
            })
            .Build();

        var services = new ServiceCollection();

        services.AddInfluxPersistence(configuration, AppContext.BaseDirectory);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<InfluxDbOptions>>().Value;
        var descriptor = services.Single(service => service.ServiceType == typeof(IInfluxWriteService));

        Assert.Equal(typeof(InfluxWriteService), descriptor.ImplementationType);
        Assert.Equal("http://localhost:8086", options.Url);
        Assert.Equal("token", options.Token);
        Assert.Equal("org", options.Organization);
        Assert.Equal("bucket", options.Bucket);
    }

    [Fact]
    public void AddInfluxPersistence_LoadsMissingValuesFromDotEnv()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"np-influx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);

        try
        {
            File.WriteAllText(
                Path.Combine(basePath, ".env"),
                string.Join(
                    Environment.NewLine,
                    [
                        "INFLUXDB_TOKEN=token-from-dotenv",
                        "INFLUXDB_ORGANIZATION=org-from-dotenv",
                        "INFLUXDB_BUCKET=bucket-from-dotenv",
                        "INFLUXDB_PORT=9191"
                    ]));

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{InfluxDbOptions.SectionName}:Url"] = string.Empty,
                    [$"{InfluxDbOptions.SectionName}:Token"] = string.Empty,
                    [$"{InfluxDbOptions.SectionName}:Organization"] = string.Empty,
                    [$"{InfluxDbOptions.SectionName}:Bucket"] = string.Empty
                })
                .Build();

            var services = new ServiceCollection();

            services.AddInfluxPersistence(configuration, basePath);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<InfluxDbOptions>>().Value;

            Assert.Equal("http://localhost:9191", options.Url);
            Assert.Equal("token-from-dotenv", options.Token);
            Assert.Equal("org-from-dotenv", options.Organization);
            Assert.Equal("bucket-from-dotenv", options.Bucket);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }
}
