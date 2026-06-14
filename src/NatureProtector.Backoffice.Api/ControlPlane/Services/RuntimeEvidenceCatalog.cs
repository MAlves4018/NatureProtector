using System.Security.Cryptography;
using System.Text;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

public sealed class RuntimeEvidenceCatalog
{
    private const long MaxContentBytes = 1_048_576;
    private const int MaxCatalogItems = 250;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".json",
        ".md",
        ".txt"
    };

    private readonly string _evidenceRoot;

    public RuntimeEvidenceCatalog(string repositoryRoot)
    {
        _evidenceRoot = Path.GetFullPath(Path.Combine(repositoryRoot, "docs", "evidence"));
    }

    public RuntimeEvidenceCatalogResponse List(DateTimeOffset observedAt)
    {
        if (!Directory.Exists(_evidenceRoot))
        {
            return new RuntimeEvidenceCatalogResponse(
                observedAt,
                [],
                [new RuntimeLimitationResponse("evidence_root_missing", "docs/evidence does not exist in this repository checkout.")]);
        }

        var allItems = EnumerateEvidenceFiles()
            .Select(file => ToItem(file, contentAvailable: file.Length <= MaxContentBytes))
            .OrderByDescending(item => item.GeneratedAt)
            .ThenBy(item => item.Scope, StringComparer.Ordinal)
            .ThenBy(item => item.Title, StringComparer.Ordinal)
            .ToArray();
        var items = allItems.Take(MaxCatalogItems).ToArray();
        var limitations = new List<RuntimeLimitationResponse>();
        if (items.Length == 0)
        {
            limitations.Add(new RuntimeLimitationResponse("evidence_catalog_empty", "No allowlisted evidence files were found under docs/evidence."));
        }

        if (allItems.Length > items.Length)
        {
            limitations.Add(new RuntimeLimitationResponse(
                "evidence_catalog_truncated",
                $"Evidence catalog returned the {MaxCatalogItems} most recent allowlisted files out of {allItems.Length}."));
        }

        return new RuntimeEvidenceCatalogResponse(
            observedAt,
            items,
            limitations);
    }

    public async Task<RuntimeEvidenceContentResponse?> GetContentAsync(
        string evidenceId,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidenceId) ||
            evidenceId.Contains("..", StringComparison.Ordinal) ||
            evidenceId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return null;
        }

        var match = EnumerateEvidenceFiles()
            .FirstOrDefault(file => string.Equals(CreateEvidenceId(file), evidenceId, StringComparison.OrdinalIgnoreCase));
        if (match is null || match.Length > MaxContentBytes)
        {
            return null;
        }

        var fullPath = Path.GetFullPath(match.FullName);
        if (!IsUnderEvidenceRoot(fullPath))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        return new RuntimeEvidenceContentResponse(
            ToItem(match, contentAvailable: true),
            GetContentType(match.Extension),
            content,
            "no-store");
    }

    private IEnumerable<FileInfo> EnumerateEvidenceFiles()
    {
        if (!Directory.Exists(_evidenceRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(_evidenceRoot, "*", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .Where(file => AllowedExtensions.Contains(file.Extension))
            .Where(file => IsUnderEvidenceRoot(file.FullName));
    }

    private RuntimeEvidenceItemResponse ToItem(FileInfo file, bool contentAvailable)
    {
        var relativePath = Path.GetRelativePath(_evidenceRoot, file.FullName);
        var scope = Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? string.Empty;
        return new RuntimeEvidenceItemResponse(
            CreateEvidenceId(file),
            Path.GetFileNameWithoutExtension(file.Name),
            file.Extension.TrimStart('.').ToLowerInvariant(),
            file.LastWriteTimeUtc == DateTime.MinValue ? null : new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero),
            "repository-docs-evidence",
            string.IsNullOrWhiteSpace(scope) ? "docs/evidence" : $"docs/evidence/{scope}",
            null,
            contentAvailable,
            contentAvailable,
            file.Length,
            contentAvailable ? "Available" : "TooLarge",
            contentAvailable ? null : "Evidence content exceeds the 1 MiB HTTP content limit.");
    }

    private string CreateEvidenceId(FileInfo file)
    {
        var relativePath = Path.GetRelativePath(_evidenceRoot, file.FullName).Replace('\\', '/');
        using var sha = SHA256.Create();
        var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(relativePath))).ToLowerInvariant()[..12];
        var slug = new string(relativePath
            .Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return $"{slug}-{hash}";
    }

    private bool IsUnderEvidenceRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(_evidenceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(fullPath, _evidenceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetContentType(string extension)
        => extension.ToLowerInvariant() switch
        {
            ".csv" => "text/csv; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".md" => "text/markdown; charset=utf-8",
            _ => "text/plain; charset=utf-8"
        };
}
