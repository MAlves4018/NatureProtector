using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Bootstrap;
using NatureProtector.Infrastructure.Postgres.Configuration;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Postgres.Bootstrap;
using NatureProtector.Shared.Observability;

/*
 * Este ponto de entrada executa o bootstrap da baseline PostgreSQL do projeto.
 *
 * Rationale:
 * - A operacao de bootstrap e usada em linha de comandos e precisa de ter um
 *   fluxo claro e autonomo.
 * - O programa limita-se a resolver a ligacao, construir o DbContext e delegar
 *   a importacao para o bootstrapper dedicado.
 */

using var bootstrapActivity = PostgresBootstrapTelemetry.ActivitySource.StartActivity("natureprotector.bootstrap.program");

var contentRoot = BootstrapProgram.ResolveContentRoot(AppContext.BaseDirectory);
var skipSchemaMigration = BootstrapProgram.ShouldSkipSchemaMigration();
var settings = PostgresConnectionSettingsLoader.LoadFromEnvironmentOrDotEnv(contentRoot);
await using var dataSource = settings.BuildDataSource();

var optionsBuilder = new DbContextOptionsBuilder<NatureProtectorControlDbContext>();
optionsBuilder.UseNpgsql(dataSource);

await using var dbContext = new NatureProtectorControlDbContext(optionsBuilder.Options);
var bootstrapper = new ControlPlaneBootstrapper(dbContext, contentRoot, skipSchemaMigration);
var summary = await bootstrapper.BootstrapPilotAreaAsync();

Console.WriteLine("NatureProtector.Postgres.Bootstrap");
Console.WriteLine($"Database: {settings.Database} @ {settings.Host}:{settings.Port}");
Console.WriteLine($"Schema migration skipped: {skipSchemaMigration}");
Console.WriteLine($"Configuration version: v{summary.ConfigurationVersionNumber}");
Console.WriteLine($"Area imported: {summary.AreaCode} ({summary.AreaName})");
Console.WriteLine($"Grid cells imported: {summary.GridCellCount}");
Console.WriteLine($"Sensor profiles imported: {summary.SensorProfileCount}");
Console.WriteLine($"Sensor nodes imported: {summary.SensorNodeCount}");
Console.WriteLine($"Scenarios imported: {summary.ScenarioCount}");
Console.WriteLine($"Dataset artifacts indexed: {summary.DatasetArtifactCount}");
Console.WriteLine($"Scenario bindings created: {summary.ScenarioDatasetBindingCount}");
