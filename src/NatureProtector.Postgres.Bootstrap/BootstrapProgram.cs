namespace NatureProtector.Postgres.Bootstrap;

public static class BootstrapProgram
{
    private static readonly string[] RequiredBootstrapInputs =
    [
        "data/manifests/datasets/proenca-a-nova-dataset-plan.json",
        "data/baseline/areas/proenca-a-nova/area.geojson",
        "data/manifests/scenarios/proenca-a-nova-scenarios.generated.json"
    ];

    public static string ResolveRepoRoot(string startPath)
    {
        return ResolveContentRoot(startPath);
    }

    public static string ResolveContentRoot(string startPath)
    {
        var current = new DirectoryInfo(Path.GetFullPath(startPath));

        while (current is not null)
        {
            if (IsRepositoryRoot(current.FullName) || IsPackagedContentRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not resolve the repository or package content root from the bootstrap application path.");
    }

    private static bool IsRepositoryRoot(string directory)
    {
        return File.Exists(Path.Combine(directory, "NatureProtector.sln"));
    }

    private static bool IsPackagedContentRoot(string directory)
    {
        return RequiredBootstrapInputs.All(relativePath =>
            File.Exists(Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar))));
    }
}
