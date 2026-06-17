using System.Text;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeEvidenceCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"np-evidence-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetContentAsync_ReturnsAllowlistedEvidenceWithoutExposingPath()
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "summary.md"), "# evidence", new UTF8Encoding(false));
        var catalog = new RuntimeEvidenceCatalog(_root);

        var listed = catalog.List(DateTimeOffset.UtcNow);
        var item = Assert.Single(listed.Items);
        var content = await catalog.GetContentAsync(item.EvidenceId, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotNull(content);
        Assert.Equal("summary", content.Metadata.Title);
        Assert.Equal("docs/evidence", content.Metadata.Scope);
        Assert.Equal("text/markdown; charset=utf-8", content.ContentType);
        Assert.Equal("# evidence", Encoding.UTF8.GetString(content.Content));
    }

    [Fact]
    public async Task GetContentAsync_RejectsTraversalAndNonAllowlistedExtension()
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "safe.md"), "safe", new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "secret.env"), "secret", new UTF8Encoding(false));
        var catalog = new RuntimeEvidenceCatalog(_root);

        var listed = catalog.List(DateTimeOffset.UtcNow);
        var traversal = await catalog.GetContentAsync("../safe.md", DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Single(listed.Items);
        Assert.Null(traversal);
    }

    [Theory]
    [InlineData("../safe.md")]
    [InlineData(@"..\safe.md")]
    [InlineData("%2E%2E%2Fsafe.md")]
    [InlineData("%252E%252E%252Fsafe.md")]
    [InlineData("/tmp/safe.md")]
    [InlineData(@"C:\tmp\safe.md")]
    [InlineData("safe\0id")]
    [InlineData("CON")]
    public async Task GetContentAsync_RejectsInvalidEvidenceIds(string evidenceId)
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "safe.md"), "safe", new UTF8Encoding(false));
        var catalog = new RuntimeEvidenceCatalog(_root);

        var content = await catalog.GetContentAsync(evidenceId, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Null(content);
    }

    [Fact]
    public async Task List_DoesNotFollowDirectoryReparsePointsOutsideEvidenceRoot()
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence");
        var outsideRoot = Path.Combine(_root, "outside-evidence");
        Directory.CreateDirectory(evidenceRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(Path.Combine(outsideRoot, "secret.md"), "secret", new UTF8Encoding(false));
        var linkPath = Path.Combine(evidenceRoot, "linked-outside");
        Assert.True(
            TryCreateDirectoryReparsePoint(linkPath, outsideRoot),
            "The local filesystem must support a directory reparse point or symlink for this containment test.");
        var catalog = new RuntimeEvidenceCatalog(_root);

        var listed = catalog.List(DateTimeOffset.UtcNow);

        Assert.Empty(listed.Items);
    }

    private static bool TryCreateDirectoryReparsePoint(string linkPath, string targetPath)
    {
        if (OperatingSystem.IsWindows())
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.ArgumentList.Add("/c");
            process.StartInfo.ArgumentList.Add("mklink");
            process.StartInfo.ArgumentList.Add("/J");
            process.StartInfo.ArgumentList.Add(linkPath);
            process.StartInfo.ArgumentList.Add(targetPath);
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }

        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            DeleteReparsePoints(_root);
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void DeleteReparsePoints(string root)
    {
        foreach (var directory in Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length))
        {
            var attributes = File.GetAttributes(directory);
            if (!attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                continue;
            }

            if (OperatingSystem.IsWindows())
            {
                using var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.ArgumentList.Add("/c");
                process.StartInfo.ArgumentList.Add("rmdir");
                process.StartInfo.ArgumentList.Add(directory);
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.Start();
                process.WaitForExit();
            }
            else
            {
                Directory.Delete(directory);
            }
        }
    }
}
