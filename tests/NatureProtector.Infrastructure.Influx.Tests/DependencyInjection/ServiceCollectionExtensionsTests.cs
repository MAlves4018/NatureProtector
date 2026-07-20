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
    public void AddInfluxPersistence_WhenEnabled_RegistersSafeWriteService_AndBindsOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InfluxDbOptions.SectionName}:Enabled"] = "true",
                [$"{InfluxDbOptions.SectionName}:FailPipelineOnWriteError"] = "false",
                [$"{InfluxDbOptions.SectionName}:Url"] = "http://localhost:8086",
                [$"{InfluxDbOptions.SectionName}:Token"] = "token",
                [$"{InfluxDbOptions.SectionName}:Organization"] = "org",
                [$"{InfluxDbOptions.SectionName}:Bucket"] = "bucket",
                [$"{InfluxDbOptions.SectionName}:Writes:AcceptedReadings"] = "true",
                [$"{InfluxDbOptions.SectionName}:Writes:RiskAssessments"] = "true",
                [$"{InfluxDbOptions.SectionName}:Writes:AreaRiskSnapshots"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfluxPersistence(configuration, AppContext.BaseDirectory);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<InfluxDbOptions>>().Value;
        var writeService = provider.GetRequiredService<IInfluxWriteService>();

        Assert.IsType<SafeInfluxWriteService>(writeService);
        Assert.True(options.Enabled);
        Assert.False(options.FailPipelineOnWriteError);
        Assert.Equal("http://localhost:8086", options.Url);
        Assert.Equal("token", options.Token);
        Assert.Equal("org", options.Organization);
        Assert.Equal("bucket", options.Bucket);
        Assert.True(options.Writes.AcceptedReadings);
        Assert.True(options.Writes.RiskAssessments);
        Assert.False(options.Writes.AreaRiskSnapshots);
    }

    [Fact]
    public void AddInfluxPersistence_WhenDisabled_RegistersNoOpWriteService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{InfluxDbOptions.SectionName}:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddInfluxPersistence(configuration, AppContext.BaseDirectory);

        using var provider = services.BuildServiceProvider();
        var writeService = provider.GetRequiredService<IInfluxWriteService>();

        Assert.IsType<NoOpInfluxWriteService>(writeService);
    }

    [Fact]
    public void AddInfluxPersistence_LoadsMissingValuesFromDotEnv()
    {
        var basePath = Path.Combine(Path.GetTempPath(), $"np-influx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(basePath);

        var environmentNames = new[]
        {
            "INFLUXDB_URL",
            "INFLUXDB_PORT",
            "INFLUXDB_TOKEN",
            "INFLUXDB_ORGANIZATION",
            "INFLUXDB_BUCKET"
        };
        var originalEnvironment = environmentNames.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable);

        try
        {
            foreach (var name in environmentNames)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

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
                    [$"{InfluxDbOptions.SectionName}:Enabled"] = "true",
                    [$"{InfluxDbOptions.SectionName}:Url"] = string.Empty,
                    [$"{InfluxDbOptions.SectionName}:Token"] = string.Empty,
                    [$"{InfluxDbOptions.SectionName}:Organization"] = string.Empty,
                    [$"{InfluxDbOptions.SectionName}:Bucket"] = string.Empty
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();

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
            foreach (var pair in originalEnvironment)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }

            Directory.Delete(basePath, recursive: true);
        }
    }
}
