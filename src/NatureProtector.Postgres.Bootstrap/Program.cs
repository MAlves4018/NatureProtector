using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Bootstrap;
using NatureProtector.Infrastructure.Postgres.Configuration;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Shared.Observability;

/*
 * Este ponto de entrada executa o bootstrap da baseline PostgreSQL do projeto.
 *
 * Rationale:
 * - A operação de bootstrap é usada em linha de comandos e precisa de ter um
 *   fluxo claro e autónomo.
 * - O programa limita-se a resolver a ligação, construir o DbContext e delegar
 *   a importação para o bootstrapper dedicado.
 */

using var bootstrapActivity = PostgresBootstrapTelemetry.ActivitySource.StartActivity("natureprotector.bootstrap.program");

var repoRoot = ResolveRepoRoot(AppContext.BaseDirectory);
var settings = PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(repoRoot);

var optionsBuilder = new DbContextOptionsBuilder<NatureProtectorControlDbContext>();
optionsBuilder.UseNpgsql(settings.BuildConnectionString());

await using var dbContext = new NatureProtectorControlDbContext(optionsBuilder.Options);
var bootstrapper = new ControlPlaneBootstrapper(dbContext, repoRoot);
var summary = await bootstrapper.BootstrapPilotAreaAsync();

Console.WriteLine("NatureProtector.Postgres.Bootstrap");
Console.WriteLine($"Database: {settings.Database} @ {settings.Host}:{settings.Port}");
Console.WriteLine($"Configuration version: v{summary.ConfigurationVersionNumber}");
Console.WriteLine($"Area imported: {summary.AreaCode} ({summary.AreaName})");
Console.WriteLine($"Grid cells imported: {summary.GridCellCount}");
Console.WriteLine($"Sensor profiles imported: {summary.SensorProfileCount}");
Console.WriteLine($"Sensor nodes imported: {summary.SensorNodeCount}");
Console.WriteLine($"Scenarios imported: {summary.ScenarioCount}");
Console.WriteLine($"Dataset artifacts indexed: {summary.DatasetArtifactCount}");
Console.WriteLine($"Scenario bindings created: {summary.ScenarioDatasetBindingCount}");

/// <summary>
/// Resolve a raiz do repositório a partir da pasta de execução da aplicação.
/// </summary>
static string ResolveRepoRoot(string startPath)
{
    var current = new DirectoryInfo(Path.GetFullPath(startPath));

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new InvalidOperationException("Could not resolve the repository root from the bootstrap application path.");
}
