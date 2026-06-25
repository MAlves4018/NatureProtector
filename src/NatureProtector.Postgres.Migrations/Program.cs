using System.Text.Json;
using NatureProtector.Postgres.Migrations;

var settings = MigrationSettings.LoadFromEnvironment();
var runner = new PostgresMigrationRunner(settings);
var summary = await runner.RunAsync();

Console.WriteLine(JsonSerializer.Serialize(new
{
    operation = "postgres_migrations",
    database = settings.Database,
    host = settings.Host,
    application_role = summary.ApplicationRole,
    applied_during_run = summary.AppliedDuringRun,
    applied_migrations = summary.AppliedMigrations,
    pending_after_run = 0,
    status = "completed"
}, new JsonSerializerOptions { WriteIndented = true }));
