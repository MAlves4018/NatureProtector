using NatureProtector.Postgres.Bootstrap;

namespace NatureProtector.IntegrationTests.Bootstrap;

public class PostgresBootstrapProgramTests
{
    [Fact]
    public void ResolveRepoRoot_FindsRepositoryFromNestedExecutionPath()
    {
        var repoRoot = FindRepositoryRoot();
        var nestedExecutionPath = Path.Combine(repoRoot, "src", "NatureProtector.Postgres.Bootstrap", "bin", "Release", "net9.0");

        var resolved = BootstrapProgram.ResolveRepoRoot(nestedExecutionPath);

        Assert.Equal(repoRoot, resolved);
    }

    [Fact]
    public void ResolveRepoRoot_FailsWhenRepositoryMarkerIsAbsent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"np-bootstrap-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => BootstrapProgram.ResolveRepoRoot(tempRoot));

            Assert.Contains("Could not resolve the repository root", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NatureProtector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate NatureProtector.sln from the test output directory.");
    }
}
