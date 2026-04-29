using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;

namespace NatureProtector.Infrastructure.Influx.DependencyInjection;

/*
 * Estas extensões centralizam o registo da escrita em InfluxDB.
 *
 * Rationale:
 * - A pipeline de prevenção precisa de publicar métricas de observabilidade sem
 *   duplicar configuração em cada host.
 * - O fallback para `.env` deve ficar encapsulado numa única composição.
 */

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Regista a configuração e o serviço de escrita em InfluxDB.
    /// </summary>
    public static IServiceCollection AddInfluxPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string basePath)
    {
        services.AddOptions<InfluxDbOptions>()
            .Bind(configuration.GetSection(InfluxDbOptions.SectionName));
        services.PostConfigure<InfluxDbOptions>(
            options => InfluxDbSettingsLoader.ApplyEnvironmentOrDotEnvFallbacks(options, basePath));

        services.AddSingleton<InfluxWriteService>();
        services.AddSingleton<Func<IInfluxWriteService>>(serviceProvider =>
            () => serviceProvider.GetRequiredService<InfluxWriteService>());
        services.AddSingleton<SafeInfluxWriteService>();
        services.AddSingleton<NoOpInfluxWriteService>();
        services.AddSingleton<IInfluxWriteService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<InfluxDbOptions>>().Value;

            return options.Enabled
                ? serviceProvider.GetRequiredService<SafeInfluxWriteService>()
                : serviceProvider.GetRequiredService<NoOpInfluxWriteService>();
        });

        return services;
    }
}
