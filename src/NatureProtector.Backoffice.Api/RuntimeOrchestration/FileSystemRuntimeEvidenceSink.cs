using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public sealed class FileSystemRuntimeEvidenceSink : IRuntimeEvidenceSink
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _root;

    public FileSystemRuntimeEvidenceSink(
        IOptions<RuntimeOrchestrationOptions> options,
        IHostEnvironment environment)
    {
        var configuredRoot = options.Value.EvidenceRoot;
        _root = Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(environment.ContentRootPath, configuredRoot));
    }

    public bool IsAvailable => true;
    public string AvailabilityMessage => $"Filesystem runtime evidence is enabled at '{_root}'.";

    public Task<RuntimeEvidenceReference> CreateAsync(
        string category,
        DateTimeOffset requestedAtUtc,
        string label,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var safeCategory = SanitizeRelativePath(category);
        var safeLabel = SanitizeSegment(label);
        var evidenceId = $"{requestedAtUtc:yyyyMMdd-HHmmss}-{safeLabel}-{Guid.NewGuid():N}";
        var directory = Path.Combine(_root, safeCategory, evidenceId);
        Directory.CreateDirectory(directory);
        return Task.FromResult(new RuntimeEvidenceReference(evidenceId, directory));
    }

    public Task WriteJsonAsync(
        RuntimeEvidenceReference evidence,
        string fileName,
        object value,
        CancellationToken cancellationToken)
        => File.WriteAllTextAsync(
            ResolveFile(evidence, fileName),
            JsonSerializer.Serialize(value, JsonOptions),
            cancellationToken);

    public Task WriteTextAsync(
        RuntimeEvidenceReference evidence,
        string fileName,
        string value,
        CancellationToken cancellationToken)
        => File.WriteAllTextAsync(
            ResolveFile(evidence, fileName),
            value,
            cancellationToken);

    private static string ResolveFile(RuntimeEvidenceReference evidence, string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        if (!string.Equals(safeName, fileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Runtime evidence fileName must not contain directory traversal.");
        }

        return Path.Combine(evidence.Location, safeName);
    }

    private static string SanitizeRelativePath(string value)
        => string.Join(
            Path.DirectorySeparatorChar.ToString(),
            value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeSegment));

    private static string SanitizeSegment(string value)
    {
        var safe = new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "runtime" : safe;
    }
}
