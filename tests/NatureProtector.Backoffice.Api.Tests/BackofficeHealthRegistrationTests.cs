namespace NatureProtector.Backoffice.Api.Tests;

public sealed class BackofficeHealthRegistrationTests
{
    [Fact]
    public void Host_RegistersPostgresAsConditionalReadinessDependency()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(
            Path.Combine(root, "src", "NatureProtector.Backoffice.Api", "Program.cs"));

        Assert.Contains("if (backofficeOptions.ControlPlaneEnabled)", program);
        Assert.Contains("AddCheck<ControlPlaneDatabaseHealthCheck>", program);
        Assert.Contains("\"control-plane-postgres\"", program);
        Assert.Contains("tags: [\"ready\"]", program);
        Assert.Contains("timeout: TimeSpan.FromSeconds(5)", program);
    }

    [Fact]
    public void Host_SeparatesProcessLivenessFromDependencyReadiness()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(
            Path.Combine(root, "src", "NatureProtector.Backoffice.Api", "Program.cs"));

        Assert.Contains("MapHealthChecks(\"/health/live\"", program);
        Assert.Contains("Predicate = _ => false", program);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program);
        Assert.Contains(
            "Predicate = registration => registration.Tags.Contains(\"ready\")",
            program);
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
