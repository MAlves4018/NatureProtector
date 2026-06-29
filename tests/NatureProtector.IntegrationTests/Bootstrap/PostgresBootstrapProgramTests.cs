using NatureProtector.Postgres.Bootstrap;

namespace NatureProtector.IntegrationTests.Bootstrap;

[Collection("EnvironmentVariables")]
public class PostgresBootstrapProgramTests
{
    [Fact]
    public void ShouldSkipSchemaMigration_DefaultsToFalse()
    {
        WithSkipSchemaMigrationValue(null, () =>
        {
            Assert.False(BootstrapProgram.ShouldSkipSchemaMigration());
        });
    }

    [Fact]
    public void ShouldSkipSchemaMigration_ReturnsTrueWhenExplicitlyEnabled()
    {
        WithSkipSchemaMigrationValue("true", () =>
        {
            Assert.True(BootstrapProgram.ShouldSkipSchemaMigration());
        });
    }

    [Fact]
    public void ShouldSkipSchemaMigration_RejectsInvalidValue()
    {
        WithSkipSchemaMigrationValue("sometimes", () =>
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => BootstrapProgram.ShouldSkipSchemaMigration());

            Assert.Contains(
                BootstrapProgram.SkipSchemaMigrationEnvironmentVariable,
                exception.Message,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ResolveRepoRoot_FindsRepositoryFromNestedExecutionPath()
    {
        var repoRoot = FindRepositoryRoot();
        var nestedExecutionPath = Path.Combine(repoRoot, "src", "NatureProtector.Postgres.Bootstrap", "bin", "Release", "net9.0");

        var resolved = BootstrapProgram.ResolveRepoRoot(nestedExecutionPath);

        Assert.Equal(repoRoot, resolved);
    }

    [Fact]
    public void ResolveContentRoot_FindsPackageRootFromPublishedBootstrapPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"np-bootstrap-package-{Guid.NewGuid():N}");
        var bootstrapPath = Path.Combine(tempRoot, "publish", "postgres-bootstrap");

        try
        {
            Directory.CreateDirectory(bootstrapPath);
            CreateFile(Path.Combine(tempRoot, "data", "manifests", "datasets", "proenca-a-nova-dataset-plan.json"));
            CreateFile(Path.Combine(tempRoot, "data", "baseline", "areas", "proenca-a-nova", "area.geojson"));
            CreateFile(Path.Combine(tempRoot, "data", "manifests", "scenarios", "proenca-a-nova-scenarios.generated.json"));

            var resolved = BootstrapProgram.ResolveContentRoot(bootstrapPath);

            Assert.Equal(tempRoot, resolved);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveRepoRoot_FailsWhenRepositoryMarkerIsAbsent()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"np-bootstrap-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() => BootstrapProgram.ResolveRepoRoot(tempRoot));

            Assert.Contains("Could not resolve the repository or package content root", exception.Message, StringComparison.Ordinal);
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

    private static void CreateFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{}");
    }

    private static void WithSkipSchemaMigrationValue(string? value, Action action)
    {
        var previous = Environment.GetEnvironmentVariable(
            BootstrapProgram.SkipSchemaMigrationEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                BootstrapProgram.SkipSchemaMigrationEnvironmentVariable,
                value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                BootstrapProgram.SkipSchemaMigrationEnvironmentVariable,
                previous);
        }
    }
}
