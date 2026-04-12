using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NatureProtector.Infrastructure.Postgres.Configuration;

namespace NatureProtector.Infrastructure.Postgres.Persistence;

public sealed class NatureProtectorControlDbContextFactory
    : IDesignTimeDbContextFactory<NatureProtectorControlDbContext>
{
    public NatureProtectorControlDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var settings = PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(basePath);

        var optionsBuilder = new DbContextOptionsBuilder<NatureProtectorControlDbContext>();
        optionsBuilder.UseNpgsql(settings.BuildConnectionString());

        return new NatureProtectorControlDbContext(optionsBuilder.Options);
    }
}
