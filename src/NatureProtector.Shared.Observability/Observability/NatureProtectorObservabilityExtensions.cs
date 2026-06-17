using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace NatureProtector.Shared.Observability;

public static class NatureProtectorObservabilityExtensions
{
    public static IServiceCollection AddNatureProtectorOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string serviceName,
        string[] activitySourceNames,
        string[] meterNames,
        bool enableAspNetCoreInstrumentation = false)
    {
        var consoleExporterEnabled = IsConsoleExporterEnabled(configuration, environment);
        var otlpEndpoint = ResolveOtlpEndpoint(configuration);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(activitySourceNames)
                    .AddHttpClientInstrumentation();

                if (enableAspNetCoreInstrumentation)
                {
                    tracing.AddAspNetCoreInstrumentation();
                }

                if (consoleExporterEnabled)
                {
                    tracing.AddConsoleExporter();
                }

                if (otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(meterNames)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation();

                if (enableAspNetCoreInstrumentation)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                if (consoleExporterEnabled)
                {
                    metrics.AddConsoleExporter();
                }

                if (otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = otlpEndpoint);
                }
            });

        return services;
    }

    public static ILoggingBuilder AddNatureProtectorActivityTracking(this ILoggingBuilder logging)
    {
        logging.Services.Configure<LoggerFactoryOptions>(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.ParentId |
                ActivityTrackingOptions.Tags |
                ActivityTrackingOptions.Baggage;
        });

        return logging;
    }

    private static bool IsConsoleExporterEnabled(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration["Observability:ConsoleExporterEnabled"];

        if (bool.TryParse(configured, out var parsed))
        {
            return parsed;
        }

        return environment.IsDevelopment();
    }

    private static Uri? ResolveOtlpEndpoint(IConfiguration configuration)
    {
        var raw = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
            ?? configuration["Observability:OtlpEndpoint"];

        return Uri.TryCreate(raw, UriKind.Absolute, out var endpoint)
            ? endpoint
            : null;
    }
}
