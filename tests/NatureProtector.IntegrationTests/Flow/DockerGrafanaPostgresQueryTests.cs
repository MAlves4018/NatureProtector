using System.Text.Json;
using NatureProtector.IntegrationTests.TestInfrastructure;
using Npgsql;

namespace NatureProtector.IntegrationTests.Flow;

[Collection(DockerIntegrationCollection.Name)]
public sealed class DockerGrafanaPostgresQueryTests
{
    [Fact]
    [Trait("Category", "DockerIntegration")]
    public async Task ProvisionedPostgresPanels_ExecuteAgainstMigratedDatabase()
    {
        await using var database = await TemporaryPostgresDatabase.CreateAsync();
        var repositoryRoot = FindRepositoryRoot();
        var dashboards = Directory.EnumerateFiles(
            Path.Combine(repositoryRoot, "infra", "grafana", "dashboards"),
            "natureprotector-*.json",
            SearchOption.TopDirectoryOnly);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var executed = 0;
        foreach (var dashboard in dashboards)
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(dashboard));
            foreach (var query in EnumeratePostgresQueries(document.RootElement))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = query;
                command.CommandTimeout = 15;
                await using var reader = await command.ExecuteReaderAsync();
                executed++;
            }
        }

        Assert.True(executed > 0, "No PostgreSQL Grafana queries were discovered.");
    }

    private static IEnumerable<string> EnumeratePostgresQueries(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("datasource", out var datasource) &&
                datasource.ValueKind == JsonValueKind.Object &&
                datasource.TryGetProperty("uid", out var uid) &&
                uid.GetString() == "natureprotector-postgres" &&
                element.TryGetProperty("rawSql", out var rawSql) &&
                !string.IsNullOrWhiteSpace(rawSql.GetString()))
            {
                yield return rawSql.GetString()!;
            }
            foreach (var property in element.EnumerateObject())
                foreach (var query in EnumeratePostgresQueries(property.Value))
                    yield return query;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var query in EnumeratePostgresQueries(item))
                    yield return query;
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate NatureProtector.sln.");
    }
}
