using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationEvidenceWriter(
    ILogger<ControlledValidationEvidenceWriter> logger,
    IHostEnvironment environment,
    IOptions<ControlledValidationOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<string?> WriteExpectedOutcomesAsync(
        ControlledValidationScenarioManifest manifest,
        IReadOnlyList<ControlledValidationMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(messages);

        if (!options.Value.WriteEvidenceSidecar)
        {
            logger.LogInformation(
                "Controlled validation evidence sidecar writing is disabled | RunLabel={RunLabel}",
                manifest.RunLabel);
            return null;
        }

        var outputRoot = ResolveOutputRoot(options.Value.EvidenceOutputRoot, manifest.Phase);
        var runDirectory = Path.Combine(
            outputRoot,
            $"{manifest.EventTime:yyyyMMdd-HHmmss}-{SanitizePathSegment(manifest.RunLabel)}");
        Directory.CreateDirectory(runDirectory);

        var sidecar = new ExpectedOutcomeSidecar(
            GeneratedAt: DateTimeOffset.UtcNow,
            ControlledValidationRunId: manifest.ControlledValidationRunId,
            RunLabel: manifest.RunLabel,
            ScenarioCode: manifest.ScenarioCode,
            AreaId: manifest.AreaId,
            SimulationRunId: manifest.SimulationRunId,
            EventTime: manifest.EventTime,
            ExpectedOutcomes: messages.Select(message => new ExpectedOutcomeRow(
                FaultCaseId: message.FaultCase.FaultCaseId,
                FaultLayer: message.FaultCase.FaultLayer.ToString(),
                ExpectedOutcome: message.FaultCase.ExpectedOutcome.ToString(),
                ExpectedReasonCode: message.FaultCase.ExpectedReasonCode,
                ExpectedEvents: message.FaultCase.ExpectedEvents,
                ExpectedPublishedEvents: message.FaultCase.ExpectedPublishedEvents,
                ExpectedCoverageGap: message.FaultCase.ExpectedCoverageGap,
                ValueProfile: message.FaultCase.ValueProfile,
                Sequence: message.Sequence,
                IsSetupMessage: message.IsSetupMessage,
                EventId: message.EventId,
                CorrelationId: message.CorrelationId,
                RawBodySha256: message.BodySha256)).ToArray());

        var jsonPath = Path.Combine(runDirectory, "expected-outcomes.json");
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(sidecar, JsonOptions),
            cancellationToken);

        var csvPath = Path.Combine(runDirectory, "expected-outcomes.csv");
        await File.WriteAllTextAsync(
            csvPath,
            BuildCsv(sidecar.ExpectedOutcomes),
            cancellationToken);

        logger.LogInformation(
            "Wrote controlled validation expected outcomes | RunLabel={RunLabel} | Directory={Directory}",
            manifest.RunLabel,
            runDirectory);

        return runDirectory;
    }

    private string ResolveOutputRoot(string configuredRoot, string phase)
    {
        var phaseDirectory = string.Equals(phase, ControlledValidationPhases.P3NegativePipeline, StringComparison.OrdinalIgnoreCase)
            ? "p3"
            : phase.ToLowerInvariant();
        var root = string.IsNullOrWhiteSpace(configuredRoot)
            ? $"../../docs/evidence/controlled-validation/{phaseDirectory}"
            : configuredRoot;

        return Path.IsPathRooted(root)
            ? Path.GetFullPath(root)
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, root));
    }

    private static string BuildCsv(IReadOnlyList<ExpectedOutcomeRow> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("fault_case_id,fault_layer,expected_outcome,expected_reason_code,expected_events,expected_published_events,expected_coverage_gap,value_profile,sequence,is_setup_message,event_id,correlation_id,raw_body_sha256");

        foreach (var row in rows)
        {
            builder.Append(Csv(row.FaultCaseId)).Append(',');
            builder.Append(Csv(row.FaultLayer)).Append(',');
            builder.Append(Csv(row.ExpectedOutcome)).Append(',');
            builder.Append(Csv(row.ExpectedReasonCode)).Append(',');
            builder.Append(row.ExpectedEvents?.ToString() ?? string.Empty).Append(',');
            builder.Append(row.ExpectedPublishedEvents?.ToString() ?? string.Empty).Append(',');
            builder.Append(row.ExpectedCoverageGap?.ToString() ?? string.Empty).Append(',');
            builder.Append(Csv(row.ValueProfile)).Append(',');
            builder.Append(row.Sequence).Append(',');
            builder.Append(row.IsSetupMessage ? "true" : "false").Append(',');
            builder.Append(Csv(row.EventId?.ToString())).Append(',');
            builder.Append(Csv(row.CorrelationId)).Append(',');
            builder.Append(Csv(row.RawBodySha256)).AppendLine();
        }

        return builder.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains('"', StringComparison.Ordinal) ||
               value.Contains(',', StringComparison.Ordinal) ||
               value.Contains('\n', StringComparison.Ordinal) ||
               value.Contains('\r', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray();

        return new string(chars);
    }

    private sealed record ExpectedOutcomeSidecar(
        DateTimeOffset GeneratedAt,
        Guid ControlledValidationRunId,
        string RunLabel,
        string ScenarioCode,
        Guid AreaId,
        Guid SimulationRunId,
        DateTimeOffset EventTime,
        IReadOnlyList<ExpectedOutcomeRow> ExpectedOutcomes);

    private sealed record ExpectedOutcomeRow(
        string FaultCaseId,
        string FaultLayer,
        string ExpectedOutcome,
        string? ExpectedReasonCode,
        int? ExpectedEvents,
        int? ExpectedPublishedEvents,
        int? ExpectedCoverageGap,
        string? ValueProfile,
        int Sequence,
        bool IsSetupMessage,
        Guid? EventId,
        string? CorrelationId,
        string RawBodySha256);
}
