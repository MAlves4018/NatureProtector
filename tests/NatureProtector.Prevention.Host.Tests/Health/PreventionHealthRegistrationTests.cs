namespace NatureProtector.Prevention.Host.Tests.Health;

public sealed class PreventionHealthRegistrationTests
{
    [Fact]
    public void Host_RegistersRabbitMqAndConditionalPostgresReadinessChecks()
    {
        var program = ReadProgram();

        Assert.Contains("var healthChecks = builder.Services.AddHealthChecks()", program);
        Assert.Contains("AddCheck<PreventionReadinessHealthCheck>", program);
        Assert.Contains("\"prevention-ready\"", program);
        Assert.Contains("tags: [\"ready\"]", program);
        var persistenceBranchStart = program.IndexOf(
            "if (preventionHostOptions.PipelinePersistenceEnabled)",
            StringComparison.Ordinal);
        Assert.True(persistenceBranchStart >= 0);

        var inMemoryBranchStart = program.IndexOf(
            "else",
            persistenceBranchStart,
            StringComparison.Ordinal);
        Assert.True(inMemoryBranchStart > persistenceBranchStart);

        var persistenceBranch = program[persistenceBranchStart..inMemoryBranchStart];
        Assert.Contains("healthChecks.AddCheck<PreventionDatabaseHealthCheck>", persistenceBranch);
        Assert.Contains("\"prevention-postgres\"", persistenceBranch);
        Assert.Contains("timeout: TimeSpan.FromSeconds(5)", persistenceBranch);
    }

    [Fact]
    public void Host_SeparatesProcessLivenessFromDependencyReadiness()
    {
        var program = ReadProgram();

        Assert.Contains("MapHealthChecks(\"/health/live\"", program);
        Assert.Contains("Predicate = _ => false", program);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program);
        Assert.Contains("registration.Tags.Contains(\"ready\")", program);
    }

    private static string ReadProgram()
    {
        var root = FindRepositoryRoot();
        return File.ReadAllText(
            Path.Combine(root, "src", "NatureProtector.Prevention.Host", "Program.cs"));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
