namespace NatureProtector.Postgres.Bootstrap;

public static class BootstrapProgram
{
    public static string ResolveRepoRoot(string startPath)
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
}
