using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;

namespace NatureProtector.Infrastructure.Influx.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfluxPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<InfluxDbOptions>(
            configuration.GetSection(InfluxDbOptions.SectionName));

        services.AddSingleton<IInfluxWriteService, InfluxWriteService>();

        return services;
    }
}