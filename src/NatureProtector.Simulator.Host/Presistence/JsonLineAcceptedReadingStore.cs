using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;

/*
 * This class persists accepted readings to a local NDJSON file.
 *
 * Rationale:
 * - Day 5 requires persistence of accepted readings, but does not justify
 *   introducing a full database yet.
 * - NDJSON is easy to inspect, append-friendly and suitable for simple local persistence.
 *
 * Design considerations:
 * - One JSON object is written per line to simplify manual inspection and downstream parsing.
 * - Relative paths are resolved from the host content root.
 * - A simple lock is used to keep appends safe within the current single-process host.
 */

namespace NatureProtector.Prevention.Host.Persistence;

public sealed class JsonLineAcceptedReadingStore : IAcceptedReadingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _fullPath;
    private readonly object _sync = new();

    public JsonLineAcceptedReadingStore(
        IOptions<PreventionOptions> preventionOptions,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(preventionOptions);
        ArgumentNullException.ThrowIfNull(environment);

        var options = preventionOptions.Value
            ?? throw new ArgumentNullException(nameof(preventionOptions));

        if (string.IsNullOrWhiteSpace(options.AcceptedReadingsPath))
        {
            throw new InvalidOperationException(
                "Prevention AcceptedReadingsPath must not be null or whitespace.");
        }

        _fullPath = Path.IsPathRooted(options.AcceptedReadingsPath)
            ? options.AcceptedReadingsPath
            : Path.Combine(environment.ContentRootPath, options.AcceptedReadingsPath);

        var directory = Path.GetDirectoryName(_fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Appends one accepted reading as a single NDJSON line.
    /// </summary>
    public void Persist(AcceptedReadingRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var json = JsonSerializer.Serialize(record, JsonOptions);

        lock (_sync)
        {
            File.AppendAllText(_fullPath, json + Environment.NewLine);
        }
    }
}