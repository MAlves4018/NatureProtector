using System.Text;
using NatureProtector.Backoffice.Api.ControlPlane.Services;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeEvidenceCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"np-evidence-{Guid.NewGuid():N}");

    [Fact]
    public void List_ReportsMissingEvidenceRootExplicitly()
    {
        var catalog = new RuntimeEvidenceCatalog(_root);

        var listed = catalog.List(DateTimeOffset.UtcNow);

        Assert.Empty(listed.Items);
        var limitation = Assert.Single(listed.Limitations);
        Assert.Equal("evidence_root_missing", limitation.Code);
    }

    [Fact]
    public void List_ReportsEmptyEvidenceRootWithoutInventingItems()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs", "evidence"));
        var catalog = new RuntimeEvidenceCatalog(_root);

        var listed = catalog.List(DateTimeOffset.UtcNow);

        Assert.Empty(listed.Items);
        var limitation = Assert.Single(listed.Limitations);
        Assert.Equal("evidence_catalog_empty", limitation.Code);
    }

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

    [Theory]
    [InlineData("data.csv", "text/csv; charset=utf-8")]
    [InlineData("payload.json", "application/json; charset=utf-8")]
    [InlineData("notes.txt", "text/plain; charset=utf-8")]
    public async Task GetContentAsync_UsesContentTypeForAllowlistedExtensions(string fileName, string contentType)
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence", "run-1");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, fileName), "content", new UTF8Encoding(false));
        var catalog = new RuntimeEvidenceCatalog(_root);

        var item = Assert.Single(catalog.List(DateTimeOffset.UtcNow).Items);
        var content = await catalog.GetContentAsync(item.EvidenceId, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.NotNull(content);
        Assert.Equal(contentType, content.ContentType);
        Assert.Equal("docs/evidence/run-1", content.Metadata.Scope);
    }

    [Fact]
    public async Task List_MarksOversizedEvidenceAsUnavailableAndRejectsDownload()
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence");
        Directory.CreateDirectory(evidenceRoot);
        var oversized = new string('x', 1_048_577);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "oversized.txt"), oversized, new UTF8Encoding(false));
        var catalog = new RuntimeEvidenceCatalog(_root);

        var item = Assert.Single(catalog.List(DateTimeOffset.UtcNow).Items);
        var content = await catalog.GetContentAsync(item.EvidenceId, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.False(item.ContentAvailable);
        Assert.False(item.DownloadAvailable);
        Assert.Equal("TooLarge", item.Status);
        Assert.Contains("1 MiB", item.Limitation, StringComparison.Ordinal);
        Assert.Null(content);
    }

    [Fact]
    public async Task List_TruncatesCatalogToMostRecentAllowlistedEvidence()
    {
        var evidenceRoot = Path.Combine(_root, "docs", "evidence");
        Directory.CreateDirectory(evidenceRoot);
        for (var index = 0; index < 251; index++)
        {
            var path = Path.Combine(evidenceRoot, $"item-{index:D3}.txt");
            await File.WriteAllTextAsync(path, index.ToString("D3"), new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(path, new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc).AddSeconds(index));
        }
        var catalog = new RuntimeEvidenceCatalog(_root);

        var listed = catalog.List(DateTimeOffset.UtcNow);

        Assert.Equal(250, listed.Items.Count);
        Assert.Equal("item-250", listed.Items[0].Title);
        Assert.DoesNotContain(listed.Items, item => item.Title == "item-000");
        Assert.Contains(listed.Limitations, limitation =>
            limitation.Code == "evidence_catalog_truncated" &&
            limitation.Message.Contains("251", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("CON", false)]
    [InlineData("safe-id-123", true)]
    [InlineData("unsafe_id", false)]
    [InlineData("unsafe.id", false)]
    public void IsValidEvidenceId_AcceptsOnlyPortableOpaqueIds(string? evidenceId, bool expected)
    {
        Assert.Equal(expected, RuntimeEvidenceCatalog.IsValidEvidenceId(evidenceId));
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
