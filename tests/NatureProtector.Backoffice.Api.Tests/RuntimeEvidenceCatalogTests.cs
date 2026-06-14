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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
