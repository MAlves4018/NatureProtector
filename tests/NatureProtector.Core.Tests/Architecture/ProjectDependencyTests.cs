using System.Xml.Linq;

namespace NatureProtector.Core.Tests.Architecture;

public class ProjectDependencyTests
{
    [Fact]
    public void CoreProject_DoesNotReferenceApplicationOrInfrastructureProjects()
    {
        var references = ReadProjectReferences("src", "NatureProtector.Core", "NatureProtector.Core.csproj");

        Assert.Empty(references);
    }

    [Fact]
    public void PreventionProject_ReferencesOnlyCoreAndSharedBoundaries()
    {
        var references = ReadProjectReferences("src", "NatureProtector.Prevention", "NatureProtector.Prevention.csproj");

        Assert.Equal(
            [
                @"..\NatureProtector.Core\NatureProtector.Core.csproj",
                @"..\NatureProtector.Shared\NatureProtector.Shared.csproj",
            ],
            references);
    }

    [Fact]
    public void SharedProject_DoesNotReferenceFeatureOrInfrastructureProjects()
    {
        var references = ReadProjectReferences("src", "NatureProtector.Shared", "NatureProtector.Shared.csproj");

        Assert.Empty(references);
    }

    [Fact]
    public void SharedProject_DoesNotReferencePersistencePackages()
    {
        var packages = ReadPackageReferences("src", "NatureProtector.Shared", "NatureProtector.Shared.csproj");

        Assert.DoesNotContain(packages, package => package.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(packages, package => package.Contains("Npgsql", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SharedProject_DoesNotReferenceOpenTelemetryPackages()
    {
        var packages = ReadPackageReferences("src", "NatureProtector.Shared", "NatureProtector.Shared.csproj");

        Assert.DoesNotContain(packages, package => package.StartsWith("OpenTelemetry", StringComparison.Ordinal));
    }

    [Fact]
    public void SharedContracts_DoNotDependOnPersistenceOrFeatureNamespaces()
    {
        var offenders = FindCSharpFiles("src", "NatureProtector.Shared", "Contracts")
            .Concat(FindCSharpFiles("src", "NatureProtector.Shared", "Messaging"))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path),
            })
            .Where(file =>
                file.Source.Contains("NatureProtector.Infrastructure.", StringComparison.Ordinal) ||
                file.Source.Contains("NatureProtector.Prevention.", StringComparison.Ordinal) ||
                file.Source.Contains("NatureProtector.Simulator.", StringComparison.Ordinal) ||
                file.Source.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                file.Source.Contains("DbContext", StringComparison.Ordinal))
            .Select(file => RelativeToRepositoryRoot(file.Path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ApiContracts_DoNotExposePersistenceTypes()
    {
        var offenders = FindCSharpFiles("src", "NatureProtector.Backoffice.Api")
            .Where(path => path.Split(Path.DirectorySeparatorChar).Contains("Contracts"))
            .Select(path => new
            {
                Path = path,
                Source = File.ReadAllText(path),
            })
            .Where(file =>
                file.Source.Contains("NatureProtector.Infrastructure.Postgres", StringComparison.Ordinal) ||
                file.Source.Contains("NatureProtectorControlDbContext", StringComparison.Ordinal) ||
                file.Source.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            .Select(file => RelativeToRepositoryRoot(file.Path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void SourceProjects_DoNotContainProjectReferenceCycles()
    {
        var projectGraph = ReadSourceProjectReferenceGraph();
        var cycles = FindProjectReferenceCycles(projectGraph)
            .Select(cycle => string.Join(" -> ", cycle.Select(RelativeToRepositoryRoot)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(cycles);
    }

    private static string[] ReadProjectReferences(params string[] projectPathParts)
    {
        var projectPath = Path.Combine([FindRepositoryRoot(), .. projectPathParts]);
        var project = XDocument.Load(projectPath);

        return project
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Order(StringComparer.Ordinal)
            .ToArray()!;
    }

    private static string[] ReadPackageReferences(params string[] projectPathParts)
    {
        var projectPath = Path.Combine([FindRepositoryRoot(), .. projectPathParts]);
        var project = XDocument.Load(projectPath);

        return project
            .Descendants("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Order(StringComparer.Ordinal)
            .ToArray()!;
    }

    private static IReadOnlyDictionary<string, string[]> ReadSourceProjectReferenceGraph()
    {
        var sourceRoot = Path.Combine(FindRepositoryRoot(), "src");
        var projectPaths = Directory
            .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path =>
                !path.Split(Path.DirectorySeparatorChar).Contains("bin") &&
                !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var sourceProjects = projectPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return projectPaths.ToDictionary(
            projectPath => projectPath,
            projectPath =>
            {
                var project = XDocument.Load(projectPath);
                var projectDirectory = Path.GetDirectoryName(projectPath)!;

                return project
                    .Descendants("ProjectReference")
                    .Select(reference => reference.Attribute("Include")?.Value)
                    .Where(include => !string.IsNullOrWhiteSpace(include))
                    .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
                    .Where(sourceProjects.Contains)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string[]> FindProjectReferenceCycles(IReadOnlyDictionary<string, string[]> projectGraph)
    {
        var cycles = new List<string[]>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();

        foreach (var projectPath in projectGraph.Keys.Order(StringComparer.Ordinal))
        {
            Visit(projectPath);
        }

        return cycles;

        void Visit(string projectPath)
        {
            if (visited.Contains(projectPath))
            {
                return;
            }

            if (!visiting.Add(projectPath))
            {
                var cycle = stack
                    .Reverse()
                    .SkipWhile(path => !StringComparer.OrdinalIgnoreCase.Equals(path, projectPath))
                    .Append(projectPath)
                    .ToArray();
                cycles.Add(cycle);
                return;
            }

            stack.Push(projectPath);

            foreach (var referencePath in projectGraph[projectPath])
            {
                Visit(referencePath);
            }

            _ = stack.Pop();
            _ = visiting.Remove(projectPath);
            _ = visited.Add(projectPath);
        }
    }

    private static string[] FindCSharpFiles(params string[] pathParts)
    {
        var root = Path.Combine([FindRepositoryRoot(), .. pathParts]);

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Split(Path.DirectorySeparatorChar).Contains("bin") &&
                !path.Split(Path.DirectorySeparatorChar).Contains("obj"))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string RelativeToRepositoryRoot(string path)
    {
        return Path.GetRelativePath(FindRepositoryRoot(), path).Replace(Path.DirectorySeparatorChar, '/');
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
