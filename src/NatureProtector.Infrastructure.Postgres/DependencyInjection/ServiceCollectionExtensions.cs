using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NatureProtector.Infrastructure.Postgres.Configuration;
using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Infrastructure.Postgres.DependencyInjection;

/*
 * Estas extensões centralizam o registo do acesso PostgreSQL usado pelo control
 * plane e pelas projeções.
 *
 * Rationale:
 * - Os vários hosts partilham a mesma forma de resolver ligação e DbContext.
 * - A configuração do provider não deve ficar duplicada por aplicação.
 */

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Regista o <see cref="NatureProtectorControlDbContext" /> com base na
    /// configuração ambiente/.env do repositório.
    /// </summary>
    public static IServiceCollection AddNatureProtectorControlPlanePostgres(
        this IServiceCollection services,
        string basePath)
    {
        var settings = PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(basePath);
        var connectionString = settings.BuildConnectionString();

        services.AddDbContextFactory<NatureProtectorControlDbContext>(
            options => options.UseNpgsql(connectionString));

        return services;
    }
}
