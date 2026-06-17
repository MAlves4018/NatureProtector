using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Shared.Tests.Observability;

public sealed class HostTelemetryTests
{
    [Fact]
    public void TelemetrySources_ExposeStableServiceNamesAndMetricNames()
    {
        Assert.Equal("NatureProtector.Postgres.Bootstrap", PostgresBootstrapTelemetry.ServiceName);
        Assert.Equal(PostgresBootstrapTelemetry.ServiceName, PostgresBootstrapTelemetry.ActivitySource.Name);
        Assert.Equal(PostgresBootstrapTelemetry.ServiceName, PostgresBootstrapTelemetry.Meter.Name);
        Assert.Equal("NatureProtector.Simulator.Host", SimulatorHostTelemetry.ServiceName);
        Assert.Equal("NatureProtector.Prevention.Host", PreventionHostTelemetry.ServiceName);
        Assert.Equal("NatureProtector.Backoffice.Api", BackofficeApiTelemetry.ServiceName);

        PostgresBootstrapTelemetry.BootstrapRuns.Add(1);
        SimulatorHostTelemetry.PublishedMessages.Add(1);
        PreventionHostTelemetry.QuarantinedEvents.Add(1);
        BackofficeApiTelemetry.Requests.Add(1);
    }

    [Fact]
    public void ActivityTrackingExtension_ConfiguresCorrelationFields()
    {
        var services = new ServiceCollection();
        var logging = new LoggingBuilder(services);

        logging.AddNatureProtectorActivityTracking();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<LoggerFactoryOptions>>().Value;

        Assert.True(options.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.TraceId));
        Assert.True(options.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.SpanId));
        Assert.True(options.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.ParentId));
        Assert.True(options.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.Tags));
        Assert.True(options.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.Baggage));
    }

    [Fact]
    public void OpenTelemetryExtension_RegistersWithoutExternalInfrastructure()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:ConsoleExporterEnabled"] = "false",
                ["Observability:OtlpEndpoint"] = "not-a-valid-uri"
            })
            .Build();

        var result = services.AddNatureProtectorOpenTelemetry(
            configuration,
            new FakeHostEnvironment("Production"),
            "test-service",
            ["test-activity-source"],
            ["test-meter"],
            enableAspNetCoreInstrumentation: false);

        Assert.Same(services, result);
        Assert.NotEmpty(services);
    }

    [Fact]
    public async Task OpenTelemetryExtension_StartsWithOtlpExporterConfiguration()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Observability:ConsoleExporterEnabled"] = "false",
                ["Observability:OtlpEndpoint"] = "http://127.0.0.1:4318"
            })
            .Build();

        services.AddNatureProtectorOpenTelemetry(
            configuration,
            new FakeHostEnvironment("Production"),
            "test-service",
            ["test-activity-source"],
            ["test-meter"],
            enableAspNetCoreInstrumentation: false);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var hostedServices = provider.GetServices<IHostedService>().ToArray();

        Assert.NotEmpty(hostedServices);

        foreach (var hostedService in hostedServices)
        {
            await hostedService.StartAsync(CancellationToken.None);
        }

        foreach (var hostedService in hostedServices.Reverse())
        {
            await hostedService.StopAsync(CancellationToken.None);
        }
    }

    private sealed class LoggingBuilder(IServiceCollection services) : ILoggingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "NatureProtector.Shared.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
