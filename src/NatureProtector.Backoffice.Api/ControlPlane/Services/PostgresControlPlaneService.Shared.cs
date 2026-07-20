using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

// Feature slice: Shared. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    private const int RuntimeSettlementGraceSeconds = 120;
    private const int MaximumRuntimeDeadlineSeconds = 24 * 60 * 60;

    internal enum RuntimeProcessWaitOutcome
    {
        Exited,
        TimedOut,
        TimedOutAndTerminated
    }

    // <phase5-slice id="shared-mapping-and-normalization">
    private static RuntimeRunSummaryResponse? ToRuntimeRun(
        SimulationRunResponse? run,
        List<string> warnings)
    {
        if (run is null)
        {
            return null;
        }

        var metadataStatus = "empty";
        string? orchestratorCorrelationId = null;
        RuntimeRunOverridesResponse? runOverrides = null;

        if (!string.IsNullOrWhiteSpace(run.MetadataJson))
        {
            try
            {
                using var document = JsonDocument.Parse(run.MetadataJson);
                metadataStatus = "valid";
                var root = document.RootElement;

                orchestratorCorrelationId = GetStringProperty(root, "orchestrator_correlation_id");
                if (root.TryGetProperty("run_overrides", out var overrides) &&
                    overrides.ValueKind == JsonValueKind.Object)
                {
                    var requested = overrides.TryGetProperty("requested", out var requestedElement)
                        ? ReadOverrideValues(requestedElement)
                        : null;
                    var resolved = overrides.TryGetProperty("resolved", out var resolvedElement)
                        ? ReadOverrideValues(resolvedElement)
                        : null;
                    var selectedSensorNames = overrides.TryGetProperty("resolved", out var selectedSource) &&
                                              selectedSource.TryGetProperty("selected_sensor_names", out var selectedElement)
                        ? ReadStringArray(selectedElement)
                        : Array.Empty<string>();

                    runOverrides = new RuntimeRunOverridesResponse(
                        requested,
                        resolved,
                        selectedSensorNames);

                    orchestratorCorrelationId ??= resolved?.OrchestratorCorrelationId ?? requested?.OrchestratorCorrelationId;
                }
            }
            catch (JsonException)
            {
                metadataStatus = "invalid";
                warnings.Add($"SimulationRun {run.Id} has invalid MetadataJson; raw metadata was returned.");
            }
        }

        return new RuntimeRunSummaryResponse(
            run.Id,
            run.AreaCode,
            run.ScenarioCode,
            run.ScenarioName,
            run.Status,
            run.ConfigurationVersionNumber,
            run.CreatedAt,
            run.StartedAt,
            run.EndedAt,
            CalculateDurationSeconds(run.StartedAt, run.EndedAt),
            run.LogicalStartTimestamp,
            run.IntervalSeconds,
            run.NumberOfCycles,
            run.ExecutionSeed,
            run.MetadataJson,
            metadataStatus,
            orchestratorCorrelationId,
            runOverrides);
    }

    private static RuntimeRunOverrideValuesResponse? ReadOverrideValues(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RuntimeRunOverrideValuesResponse(
            GetIntProperty(element, "sensor_count"),
            GetIntProperty(element, "number_of_cycles"),
            GetIntProperty(element, "interval_seconds"),
            GetIntProperty(element, "seed"),
            GetStringProperty(element, "degradation_profile"),
            GetStringProperty(element, "orchestrator_correlation_id"),
            element.TryGetProperty("degradation_profiles", out var profilesElement)
                ? ReadStringArray(profilesElement)
                : null);
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeDegradationProfiles(
        IEnumerable<string>? profiles,
        string? legacyProfile)
    {
        var values = new List<string>();

        if (profiles is not null)
        {
            foreach (var profile in profiles)
            {
                AddDegradationProfile(values, profile);
            }
        }

        AddDegradationProfile(values, legacyProfile);

        var normalized = values
            .Select(NormalizeDegradationProfile)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count > 1)
        {
            normalized.RemoveAll(value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase));
        }

        return normalized;
    }

    private static string? NormalizeLegacyDegradationProfile(string? profile)
    {
        var normalized = NormalizeDegradationProfiles(null, profile);
        return ToLegacyDegradationProfile(normalized);
    }

    private static string? ToLegacyDegradationProfile(IReadOnlyCollection<string> profiles)
    {
        if (profiles.Count == 0)
        {
            return null;
        }

        return profiles.Count == 1
            ? profiles.First()
            : string.Join("+", profiles);
    }

    private static bool IsNoneOrEmpty(IReadOnlyCollection<string> profiles)
        => profiles.Count == 0 ||
           (profiles.Count == 1 && string.Equals(profiles.First(), "none", StringComparison.OrdinalIgnoreCase));

    private static void AddDegradationProfile(List<string> values, string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return;
        }

        foreach (var part in profile.Split([',', ';', '|', '+'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            values.Add(part);
        }
    }

    private static string NormalizeDegradationProfile(string profile)
    {
        var normalized = profile.Trim().ToLowerInvariant();
        return normalized switch
        {
            "deterministic-missing-readings" => "missing-readings",
            "missing" => "missing-readings",
            "noisy-readings" => "noise",
            "noisy" => "noise",
            "stuck" => "stuck-value",
            "flatline" => "stuck-value",
            "range" => "clipping/range",
            "clipping" => "clipping/range",
            "clipping-range" => "clipping/range",
            "delay" => "lag/delay",
            "delayed" => "lag/delay",
            "lag" => "lag/delay",
            "late" => "lag/delay",
            "duplicate-events" => "duplicate",
            "out-of-order-events" => "out-of-order",
            "outoforder" => "out-of-order",
            _ => normalized
        };
    }

    private static int? GetIntProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static double? CalculateDurationSeconds(DateTimeOffset? startedAt, DateTimeOffset? endedAt)
    {
        if (!startedAt.HasValue || !endedAt.HasValue)
        {
            return null;
        }

        return Math.Max(0, (endedAt.Value - startedAt.Value).TotalSeconds);
    }

    private static double? CalculateDurationMilliseconds(ProcessingAttemptRecord attempt)
        => CalculateDurationMilliseconds(attempt.StartedAt, attempt.FinishedAt);

    private static double? CalculateDurationMilliseconds(DateTimeOffset? startedAt, DateTimeOffset? endedAt)
    {
        if (!startedAt.HasValue || !endedAt.HasValue)
        {
            return null;
        }

        return Math.Max(0, (endedAt.Value - startedAt.Value).TotalMilliseconds);
    }

    private static DateTimeOffset? MaxFinishedAt(IEnumerable<ProcessingAttemptRecord> attempts)
    {
        var finishedAtValues = attempts
            .Where(entity => entity.FinishedAt.HasValue)
            .Select(entity => entity.FinishedAt!.Value)
            .ToArray();

        return finishedAtValues.Length == 0 ? null : finishedAtValues.Max();
    }

    private static RuntimeDataScopeResponse BuildRunDataScope(
        Guid requestedRunId,
        Guid? resolvedRunId,
        Guid? dataRunId,
        string source,
        string scope)
        => new(
            requestedRunId,
            resolvedRunId,
            dataRunId,
            DateTimeOffset.UtcNow,
            source,
            scope,
            [
                new RuntimeLimitationResponse(
                    "risk_not_recalculated",
                    "Runtime audit and timing endpoints read persisted runtime records only; they do not recalculate risk."),
                new RuntimeLimitationResponse(
                    "publisher_timestamp_optional",
                    "PublishedAt is populated by the RabbitMQ publisher for new live runs; older or non-RabbitMQ rows may have null PublishedAt.")
            ]);

    private static IReadOnlyList<RuntimeTimelinePointResponse> BuildRunTimeline(
        Guid runId,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        DateTimeOffset? firstPublishedAt,
        DateTimeOffset? firstInboxReceivedAt,
        DateTimeOffset? firstProcessingAttemptStartedAt,
        DateTimeOffset? lastProcessingAttemptFinishedAt,
        DateTimeOffset? firstRiskAssessmentCreatedAt,
        DateTimeOffset? firstAlertTriggeredAt)
    {
        var points = new List<RuntimeTimelinePointResponse>
        {
            new("requested", createdAt, "control.simulation_runs.created_at", "simulation-run", null, "measured")
        };

        AddTimelinePoint(points, "started", startedAt, "control.simulation_runs.started_at");
        AddTimelinePoint(points, "first_published", firstPublishedAt, "pipeline.event_inbox.published_at");
        AddTimelinePoint(points, "first_received", firstInboxReceivedAt, "pipeline.event_inbox.received_at");
        AddTimelinePoint(points, "first_processing_started", firstProcessingAttemptStartedAt, "pipeline.processing_attempts.started_at");
        AddTimelinePoint(points, "first_risk_assessment", firstRiskAssessmentCreatedAt, "projection.risk_assessment_log.created_at");
        AddTimelinePoint(points, "first_alert", firstAlertTriggeredAt, "projection.alert_state.triggered_at");
        AddTimelinePoint(points, "last_processing_finished", lastProcessingAttemptFinishedAt, "pipeline.processing_attempts.finished_at");
        AddTimelinePoint(points, "completed", endedAt, "control.simulation_runs.ended_at");

        return points
            .OrderBy(point => point.Timestamp)
            .ThenBy(point => point.Stage, StringComparer.Ordinal)
            .Select(point => point with { Scope = $"simulation-run:{runId:D}" })
            .ToArray();
    }

    private static void AddTimelinePoint(
        ICollection<RuntimeTimelinePointResponse> points,
        string stage,
        DateTimeOffset? timestamp,
        string source)
    {
        if (timestamp.HasValue)
        {
            points.Add(new RuntimeTimelinePointResponse(stage, timestamp.Value, source, "simulation-run", null, "measured"));
        }
    }

    private static string? ParseAlertState(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        const string prefix = "AlertState=";
        var startIndex = message.IndexOf(prefix, StringComparison.Ordinal);

        if (startIndex < 0)
        {
            return null;
        }

        startIndex += prefix.Length;
        var endIndex = message.IndexOf(';', startIndex);

        if (endIndex < 0)
        {
            return null;
        }

        var parsedValue = message[startIndex..endIndex].Trim();
        return parsedValue.Length == 0 ? null : parsedValue;
    }

    private static string BuildOperationalStatusReason(
        string? coverageStatus,
        string? freshnessStatus,
        string? carryForwardStatus)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(coverageStatus) ? null : $"coverage={coverageStatus}",
            string.IsNullOrWhiteSpace(freshnessStatus) ? null : $"freshness={freshnessStatus}",
            string.IsNullOrWhiteSpace(carryForwardStatus) ? null : $"carryForward={carryForwardStatus}"
        };

        return string.Join("; ", parts.Where(part => part is not null));
    }

    /// <summary>
    /// Projeta uma versão de configuração para o contrato da API.
    /// </summary>
    private static async Task<ConfigurationVersionResponse?> ProjectConfigurationAsync(
        NatureProtectorControlDbContext dbContext,
        IQueryable<Infrastructure.Postgres.Control.ConfigurationVersionRecord> query,
        CancellationToken cancellationToken)
    {
        return await query
            .Select(entity => new ConfigurationVersionResponse(
                entity.VersionNumber,
                entity.IsActive,
                entity.Description,
                entity.CreatedAt,
                entity.CreatedBy,
                dbContext.Areas.Count(area => area.ConfigurationVersionId == entity.Id),
                dbContext.GridCells.Count(cell => cell.ConfigurationVersionId == entity.Id),
                dbContext.SensorNodes.Count(node => node.ConfigurationVersionId == entity.Id),
                dbContext.ScenarioDefinitions.Count(scenario => scenario.ConfigurationVersionId == entity.Id),
                dbContext.SimulationRuns.Count(run => run.ConfigurationVersionId == entity.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Resolve a versão de configuração pedida ou, na sua ausência, a ativa.
    /// </summary>
    private static async Task<int?> ResolveConfigurationVersionAsync(
        NatureProtectorControlDbContext dbContext,
        int? configurationVersion,
        CancellationToken cancellationToken)
    {
        if (configurationVersion.HasValue)
        {
            var exists = await dbContext.ConfigurationVersions
                .AsNoTracking()
                .AnyAsync(entity => entity.VersionNumber == configurationVersion.Value, cancellationToken);

            return exists ? configurationVersion.Value : null;
        }

        return await dbContext.ConfigurationVersions
            .AsNoTracking()
            .Where(entity => entity.IsActive)
            .OrderByDescending(entity => entity.VersionNumber)
            .Select(entity => (int?)entity.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Normaliza o offset de paginação para valores não negativos.
    /// </summary>
    private static int NormalizeSkip(int skip)
        => Math.Max(0, skip);

    /// <summary>
    /// Aplica limites defensivos ao tamanho das páginas devolvidas pela API.
    /// </summary>
    private static int NormalizeTake(int take)
    {
        if (take <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(take, MaxTake);
    }

    private static int NormalizeRecentMinutes(int recentMinutes)
    {
        if (recentMinutes <= 0)
        {
            return DefaultRecentMinutes;
        }

        return Math.Clamp(recentMinutes, MinRecentMinutes, MaxRecentMinutes);
    }

    internal static async Task<RuntimeProcessWaitOutcome> WaitForRuntimeProcessAsync(
        Process process,
        TimeSpan? timeout,
        bool terminateOnTimeout,
        Func<string, Task> writeWarningAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(writeWarningAsync);

        CancellationTokenSource? timeoutCts = null;
        var waitToken = cancellationToken;
        if (timeout is TimeSpan configuredTimeout)
        {
            if (configuredTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive when provided.");
            }

            timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(configuredTimeout);
            waitToken = timeoutCts.Token;
        }

        try
        {
            await process.WaitForExitAsync(waitToken);
            return RuntimeProcessWaitOutcome.Exited;
        }
        catch (OperationCanceledException) when (
            timeoutCts is not null &&
            timeoutCts.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            if (terminateOnTimeout &&
                await TryTerminateProcessTreeAsync(process, timeout!.Value, writeWarningAsync))
            {
                return RuntimeProcessWaitOutcome.TimedOutAndTerminated;
            }

            return RuntimeProcessWaitOutcome.TimedOut;
        }
        finally
        {
            timeoutCts?.Dispose();
        }
    }

    internal static TimeSpan CalculateRuntimeOperationDeadline(RuntimeRunStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var configuredSeconds = Math.Clamp(request.TimeoutSeconds, 5, MaximumRuntimeDeadlineSeconds);
        var predictedSeconds = 0L;
        if (request.NumberOfCycles is > 0 && request.IntervalSeconds is > 0)
        {
            predictedSeconds = checked((long)request.NumberOfCycles.Value * request.IntervalSeconds.Value);
        }

        var producerBudgetSeconds = Math.Max(configuredSeconds, predictedSeconds);
        var operationDeadlineSeconds = Math.Min(
            producerBudgetSeconds + RuntimeSettlementGraceSeconds,
            MaximumRuntimeDeadlineSeconds);
        return TimeSpan.FromSeconds(operationDeadlineSeconds);
    }

    private static async Task<bool> TryTerminateProcessTreeAsync(
        Process process,
        TimeSpan timeout,
        Func<string, Task> writeWarningAsync)
    {
        if (process.HasExited)
        {
            return false;
        }

        try
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            await writeWarningAsync($"Simulator.Host did not exit within {timeout.TotalSeconds:0} seconds and was terminated.");
            return true;
        }
        catch (Exception exception)
        {
            await writeWarningAsync($"Simulator.Host termination failed: {exception.Message}");
            return false;
        }
    }
    // </phase5-slice>

}
