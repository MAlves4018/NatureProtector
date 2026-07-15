using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

// Feature slice: RuntimeDiagnostics. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    // <phase5-slice id="runtime-diagnostics-api">
    public Task<RuntimeDiagnosticCatalogResponse> ListRuntimeDiagnosticsAsync(CancellationToken cancellationToken)
        => Task.FromResult(new RuntimeDiagnosticCatalogResponse(RuntimeDiagnostics));

    public async Task<RuntimeDiagnosticResultResponse?> ExecuteRuntimeDiagnosticAsync(
        string diagnosticId,
        RuntimeDiagnosticRequest request,
        CancellationToken cancellationToken)
    {
        var definition = RuntimeDiagnostics.SingleOrDefault(entity =>
            string.Equals(entity.Id, diagnosticId, StringComparison.OrdinalIgnoreCase));

        if (definition is null)
        {
            return null;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedAreaCode = string.IsNullOrWhiteSpace(request.AreaCode) ? null : request.AreaCode.Trim();
        var normalizedRecentMinutes = NormalizeRecentMinutes(request.RecentMinutes);
        var recentSince = DateTimeOffset.UtcNow.AddMinutes(-normalizedRecentMinutes);
        Guid? areaId = normalizedAreaCode is null
            ? (Guid?)null
            : (await dbContext.Areas
                .AsNoTracking()
                .Where(entity => entity.Code == normalizedAreaCode)
                .Select(entity => (Guid?)entity.Id)
                .FirstOrDefaultAsync(cancellationToken)) ?? Guid.Empty;

        var result = diagnosticId.ToLowerInvariant() switch
        {
            "runtime-table-counts" => await DiagnosticRuntimeTableCountsAsync(dbContext, definition, cancellationToken),
            "active-runs" => await DiagnosticActiveRunsAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-runs" => await DiagnosticLatestRunsAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "inbox-by-status" => await DiagnosticInboxByStatusAsync(dbContext, definition, areaId, cancellationToken),
            "attempts-by-outcome" => await DiagnosticAttemptsByOutcomeAsync(dbContext, definition, areaId, cancellationToken),
            "failed-attempts-by-error" => await DiagnosticFailedAttemptsByErrorAsync(dbContext, definition, areaId, recentSince, cancellationToken),
            "latest-rejected-events" => await DiagnosticLatestRejectedEventsAsync(dbContext, definition, areaId, cancellationToken),
            "latest-quarantined-events" => await DiagnosticLatestQuarantinedEventsAsync(dbContext, definition, areaId, cancellationToken),
            "latest-run-expected-vs-observed" => await DiagnosticLatestRunExpectedVsObservedAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-events-by-cycle" => await DiagnosticLatestRunEventsByCycleAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-risk-by-metric" => await DiagnosticLatestRunRiskByMetricAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-np-vs-fwi-kbdi" => await DiagnosticLatestRunNpVsFwiAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-np-vs-fwi" => await DiagnosticLatestRunNpVsFwiAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-portuguese-context-proxy" => await DiagnosticLatestRunPortugueseContextProxyAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-kbdi-series-context" => await DiagnosticLatestRunKbdiSeriesContextAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-components" => await DiagnosticLatestRunComponentsAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-quality-by-profile" => await DiagnosticLatestRunQualityByProfileAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-degradation-effects" => await DiagnosticLatestRunDegradationEffectsAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-cell-context" => await DiagnosticLatestRunCellContextAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-fwi-input-completeness" => await DiagnosticLatestRunFwiInputCompletenessAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-kbdi-input-completeness" => await DiagnosticLatestRunFwiInputCompletenessAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "latest-run-coverage-freshness" => await DiagnosticLatestRunCoverageFreshnessAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "area-operational-state" => await DiagnosticAreaOperationalStateAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "cell-operational-states" => await DiagnosticCellOperationalStatesAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "active-alerts" => await DiagnosticActiveAlertsAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            "recent-alert-transitions" => await DiagnosticRecentAlertTransitionsAsync(dbContext, definition, normalizedAreaCode, recentSince, cancellationToken),
            "scenario-definition-details" => await DiagnosticScenarioDefinitionDetailsAsync(dbContext, definition, normalizedAreaCode, request.ScenarioCode, cancellationToken),
            "compare-latest-b-vs-c" => await DiagnosticCompareLatestBvsCAsync(dbContext, definition, normalizedAreaCode, cancellationToken),
            _ => null
        };

        return result;
    }

    // </phase5-slice>

    // <phase5-slice id="runtime-diagnostics-queries">
    private static RuntimeDiagnosticResultResponse DiagnosticResult(
        RuntimeDiagnosticDefinitionResponse definition,
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyList<string>? limitations = null)
        => new(definition.Id, definition.Title, definition.Description, columns, rows, limitations ?? []);

    private static Dictionary<string, string?> Row(params (string Key, object? Value)[] values)
        => values.ToDictionary(item => item.Key, item => FormatDiagnosticValue(item.Value));

    private static string? FormatDiagnosticValue(object? value)
        => value switch
        {
            null => null,
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("o"),
            DateTime dateTime => dateTime.ToString("o"),
            double number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            float number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            decimal number => number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

    private static async Task<IReadOnlyList<RuntimeTableCountResponse>> BuildRuntimeTableCountsAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
        =>
        [
            new("control", "simulation_runs", await dbContext.SimulationRuns.CountAsync(cancellationToken)),
            new("pipeline", "event_inbox", await dbContext.InboxEvents.CountAsync(cancellationToken)),
            new("pipeline", "processing_attempts", await dbContext.ProcessingAttempts.CountAsync(cancellationToken)),
            new("pipeline", "rejected_events", await dbContext.RejectedEvents.CountAsync(cancellationToken)),
            new("pipeline", "quarantined_events", await dbContext.QuarantinedEvents.CountAsync(cancellationToken)),
            new("projection", "accepted_reading_log", await dbContext.AcceptedReadingLogs.CountAsync(cancellationToken)),
            new("projection", "risk_assessment_log", await dbContext.RiskAssessmentLogs.CountAsync(cancellationToken)),
            new("projection", "area_risk_snapshot_log", await dbContext.AreaRiskSnapshotLogs.CountAsync(cancellationToken)),
            new("projection", "cell_operational_state", await dbContext.CellOperationalStates.CountAsync(cancellationToken)),
            new("projection", "area_operational_state", await dbContext.AreaOperationalStates.CountAsync(cancellationToken)),
            new("projection", "alert_state", await dbContext.AlertStates.CountAsync(cancellationToken))
        ];

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticRuntimeTableCountsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        CancellationToken cancellationToken)
    {
        var counts = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);
        return DiagnosticResult(
            definition,
            ["schema", "table", "count"],
            counts.Select(item => Row(
                ("schema", item.Schema),
                ("table", item.Table),
                ("count", item.Count))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticActiveRunsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SimulationRuns.AsNoTracking().Where(entity => entity.EndedAt == null);
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .Select(entity => new
            {
                entity.Id,
                AreaCode = entity.Area!.Code,
                entity.ScenarioCode,
                entity.Status,
                entity.StartedAt,
                entity.CreatedAt,
                entity.NumberOfCycles,
                entity.IntervalSeconds,
                entity.ExecutionSeed
            })
            .ToListAsync(cancellationToken);

        return DiagnosticResult(
            definition,
            ["id", "areaCode", "scenarioCode", "status", "startedAt", "createdAt", "numberOfCycles", "intervalSeconds", "executionSeed"],
            rows.OrderByDescending(entity => entity.CreatedAt)
                .Select(entity => Row(
                    ("id", entity.Id),
                    ("areaCode", entity.AreaCode),
                    ("scenarioCode", entity.ScenarioCode),
                    ("status", entity.Status),
                    ("startedAt", entity.StartedAt),
                    ("createdAt", entity.CreatedAt),
                    ("numberOfCycles", entity.NumberOfCycles),
                    ("intervalSeconds", entity.IntervalSeconds),
                    ("executionSeed", entity.ExecutionSeed)))
                .ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SimulationRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .OrderByDescending(entity => entity.CreatedAt)
            .Take(10)
            .Select(entity => new
            {
                entity.Id,
                AreaCode = entity.Area!.Code,
                entity.ScenarioCode,
                entity.StartedAt,
                entity.EndedAt,
                entity.Status,
                entity.NumberOfCycles,
                entity.IntervalSeconds,
                entity.ExecutionSeed,
                entity.MetadataJson
            })
            .ToListAsync(cancellationToken);

        return DiagnosticResult(
            definition,
            ["id", "areaCode", "scenarioCode", "startedAt", "endedAt", "status", "numberOfCycles", "intervalSeconds", "executionSeed", "metadata"],
            rows.Select(entity => Row(
                ("id", entity.Id),
                ("areaCode", entity.AreaCode),
                ("scenarioCode", entity.ScenarioCode),
                ("startedAt", entity.StartedAt),
                ("endedAt", entity.EndedAt),
                ("status", entity.Status),
                ("numberOfCycles", entity.NumberOfCycles),
                ("intervalSeconds", entity.IntervalSeconds),
                ("executionSeed", entity.ExecutionSeed),
                ("metadata", SummarizeMetadata(entity.MetadataJson)))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticInboxByStatusAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        Guid? areaId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InboxEvents.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.AreaId == areaId.Value);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["status", "count"],
            rows.GroupBy(entity => entity.Status)
                .OrderBy(group => group.Key.ToString())
                .Select(group => Row(("status", group.Key), ("count", group.Count())))
                .ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticAttemptsByOutcomeAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        Guid? areaId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProcessingAttempts.AsNoTracking();
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.InboxEvent!.AreaId == areaId.Value);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["outcome", "count"],
            rows.GroupBy(entity => entity.Outcome)
                .OrderBy(group => group.Key.ToString())
                .Select(group => Row(("outcome", group.Key), ("count", group.Count())))
                .ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticFailedAttemptsByErrorAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        Guid? areaId,
        DateTimeOffset recentSince,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ProcessingAttempts.AsNoTracking()
            .Where(entity => entity.StartedAt >= recentSince &&
                (entity.Outcome == ProcessingAttemptOutcome.Failed ||
                 entity.Outcome == ProcessingAttemptOutcome.RetryScheduled ||
                 entity.Outcome == ProcessingAttemptOutcome.Quarantined));
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.InboxEvent!.AreaId == areaId.Value);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["outcome", "errorCode", "errorMessage", "count", "firstStartedAt", "lastStartedAt"],
            rows.GroupBy(entity => new { entity.Outcome, entity.ErrorCode, entity.ErrorMessage })
                .OrderByDescending(group => group.Max(entity => entity.StartedAt))
                .Select(group => Row(
                    ("outcome", group.Key.Outcome),
                    ("errorCode", group.Key.ErrorCode),
                    ("errorMessage", group.Key.ErrorMessage),
                    ("count", group.Count()),
                    ("firstStartedAt", group.Min(entity => entity.StartedAt)),
                    ("lastStartedAt", group.Max(entity => entity.StartedAt))))
                .ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRejectedEventsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        Guid? areaId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RejectedEvents.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.InboxEvent != null && entity.InboxEvent.AreaId == areaId.Value);
        }

        var rows = await query.OrderByDescending(entity => entity.RejectedAt).Take(20).ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["id", "eventId", "rejectionCode", "rejectionReason", "rejectedAt"],
            rows.Select(entity => Row(
                ("id", entity.Id),
                ("eventId", entity.EventId),
                ("rejectionCode", entity.RejectionCode),
                ("rejectionReason", entity.RejectionReason),
                ("rejectedAt", entity.RejectedAt))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestQuarantinedEventsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        Guid? areaId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.QuarantinedEvents.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.InboxEvent!.AreaId == areaId.Value);
        }

        var rows = await query.OrderByDescending(entity => entity.QuarantinedAt).Take(20).ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["id", "eventId", "finalAttemptNumber", "quarantineCode", "quarantineReason", "quarantinedAt"],
            rows.Select(entity => Row(
                ("id", entity.Id),
                ("eventId", entity.EventId),
                ("finalAttemptNumber", entity.FinalAttemptNumber),
                ("quarantineCode", entity.QuarantineCode),
                ("quarantineReason", entity.QuarantineReason),
                ("quarantinedAt", entity.QuarantinedAt))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunExpectedVsObservedAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["metric", "value"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var observed = await CountAcceptedReadingsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var sensorCount = TryGetResolvedSensorCount(latestRun.MetadataJson);
        var expected = sensorCount.HasValue ? sensorCount.Value * latestRun.NumberOfCycles : (int?)null;
        var rows = new[]
        {
            Row(("metric", "simulationRunId"), ("value", latestRun.Id)),
            Row(("metric", "scenarioCode"), ("value", latestRun.ScenarioCode)),
            Row(("metric", "expected"), ("value", expected)),
            Row(("metric", "observed"), ("value", observed)),
            Row(("metric", "missing"), ("value", expected.HasValue ? Math.Max(0, expected.Value - observed) : null)),
            Row(("metric", "note"), ("value", expected.HasValue ? "Expected = resolved/requested sensor count * NumberOfCycles." : "Expected could not be calculated because sensor_count was not found in MetadataJson."))
        };

        return DiagnosticResult(definition, ["metric", "value"], rows);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunEventsByCycleAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["cycle", "count", "firstEventTime", "lastEventTime"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var readings = await GetAcceptedReadingsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var intervalSeconds = Math.Max(1, latestRun.IntervalSeconds);
        var rows = readings
            .GroupBy(entity => Math.Max(0, (int)Math.Round((entity.EventTime - latestRun.LogicalStartTimestamp).TotalSeconds / intervalSeconds)))
            .OrderBy(group => group.Key)
            .Select(group => Row(
                ("cycle", group.Key),
                ("count", group.Count()),
                ("firstEventTime", group.Min(entity => entity.EventTime)),
                ("lastEventTime", group.Max(entity => entity.EventTime))))
            .ToArray();

        return DiagnosticResult(definition, ["cycle", "count", "firstEventTime", "lastEventTime"], rows);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunRiskByMetricAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["metricType", "count", "minValue", "maxValue", "avgValue", "minScore", "maxScore", "avgScore"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var readings = await GetAcceptedReadingsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var sourceEventIds = readings.Select(entity => entity.EventId).ToHashSet();
        var risks = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => sourceEventIds.Contains(entity.SourceEventId))
            .ToListAsync(cancellationToken);

        var joined = risks
            .Join(readings, risk => risk.SourceEventId, reading => reading.EventId, (risk, reading) => new { risk, reading })
            .ToArray();

        return DiagnosticResult(
            definition,
            ["metricType", "count", "minValue", "maxValue", "avgValue", "minScore", "maxScore", "avgScore"],
            joined.GroupBy(item => item.reading.MetricType)
                .OrderBy(group => group.Key)
                .Select(group => Row(
                    ("metricType", group.Key),
                    ("count", group.Count()),
                    ("minValue", group.Min(item => item.reading.Value)),
                    ("maxValue", group.Max(item => item.reading.Value)),
                    ("avgValue", group.Average(item => item.reading.Value)),
                    ("minScore", group.Min(item => item.risk.RiskScore)),
                    ("maxScore", group.Max(item => item.risk.RiskScore)),
                    ("avgScore", group.Average(item => item.risk.RiskScore))))
                .ToArray(),
            ["This diagnostic joins risk_assessment_log to accepted_reading_log by SourceEventId/EventId and does not recalculate risk."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunNpVsFwiAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["metric", "value"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var risks = await GetRiskAssessmentsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var latestRisk = risks.OrderByDescending(entity => entity.Timestamp).ThenByDescending(entity => entity.CreatedAt).FirstOrDefault();
        var dailyState = await GetLatestDailyCellStateForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var npClass = ClassifyNatureProtector(latestRisk?.RiskScore);
        var fwiClass = ClassifyFireWeatherIndex(
            dailyState?.FireWeatherIndex,
            dailyState?.NormalizedFireWeatherIndex,
            dailyState?.FireWeatherCalculationStatus);
        var kbdiClass = ClassifyKbdi(
            dailyState?.KeetchByramDroughtIndex,
            dailyState?.NormalizedKeetchByramDroughtIndex,
            dailyState?.KbdiCalculationStatus,
            dailyState?.KbdiLimitations);
        var proxy = BuildPortugueseContextProxy(fwiClass.IpmaClass, latestRisk?.TerritoryComponent);
        var localPercentile = LocalFwiPercentileNotAvailable();
        var rows = new[]
        {
            Row(("metric", "simulationRunId"), ("value", latestRun.Id)),
            Row(("metric", "scenarioCode"), ("value", latestRun.ScenarioCode)),
            Row(("metric", "npScore"), ("value", latestRisk?.RiskScore)),
            Row(("metric", "npClass"), ("value", npClass.Code)),
            Row(("metric", "baseRisk"), ("value", latestRisk?.BaseRisk)),
            Row(("metric", "adjustedScore"), ("value", latestRisk?.AdjustedScore)),
            Row(("metric", "score100"), ("value", latestRisk?.Score100)),
            Row(("metric", "fireWeatherIndex"), ("value", dailyState?.FireWeatherIndex)),
            Row(("metric", "normalizedFWI"), ("value", dailyState?.NormalizedFireWeatherIndex)),
            Row(("metric", "fwiStatus"), ("value", dailyState?.FireWeatherCalculationStatus)),
            Row(("metric", "fwiIpmaClass"), ("value", fwiClass.IpmaClass)),
            Row(("metric", "fwiIpmaClassLabel"), ("value", fwiClass.IpmaLabel)),
            Row(("metric", "fwiEffisClass"), ("value", fwiClass.EffisClass)),
            Row(("metric", "fwiDistanceToNextIpmaClass"), ("value", fwiClass.DistanceToNext)),
            Row(("metric", "kbdi"), ("value", dailyState?.KeetchByramDroughtIndex)),
            Row(("metric", "normalizedKBDI"), ("value", dailyState?.NormalizedKeetchByramDroughtIndex)),
            Row(("metric", "kbdiStatus"), ("value", dailyState?.KbdiCalculationStatus)),
            Row(("metric", "kbdiDrynessClass"), ("value", kbdiClass.Code)),
            Row(("metric", "kbdiDrynessClassLabel"), ("value", kbdiClass.Label)),
            Row(("metric", "kbdiAntecedentHistoryQuality"), ("value", kbdiClass.AntecedentQuality)),
            Row(("metric", "portugueseContextRiskProxy"), ("value", proxy.Code)),
            Row(("metric", "territorialHazardProxy"), ("value", proxy.TerritoryClass)),
            Row(("metric", "localFwiPercentileStatus"), ("value", localPercentile.Status)),
            Row(("metric", "localFwiPercentileReason"), ("value", localPercentile.Reason)),
            Row(("metric", "parameterSetVersion"), ("value", latestRisk?.ParameterSetVersion ?? dailyState?.CandidateParameterSetVersion)),
            Row(("metric", "limitations"), ("value", string.Join("; ", new[] { latestRisk?.Limitations, dailyState?.FireWeatherLimitations, dailyState?.KbdiLimitations }.Where(value => !string.IsNullOrWhiteSpace(value)))))
        };

        return DiagnosticResult(
            definition,
            ["metric", "value"],
            rows,
            ["FWI/KBDI are persisted comparison/provenance context. This diagnostic does not claim scientific validation and does not recalculate NP score."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunPortugueseContextProxyAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["metric", "value"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var latestRisk = (await GetRiskAssessmentsForRunAsync(dbContext, latestRun.Id, cancellationToken))
            .OrderByDescending(entity => entity.Timestamp)
            .ThenByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();
        var dailyState = await GetLatestDailyCellStateForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var fwiClass = ClassifyFireWeatherIndex(
            dailyState?.FireWeatherIndex,
            dailyState?.NormalizedFireWeatherIndex,
            dailyState?.FireWeatherCalculationStatus);
        var proxy = BuildPortugueseContextProxy(fwiClass.IpmaClass, latestRisk?.TerritoryComponent);
        var rows = new[]
        {
            Row(("metric", "simulationRunId"), ("value", latestRun.Id)),
            Row(("metric", "fwiIpmaClass"), ("value", fwiClass.IpmaClass)),
            Row(("metric", "territoryComponent"), ("value", latestRisk?.TerritoryComponent)),
            Row(("metric", "territorialHazardProxy"), ("value", proxy.TerritoryClass)),
            Row(("metric", "portugueseContextRiskProxy"), ("value", proxy.Code)),
            Row(("metric", "proxyLabel"), ("value", proxy.Label)),
            Row(("metric", "status"), ("value", proxy.Status)),
            Row(("metric", "matrixVersion"), ("value", "Candidate Parameter Set V1.0")),
            Row(("metric", "provenance"), ("value", "candidate_portuguese_context_proxy")),
            Row(("metric", "limitations"), ("value", proxy.Limitations))
        };

        return DiagnosticResult(
            definition,
            ["metric", "value"],
            rows,
            ["PortugueseContextRiskProxy is a candidate V1 interpretation aid inspired by Portuguese rural fire danger context; it is not the official IPMA/RCM/PIR product."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunKbdiSeriesContextAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["metric", "value"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var dailyState = await GetLatestDailyCellStateForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var antecedentDays = dailyState?.KbdiLimitations?.Contains("antecedent_kbdi_candidate_default", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(dailyState?.KbdiCalculationStatus, "LimitedAntecedentHistory", StringComparison.OrdinalIgnoreCase)
                ? 0
                : (int?)null;
        var rows = new[]
        {
            Row(("metric", "simulationRunId"), ("value", latestRun.Id)),
            Row(("metric", "logicalDate"), ("value", dailyState?.LogicalDate)),
            Row(("metric", "previousKbdi"), ("value", dailyState?.PreviousKeetchByramDroughtIndex)),
            Row(("metric", "kbdi"), ("value", dailyState?.KeetchByramDroughtIndex)),
            Row(("metric", "normalizedKBDI"), ("value", dailyState?.NormalizedKeetchByramDroughtIndex)),
            Row(("metric", "kbdiStatus"), ("value", dailyState?.KbdiCalculationStatus)),
            Row(("metric", "antecedentDays"), ("value", antecedentDays)),
            Row(("metric", "sameDayIdempotencyPolicy"), ("value", "same_logical_date_uses_previous_daily_kbdi_not_current_event_kbdi")),
            Row(("metric", "limitations"), ("value", dailyState?.KbdiLimitations))
        };

        return DiagnosticResult(
            definition,
            ["metric", "value"],
            rows,
            ["KBDI is a daily drought context. If only one scenario daily_reference exists, antecedent history is limited and the value remains candidate context."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunComponentsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["timestamp", "sensorId", "gridCellId", "riskScore", "baseRisk", "adjustedScore", "score100", "M", "D", "T", "H", "F", "G", "C", "I", "dominantDriver", "parameterSetVersion", "calculationStatus", "limitations"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var risks = await GetRiskAssessmentsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var rows = risks
            .OrderByDescending(entity => entity.Timestamp)
            .Take(25)
            .Select(entity => Row(
                ("timestamp", entity.Timestamp),
                ("sensorId", entity.SensorId),
                ("gridCellId", entity.GridCellId),
                ("riskScore", entity.RiskScore),
                ("baseRisk", entity.BaseRisk),
                ("adjustedScore", entity.AdjustedScore),
                ("score100", entity.Score100),
                ("M", entity.MeteorologyComponent),
                ("D", entity.DroughtComponent),
                ("T", entity.TerritoryComponent),
                ("H", entity.HazardComponent),
                ("F", entity.FuelComponent),
                ("G", entity.GeomorphologyComponent),
                ("C", entity.ConfidenceFactor),
                ("I", entity.IntegrityFactor),
                ("dominantDriver", entity.DominantDriver),
                ("parameterSetVersion", entity.ParameterSetVersion),
                ("calculationStatus", entity.CalculationStatus),
                ("limitations", entity.Limitations)))
            .ToArray();

        return DiagnosticResult(
            definition,
            ["timestamp", "sensorId", "gridCellId", "riskScore", "baseRisk", "adjustedScore", "score100", "M", "D", "T", "H", "F", "G", "C", "I", "dominantDriver", "parameterSetVersion", "calculationStatus", "limitations"],
            rows);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunQualityByProfileAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["profile", "metric", "value"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var readings = await GetAcceptedReadingsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var risks = await GetRiskAssessmentsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var profiles = ExtractDegradationProfiles(latestRun.MetadataJson);
        if (profiles.Count == 0)
        {
            profiles = ["none"];
        }

        var expected = TryGetResolvedSensorCount(latestRun.MetadataJson) is { } sensorCount
            ? sensorCount * latestRun.NumberOfCycles
            : (int?)null;
        var quality = BuildQualityFlagSummary(readings, expected);
        var eligibility = BuildEligibilitySummary(readings.Count, risks);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        foreach (var profile in profiles)
        {
            rows.Add(Row(("profile", profile), ("metric", "acceptedReadings"), ("value", readings.Count)));
            rows.Add(Row(("profile", profile), ("metric", "riskAssessments"), ("value", risks.Count)));
            rows.Add(Row(("profile", profile), ("metric", "missingEvents"), ("value", expected.HasValue ? Math.Max(0, expected.Value - readings.Count) : null)));
            foreach (var item in quality)
            {
                rows.Add(Row(("profile", profile), ("metric", $"quality:{item.Status}"), ("value", item.Count)));
            }

            foreach (var item in eligibility)
            {
                rows.Add(Row(("profile", profile), ("metric", $"eligibility:{item.Status}"), ("value", item.Count)));
            }
        }

        return DiagnosticResult(
            definition,
            ["profile", "metric", "value"],
            rows,
            ["Profiles are read from run metadata; quality/eligibility summaries are persisted aggregate signals and not a physical risk recalculation."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunDegradationEffectsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["profile", "metric", "value", "status"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var readings = await GetAcceptedReadingsForRunAsync(dbContext, latestRun.Id, cancellationToken);
        var profiles = ExtractDegradationProfiles(latestRun.MetadataJson);
        if (profiles.Count == 0)
        {
            profiles = ["none"];
        }

        var expected = TryGetResolvedSensorCount(latestRun.MetadataJson) is { } sensorCount
            ? sensorCount * latestRun.NumberOfCycles
            : (int?)null;
        var missingEvents = expected.HasValue ? Math.Max(0, expected.Value - readings.Count) : (int?)null;
        var delayedCount = readings.Count(reading =>
            string.Equals(reading.OperationalState, "Delayed", StringComparison.OrdinalIgnoreCase));
        var duplicateCount = readings
            .GroupBy(reading => reading.CorrelationId)
            .Count(group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1);
        var missingActive = profiles.Contains("missing-readings", StringComparer.OrdinalIgnoreCase);
        var noiseActive = profiles.Contains("noise", StringComparer.OrdinalIgnoreCase) ||
            profiles.Contains("noisy-readings", StringComparer.OrdinalIgnoreCase);
        var lagActive = profiles.Contains("lag/delay", StringComparer.OrdinalIgnoreCase) ||
            profiles.Contains("lag", StringComparer.OrdinalIgnoreCase) ||
            profiles.Contains("delay", StringComparer.OrdinalIgnoreCase) ||
            profiles.Contains("delayed-events", StringComparer.OrdinalIgnoreCase);
        var duplicateActive = profiles.Contains("duplicate", StringComparer.OrdinalIgnoreCase) ||
            profiles.Contains("duplicate-events", StringComparer.OrdinalIgnoreCase);
        var outlierActive = profiles.Contains("outlier", StringComparer.OrdinalIgnoreCase);
        var stuckActive = profiles.Contains("stuck-value", StringComparer.OrdinalIgnoreCase);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        rows.Add(Row(("profile", string.Join("+", profiles)), ("metric", "expectedEvents"), ("value", expected), ("status", expected.HasValue ? "observed" : "not_exposed")));
        rows.Add(Row(("profile", string.Join("+", profiles)), ("metric", "acceptedReadings"), ("value", readings.Count), ("status", "observed")));
        rows.Add(Row(("profile", "missing-readings"), ("metric", "missingEvents"), ("value", missingEvents), ("status", missingActive && missingEvents is > 0 ? "effect_observed" : missingActive ? "not_observed" : "profile_inactive")));
        rows.Add(Row(("profile", "duplicate"), ("metric", "duplicateCorrelationIds"), ("value", duplicateCount), ("status", duplicateCount > 0 ? "effect_observed" : duplicateActive ? "not_observed_or_not_persisted" : "profile_inactive")));
        rows.Add(Row(("profile", "lag/delay"), ("metric", "delayedOperationalStateCount"), ("value", delayedCount), ("status", delayedCount > 0 ? "effect_observed" : lagActive ? "applied_below_threshold_or_not_persisted" : "profile_inactive")));
        rows.Add(Row(("profile", "outlier"), ("metric", "outlierFlagCount"), ("value", 0), ("status", outlierActive ? "not_materialized_in_accepted_reading_log" : "profile_inactive")));
        rows.Add(Row(("profile", "stuck-value"), ("metric", "stuckFlagCount"), ("value", 0), ("status", stuckActive ? "not_materialized_in_accepted_reading_log" : "profile_inactive")));

        var scenarioQuery = dbContext.ScenarioDefinitions
            .AsNoTracking()
            .Where(entity => entity.Code == latestRun.ScenarioCode);
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            scenarioQuery = scenarioQuery.Where(entity => entity.Area!.Code == areaCode);
        }

        var scenario = await scenarioQuery
            .OrderByDescending(entity => entity.ConfigurationVersion!.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);
        var baseValues = scenario is null
            ? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            : ExtractSimulatorBaseValues(scenario.ParametersJson);
        foreach (var group in readings.GroupBy(reading => reading.MetricType).OrderBy(group => group.Key))
        {
            var values = group.Select(reading => reading.Value).ToArray();
            var min = values.Length == 0 ? (double?)null : values.Min();
            var max = values.Length == 0 ? (double?)null : values.Max();
            var avg = values.Length == 0 ? (double?)null : values.Average();
            var baseKey = group.Key switch
            {
                "Temperature" => "BaseTemperature",
                "Humidity" => "BaseHumidity",
                "WindSpeed" => "BaseWindSpeed",
                _ => string.Empty
            };
            var avgAbsDelta = baseValues.TryGetValue(baseKey, out var baseValue) && values.Length > 0
                ? values.Average(value => Math.Abs(value - baseValue))
                : (double?)null;
            rows.Add(Row(("profile", "natural-variation"), ("metric", $"metric:{group.Key}:min"), ("value", min), ("status", "observed_range")));
            rows.Add(Row(("profile", "natural-variation"), ("metric", $"metric:{group.Key}:max"), ("value", max), ("status", "observed_range")));
            rows.Add(Row(("profile", "natural-variation"), ("metric", $"metric:{group.Key}:avg"), ("value", avg), ("status", "observed_range")));
            rows.Add(Row(("profile", "noise"), ("metric", $"metric:{group.Key}:avgAbsDeltaFromScenarioBase"), ("value", noiseActive ? avgAbsDelta : null), ("status", noiseActive ? "effect_estimated_without_truth_persistence" : "profile_inactive")));
        }

        return DiagnosticResult(
            definition,
            ["profile", "metric", "value", "status"],
            rows,
            ["Noise effect is estimated from persisted accepted readings because TruthSnapshot values are not persisted. Lag/delay is reported from persisted operational state; if the profile is active but no delayed state is persisted, the effect is below threshold or not materialized in storage."]);
    }

    private static Dictionary<string, double> ExtractSimulatorBaseValues(string? parametersJson)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return result;
        }

        using var document = JsonDocument.Parse(parametersJson);
        if (!document.RootElement.TryGetProperty("simulator_options", out var options) ||
            options.ValueKind != JsonValueKind.Object)
        {
            return result;
        }

        foreach (var name in new[] { "BaseTemperature", "BaseHumidity", "BaseWindSpeed" })
        {
            if (options.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number)
            {
                result[name] = value.GetDouble();
            }
        }

        return result;
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunCellContextAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["cellCode", "logicalDate", "dailyPrecipitationMm", "maxTemperatureC", "humidityPercent", "windMps", "droughtContext", "provenance", "parameterSetVersion"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var stateRows = await dbContext.DailyCellStates
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == latestRun.Id)
            .Select(entity => new
            {
                CellCode = entity.GridCell!.CellCode,
                entity.LogicalDate,
                entity.DailyPrecipitationMillimeters,
                entity.MaxTemperatureCelsius,
                entity.LatestHumidityPercent,
                entity.LatestWindSpeedMetersPerSecond,
                entity.DroughtContext,
                entity.Provenance,
                entity.CandidateParameterSetVersion
            })
            .ToListAsync(cancellationToken);
        var states = stateRows
            .OrderByDescending(entity => entity.LogicalDate)
            .ThenBy(entity => entity.CellCode)
            .Take(25)
            .ToArray();

        return DiagnosticResult(
            definition,
            ["cellCode", "logicalDate", "dailyPrecipitationMm", "maxTemperatureC", "humidityPercent", "windMps", "droughtContext", "provenance", "parameterSetVersion"],
            states.Select(entity => Row(
                ("cellCode", entity.CellCode),
                ("logicalDate", entity.LogicalDate),
                ("dailyPrecipitationMm", entity.DailyPrecipitationMillimeters),
                ("maxTemperatureC", entity.MaxTemperatureCelsius),
                ("humidityPercent", entity.LatestHumidityPercent),
                ("windMps", entity.LatestWindSpeedMetersPerSecond),
                ("droughtContext", entity.DroughtContext),
                ("provenance", entity.Provenance),
                ("parameterSetVersion", entity.CandidateParameterSetVersion))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunFwiInputCompletenessAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var latestRun = await FindLatestRunAsync(dbContext, areaCode, cancellationToken);
        if (latestRun is null)
        {
            return DiagnosticResult(definition, ["cellCode", "hasTemperature", "hasHumidity", "hasWind", "hasPrecipitation", "fwiStatus", "kbdiStatus", "limitations"], [], ["No simulation run is persisted for the selected scope."]);
        }

        var stateRows = await dbContext.DailyCellStates
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == latestRun.Id)
            .Select(entity => new
            {
                CellCode = entity.GridCell!.CellCode,
                entity.MaxTemperatureCelsius,
                entity.LatestHumidityPercent,
                entity.LatestWindSpeedMetersPerSecond,
                entity.DailyPrecipitationMillimeters,
                entity.FireWeatherCalculationStatus,
                entity.KbdiCalculationStatus,
                entity.FireWeatherLimitations,
                entity.KbdiLimitations,
                entity.LogicalDate
            })
            .ToListAsync(cancellationToken);
        var states = stateRows
            .OrderByDescending(entity => entity.LogicalDate)
            .ThenBy(entity => entity.CellCode)
            .Take(25)
            .ToArray();

        return DiagnosticResult(
            definition,
            ["cellCode", "hasTemperature", "hasHumidity", "hasWind", "hasPrecipitation", "fwiStatus", "kbdiStatus", "limitations"],
            states.Select(entity => Row(
                ("cellCode", entity.CellCode),
                ("hasTemperature", entity.MaxTemperatureCelsius.HasValue),
                ("hasHumidity", entity.LatestHumidityPercent.HasValue),
                ("hasWind", entity.LatestWindSpeedMetersPerSecond.HasValue),
                ("hasPrecipitation", entity.DailyPrecipitationMillimeters.HasValue),
                ("fwiStatus", entity.FireWeatherCalculationStatus),
                ("kbdiStatus", entity.KbdiCalculationStatus),
                ("limitations", string.Join("; ", new[] { entity.FireWeatherLimitations, entity.KbdiLimitations }.Where(value => !string.IsNullOrWhiteSpace(value)))))).ToArray(),
            ["Completeness is reported from persisted DailyCellState fields. Missing/Partial is explicit and does not imply official scientific validation."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticLatestRunCoverageFreshnessAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CellOperationalStates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rowItems = await query
            .Select(entity => new
            {
                AreaCode = entity.Area!.Code,
                CellCode = entity.GridCell!.CellCode,
                entity.CoverageStatus,
                entity.FreshnessStatus,
                entity.CarryForwardStatus,
                entity.SnapshotTimestamp,
                entity.UpdatedAt,
                entity.Summary
            })
            .ToListAsync(cancellationToken);
        var rows = rowItems
            .OrderByDescending(entity => entity.UpdatedAt)
            .Take(50)
            .ToArray();

        return DiagnosticResult(
            definition,
            ["areaCode", "cellCode", "coverageStatus", "freshnessStatus", "carryForwardStatus", "snapshotTimestamp", "updatedAt", "summary"],
            rows.Select(entity => Row(
                ("areaCode", entity.AreaCode),
                ("cellCode", entity.CellCode),
                ("coverageStatus", entity.CoverageStatus),
                ("freshnessStatus", entity.FreshnessStatus),
                ("carryForwardStatus", entity.CarryForwardStatus),
                ("snapshotTimestamp", entity.SnapshotTimestamp),
                ("updatedAt", entity.UpdatedAt),
                ("summary", entity.Summary))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticAreaOperationalStateAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AreaOperationalStates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .OrderByDescending(entity => entity.UpdatedAt)
            .Take(10)
            .Select(entity => new
            {
                AreaCode = entity.Area!.Code,
                entity.SimulationRunId,
                entity.SnapshotTimestamp,
                entity.AggregateRiskScore,
                entity.AggregateRiskLevel,
                entity.Severity,
                entity.CoverageStatus,
                entity.FreshnessStatus,
                entity.CarryForwardStatus,
                entity.AssessmentCount,
                entity.UpdatedAt,
                entity.Summary
            })
            .ToListAsync(cancellationToken);

        return DiagnosticResult(
            definition,
            ["areaCode", "simulationRunId", "snapshotTimestamp", "aggregateRiskScore", "aggregateRiskLevel", "severity", "coverageStatus", "freshnessStatus", "carryForwardStatus", "assessmentCount", "updatedAt", "summary"],
            rows.Select(entity => Row(
                ("areaCode", entity.AreaCode),
                ("simulationRunId", entity.SimulationRunId),
                ("snapshotTimestamp", entity.SnapshotTimestamp),
                ("aggregateRiskScore", entity.AggregateRiskScore),
                ("aggregateRiskLevel", entity.AggregateRiskLevel),
                ("severity", entity.Severity),
                ("coverageStatus", entity.CoverageStatus),
                ("freshnessStatus", entity.FreshnessStatus),
                ("carryForwardStatus", entity.CarryForwardStatus),
                ("assessmentCount", entity.AssessmentCount),
                ("updatedAt", entity.UpdatedAt),
                ("summary", entity.Summary))).ToArray(),
            ["Area Operational State uses persisted projections and may include carry-forward. It is not necessarily limited to the latest run."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticCellOperationalStatesAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CellOperationalStates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .OrderByDescending(entity => entity.UpdatedAt)
            .Take(25)
            .Select(entity => new
            {
                AreaCode = entity.Area!.Code,
                CellCode = entity.GridCell!.CellCode,
                entity.SensorId,
                SensorName = entity.SensorNode != null ? entity.SensorNode.Name : null,
                entity.SnapshotTimestamp,
                entity.RiskScore,
                entity.RiskLevel,
                entity.Severity,
                entity.CoverageStatus,
                entity.FreshnessStatus,
                entity.CarryForwardStatus,
                entity.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return DiagnosticResult(
            definition,
            ["areaCode", "cellCode", "sensorId", "sensorName", "snapshotTimestamp", "riskScore", "riskLevel", "severity", "coverageStatus", "freshnessStatus", "carryForwardStatus", "updatedAt"],
            rows.Select(entity => Row(
                ("areaCode", entity.AreaCode),
                ("cellCode", entity.CellCode),
                ("sensorId", entity.SensorId),
                ("sensorName", entity.SensorName),
                ("snapshotTimestamp", entity.SnapshotTimestamp),
                ("riskScore", entity.RiskScore),
                ("riskLevel", entity.RiskLevel),
                ("severity", entity.Severity),
                ("coverageStatus", entity.CoverageStatus),
                ("freshnessStatus", entity.FreshnessStatus),
                ("carryForwardStatus", entity.CarryForwardStatus),
                ("updatedAt", entity.UpdatedAt))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticActiveAlertsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AlertStates.AsNoTracking().Where(entity => entity.Status == "Open");
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .OrderByDescending(entity => entity.UpdatedAt)
            .Select(entity => new
            {
                entity.Id,
                AreaCode = entity.Area!.Code,
                entity.AlertCode,
                entity.Severity,
                entity.Status,
                entity.Message,
                entity.TriggeredAt,
                entity.UpdatedAt
            })
            .ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["id", "areaCode", "alertCode", "severity", "status", "alertState", "triggeredAt", "updatedAt", "message"],
            rows.Select(entity => Row(
                ("id", entity.Id),
                ("areaCode", entity.AreaCode),
                ("alertCode", entity.AlertCode),
                ("severity", entity.Severity),
                ("status", entity.Status),
                ("alertState", ParseAlertState(entity.Message)),
                ("triggeredAt", entity.TriggeredAt),
                ("updatedAt", entity.UpdatedAt),
                ("message", entity.Message))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticRecentAlertTransitionsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        DateTimeOffset recentSince,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AlertStates.AsNoTracking()
            .Where(entity => entity.UpdatedAt >= recentSince || entity.TriggeredAt >= recentSince || entity.ResolvedAt >= recentSince);
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .OrderByDescending(entity => entity.UpdatedAt)
            .Take(25)
            .Select(entity => new
            {
                entity.Id,
                AreaCode = entity.Area!.Code,
                entity.AlertCode,
                entity.Severity,
                entity.Status,
                entity.Message,
                entity.TriggeredAt,
                entity.UpdatedAt,
                entity.ResolvedAt
            })
            .ToListAsync(cancellationToken);
        return DiagnosticResult(
            definition,
            ["id", "areaCode", "alertCode", "severity", "status", "alertState", "triggeredAt", "updatedAt", "resolvedAt", "message"],
            rows.Select(entity => Row(
                ("id", entity.Id),
                ("areaCode", entity.AreaCode),
                ("alertCode", entity.AlertCode),
                ("severity", entity.Severity),
                ("status", entity.Status),
                ("alertState", ParseAlertState(entity.Message)),
                ("triggeredAt", entity.TriggeredAt),
                ("updatedAt", entity.UpdatedAt),
                ("resolvedAt", entity.ResolvedAt),
                ("message", entity.Message))).ToArray());
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticScenarioDefinitionDetailsAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        string? scenarioCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ScenarioDefinitions.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        if (!string.IsNullOrWhiteSpace(scenarioCode))
        {
            query = query.Where(entity => entity.Code == scenarioCode);
        }

        var scenarios = await query
            .OrderBy(entity => entity.Code)
            .Select(entity => new
            {
                AreaCode = entity.Area!.Code,
                entity.Code,
                entity.Name,
                Category = entity.ScenarioKind.ToString(),
                entity.Description,
                entity.ParametersJson,
                BaseScenarioCode = entity.BaseScenario == null ? null : entity.BaseScenario.Code
            })
            .ToListAsync(cancellationToken);

        var rows = scenarios.Select(entity =>
        {
            var flags = InspectScenarioParameters(entity.ParametersJson);
            return Row(
                ("areaCode", entity.AreaCode),
                ("code", entity.Code),
                ("name", entity.Name),
                ("category", entity.Category),
                ("baseScenarioCode", entity.BaseScenarioCode),
                ("parametersSummary", SummarizeScenarioParameters(entity.ParametersJson)),
                ("simulatorOptions", flags.SimulatorOptionsSummary),
                ("hasSimulatorOptions", flags.HasSimulatorOptions),
                ("hasDegradationParameters", flags.HasDegradationParameters),
                ("hasWeatherParameters", flags.HasWeatherParameters),
                ("likelyEquivalentTo", flags.LikelyEquivalentTo),
                ("notes", flags.Notes));
        }).ToArray();

        return DiagnosticResult(
            definition,
            ["areaCode", "code", "name", "category", "baseScenarioCode", "parametersSummary", "simulatorOptions", "hasSimulatorOptions", "hasDegradationParameters", "hasWeatherParameters", "likelyEquivalentTo", "notes"],
            rows,
            ["This diagnostic reads control.scenario_definitions only. It does not execute simulator logic or infer runtime degradation from produced events."]);
    }

    private static async Task<RuntimeDiagnosticResultResponse> DiagnosticCompareLatestBvsCAsync(
        NatureProtectorControlDbContext dbContext,
        RuntimeDiagnosticDefinitionResponse definition,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var runB = await FindLatestRunByScenarioAsync(dbContext, areaCode, "scenario_b", cancellationToken);
        var runC = await FindLatestRunByScenarioAsync(dbContext, areaCode, "scenario_c", cancellationToken);
        var rows = new List<IReadOnlyDictionary<string, string?>>();
        var limitations = new List<string>
        {
            "Comparison uses persisted rows only and does not recalculate risk or alert state.",
            "Area operational state is current projection state and may include carry-forward; it is not a per-run-only aggregate."
        };

        if (runB is null)
        {
            limitations.Add("No scenario_b run is persisted for the selected area.");
        }

        if (runC is null)
        {
            limitations.Add("No scenario_c run is persisted for the selected area.");
        }

        if (runB is not null)
        {
            rows.AddRange(await BuildRunComparisonRowsAsync(dbContext, "scenario_b", runB, cancellationToken));
        }

        if (runC is not null)
        {
            rows.AddRange(await BuildRunComparisonRowsAsync(dbContext, "scenario_c", runC, cancellationToken));
        }

        return DiagnosticResult(
            definition,
            ["scenario", "metric", "value"],
            rows,
            limitations);
    }

    private static async Task<RuntimeFreshnessSummaryResponse?> BuildFreshnessSummaryAsync(
        NatureProtectorControlDbContext dbContext,
        string? areaCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        const int freshSeconds = 120;
        const int staleSeconds = 300;

        var query = dbContext.CellOperationalStates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .Select(entity => new { entity.SnapshotTimestamp, entity.UpdatedAt })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new RuntimeFreshnessSummaryResponse(
                0,
                0,
                0,
                null,
                null,
                freshSeconds,
                staleSeconds,
                "No cell operational states are persisted for this scope. Candidate Parameter Set only.");
        }

        var ages = rows.Select(entity => new
        {
            entity.SnapshotTimestamp,
            AgeSeconds = Math.Max(0, (now - entity.UpdatedAt).TotalSeconds)
        }).ToArray();

        return new RuntimeFreshnessSummaryResponse(
            ages.Count(entity => entity.AgeSeconds <= freshSeconds),
            ages.Count(entity => entity.AgeSeconds > freshSeconds && entity.AgeSeconds <= staleSeconds),
            ages.Count(entity => entity.AgeSeconds > staleSeconds),
            rows.Min(entity => entity.SnapshotTimestamp),
            rows.Max(entity => entity.SnapshotTimestamp),
            freshSeconds,
            staleSeconds,
            "Freshness uses cell operational state UpdatedAt with Candidate Parameter Set thresholds; it does not alter scoring or alert policy.");
    }

    private static async Task<SimulationRunResponse?> FindLatestRunAsync(
        NatureProtectorControlDbContext dbContext,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SimulationRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .Select(entity => new SimulationRunResponse(
                entity.Id,
                entity.Area!.Code,
                entity.ScenarioCode,
                entity.ScenarioName,
                entity.Status.ToString(),
                entity.ConfigurationVersion!.VersionNumber,
                entity.CreatedAt,
                entity.StartedAt,
                entity.EndedAt,
                entity.LogicalStartTimestamp,
                entity.IntervalSeconds,
                entity.NumberOfCycles,
                entity.ExecutionSeed,
                entity.MetadataJson))
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();
    }

    private static async Task<SimulationRunResponse?> FindLatestRunByScenarioAsync(
        NatureProtectorControlDbContext dbContext,
        string? areaCode,
        string scenarioCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SimulationRuns.AsNoTracking()
            .Where(entity => entity.ScenarioCode == scenarioCode);
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var rows = await query
            .Select(entity => new SimulationRunResponse(
                entity.Id,
                entity.Area!.Code,
                entity.ScenarioCode,
                entity.ScenarioName,
                entity.Status.ToString(),
                entity.ConfigurationVersion!.VersionNumber,
                entity.CreatedAt,
                entity.StartedAt,
                entity.EndedAt,
                entity.LogicalStartTimestamp,
                entity.IntervalSeconds,
                entity.NumberOfCycles,
                entity.ExecutionSeed,
                entity.MetadataJson))
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> BuildRunComparisonRowsAsync(
        NatureProtectorControlDbContext dbContext,
        string label,
        SimulationRunResponse run,
        CancellationToken cancellationToken)
    {
        var readings = await GetAcceptedReadingsForRunAsync(dbContext, run.Id, cancellationToken);
        var sourceEventIds = readings.Select(entity => entity.EventId).ToHashSet();
        var risks = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => sourceEventIds.Contains(entity.SourceEventId))
            .ToListAsync(cancellationToken);
        var rejectedCount = await dbContext.RejectedEvents
            .AsNoTracking()
            .CountAsync(entity => entity.InboxEvent != null && entity.InboxEvent.AreaId == readings.Select(reading => reading.AreaId).FirstOrDefault(), cancellationToken);
        var quarantinedCount = await dbContext.QuarantinedEvents
            .AsNoTracking()
            .CountAsync(entity => entity.InboxEvent != null && entity.InboxEvent.AreaId == readings.Select(reading => reading.AreaId).FirstOrDefault(), cancellationToken);

        var expected = TryGetResolvedSensorCount(run.MetadataJson) is { } sensors
            ? sensors * run.NumberOfCycles
            : (int?)null;
        var overrides = ExtractOverridesSummary(run.MetadataJson);
        var profiles = ExtractDegradationProfiles(run.MetadataJson);
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            Row(("scenario", label), ("metric", "simulationRunId"), ("value", run.Id)),
            Row(("scenario", label), ("metric", "status"), ("value", run.Status)),
            Row(("scenario", label), ("metric", "requested/resolved overrides"), ("value", overrides)),
            Row(("scenario", label), ("metric", "degradation profiles"), ("value", profiles.Count == 0 ? "not exposed" : string.Join(",", profiles))),
            Row(("scenario", label), ("metric", "expected events"), ("value", expected)),
            Row(("scenario", label), ("metric", "observed accepted readings"), ("value", readings.Count)),
            Row(("scenario", label), ("metric", "missing events"), ("value", expected.HasValue ? Math.Max(0, expected.Value - readings.Count) : null)),
            Row(("scenario", label), ("metric", "risk assessments"), ("value", risks.Count)),
            Row(("scenario", label), ("metric", "risk min/max/avg"), ("value", risks.Count == 0 ? null : $"{risks.Min(item => item.RiskScore):0.###}/{risks.Max(item => item.RiskScore):0.###}/{risks.Average(item => item.RiskScore):0.###}")),
            Row(("scenario", label), ("metric", "rejected count for area"), ("value", rejectedCount)),
            Row(("scenario", label), ("metric", "quarantined count for area"), ("value", quarantinedCount))
        };

        foreach (var metricGroup in risks
            .Join(readings, risk => risk.SourceEventId, reading => reading.EventId, (risk, reading) => new { risk, reading })
            .GroupBy(item => item.reading.MetricType)
            .OrderBy(group => group.Key))
        {
            rows.Add(Row(
                ("scenario", label),
                ("metric", $"metric {metricGroup.Key} count/min/max/avg score"),
                ("value", $"{metricGroup.Count()}/{metricGroup.Min(item => item.risk.RiskScore):0.###}/{metricGroup.Max(item => item.risk.RiskScore):0.###}/{metricGroup.Average(item => item.risk.RiskScore):0.###}")));
        }

        return rows;
    }

    private static async Task<SimulationRunResponse?> FindRuntimeRunByCorrelationAsync(
        NatureProtectorControlDbContext dbContext,
        string areaCode,
        string scenarioCode,
        DateTimeOffset createdAfterUtc,
        string orchestratorCorrelationId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.SimulationRuns
            .AsNoTracking()
            .Where(entity =>
                entity.Area!.Code == areaCode &&
                entity.ScenarioCode == scenarioCode &&
                entity.CreatedAt >= createdAfterUtc)
            .Select(entity => new SimulationRunResponse(
                entity.Id,
                entity.Area!.Code,
                entity.ScenarioCode,
                entity.ScenarioName,
                entity.Status.ToString(),
                entity.ConfigurationVersion!.VersionNumber,
                entity.CreatedAt,
                entity.StartedAt,
                entity.EndedAt,
                entity.LogicalStartTimestamp,
                entity.IntervalSeconds,
                entity.NumberOfCycles,
                entity.ExecutionSeed,
                entity.MetadataJson))
            .ToListAsync(cancellationToken);

        return rows
            .Where(entity => !string.IsNullOrWhiteSpace(entity.MetadataJson) &&
                entity.MetadataJson.Contains(orchestratorCorrelationId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstOrDefault()
            ?? rows.OrderByDescending(entity => entity.CreatedAt).FirstOrDefault();
    }

    private static async Task<int> CountAcceptedReadingsForRunAsync(
        NatureProtectorControlDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var readings = await GetAcceptedReadingsForRunAsync(dbContext, runId, cancellationToken);
        return readings.Count;
    }

    private static async Task<IReadOnlyList<AcceptedReadingLogRecord>> GetAcceptedReadingsForRunAsync(
        NatureProtectorControlDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AcceptedReadingLogs.AsNoTracking().ToListAsync(cancellationToken);
        return rows
            .Where(entity => TryGetSimulationRunId(entity.PayloadJson) == runId)
            .OrderBy(entity => entity.EventTime)
            .ToArray();
    }

    private static async Task<IReadOnlyList<RiskAssessmentLogRecord>> GetRiskAssessmentsForRunAsync(
        NatureProtectorControlDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var direct = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

        if (direct.Count > 0)
        {
            return direct;
        }

        var readings = await GetAcceptedReadingsForRunAsync(dbContext, runId, cancellationToken);
        var sourceEventIds = readings.Select(entity => entity.EventId).ToHashSet();
        return await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => sourceEventIds.Contains(entity.SourceEventId))
            .ToListAsync(cancellationToken);
    }

    private static async Task<DailyCellStateRecord?> GetLatestDailyCellStateForRunAsync(
        NatureProtectorControlDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.DailyCellStates
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(entity => entity.LogicalDate)
            .ThenByDescending(entity => entity.UpdatedAt)
            .FirstOrDefault();
    }

    private static IReadOnlyList<RuntimeStatusCountResponse> BuildQualityFlagSummary(
        IReadOnlyCollection<AcceptedReadingLogRecord> readings,
        int? expectedEvents)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var reading in readings)
        {
            if (string.Equals(reading.OperationalState, "Delayed", StringComparison.Ordinal))
            {
                Increment(counts, "Delayed");
            }
            else if (string.Equals(reading.OperationalState, "Retransmitted", StringComparison.Ordinal))
            {
                Increment(counts, "Duplicate");
            }
            else if (string.Equals(reading.OperationalState, "Dropped", StringComparison.Ordinal))
            {
                Increment(counts, "Dropped");
            }
        }

        if (expectedEvents.HasValue)
        {
            var missing = Math.Max(0, expectedEvents.Value - readings.Count);
            if (missing > 0)
            {
                counts["Missing"] = missing;
            }
        }

        return counts
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new RuntimeStatusCountResponse(item.Key, item.Value))
            .ToArray();
    }

    private static IReadOnlyList<RuntimeStatusCountResponse> BuildEligibilitySummary(
        int acceptedReadingCount,
        IReadOnlyCollection<RiskAssessmentLogRecord> riskAssessments)
    {
        var counts = riskAssessments
            .GroupBy(entity => ExtractInputStatus(entity.ExplanationSummary))
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var nonScoredAcceptedReadings = Math.Max(0, acceptedReadingCount - riskAssessments.Count);
        if (nonScoredAcceptedReadings > 0)
        {
            counts["BlockedOrMissingRisk"] = nonScoredAcceptedReadings;
        }

        return counts
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new RuntimeStatusCountResponse(item.Key, item.Value))
            .ToArray();
    }

    private static string ExtractInputStatus(string? explanationSummary)
    {
        if (string.IsNullOrWhiteSpace(explanationSummary))
        {
            return "Unknown";
        }

        var marker = "InputStatus=";
        var markerIndex = explanationSummary.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return "Unknown";
        }

        var start = markerIndex + marker.Length;
        var end = explanationSummary.IndexOf(';', start);
        return (end < 0 ? explanationSummary[start..] : explanationSummary[start..end]).Trim();
    }

    private static void Increment(Dictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out var current) ? current + 1 : 1;
    }

    private static Guid? TryGetSimulationRunId(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            foreach (var propertyName in new[] { "SimulationRunId", "simulationRunId", "simulation_run_id" })
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(property.GetString(), out var value))
                {
                    return value;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static int? TryGetResolvedSensorCount(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            if (root.TryGetProperty("run_overrides", out var overrides) &&
                overrides.TryGetProperty("resolved", out var resolved) &&
                GetIntProperty(resolved, "sensor_count") is { } resolvedSensorCount)
            {
                return resolvedSensorCount;
            }

            if (GetIntProperty(root, "sensor_count") is { } sensorCount)
            {
                return sensorCount;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string SummarizeMetadata(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return "empty";
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            var parts = new List<string>();
            if (GetIntProperty(root, "sensor_count") is { } sensorCount)
            {
                parts.Add($"sensor_count={sensorCount}");
            }

            var scenarioCategory = GetStringProperty(root, "scenario_category");
            if (!string.IsNullOrWhiteSpace(scenarioCategory))
            {
                parts.Add($"scenario_category={scenarioCategory}");
            }

            var correlation = GetStringProperty(root, "orchestrator_correlation_id");
            if (!string.IsNullOrWhiteSpace(correlation))
            {
                parts.Add($"orchestrator_correlation_id={correlation}");
            }

            return parts.Count == 0 ? "valid JSON" : string.Join("; ", parts);
        }
        catch (JsonException)
        {
            return "invalid JSON";
        }
    }

    private static string SummarizeScenarioParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return "empty";
        }

        try
        {
            using var document = JsonDocument.Parse(parametersJson);
            var root = document.RootElement;
            var parts = new List<string>();
            if (root.TryGetProperty("simulator_options", out var simulatorOptions) &&
                simulatorOptions.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in new[] { "BaseTemperature", "BaseHumidity", "BaseWindSpeed", "FailureRate", "NoiseLevel", "NumberOfCycles", "IntervalSeconds" })
                {
                    if (simulatorOptions.TryGetProperty(name, out var value))
                    {
                        parts.Add($"{name}={value}");
                    }
                }
            }

            return parts.Count == 0 ? "valid JSON; no common simulator option summary fields found" : string.Join("; ", parts);
        }
        catch (JsonException)
        {
            return "invalid JSON";
        }
    }

    private static ScenarioParameterInspection InspectScenarioParameters(string? parametersJson)
    {
        if (string.IsNullOrWhiteSpace(parametersJson))
        {
            return new(false, false, false, "none", "unknown", "ParametersJson is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(parametersJson);
            var root = document.RootElement;
            var raw = parametersJson.ToLowerInvariant();
            var hasSimulatorOptions = root.TryGetProperty("simulator_options", out var simulatorOptions) &&
                                      simulatorOptions.ValueKind == JsonValueKind.Object;
            var simulatorOptionsSummary = hasSimulatorOptions
                ? SummarizeScenarioParameters(parametersJson)
                : "missing";
            var hasDegradation = raw.Contains("degradation", StringComparison.OrdinalIgnoreCase) ||
                                 raw.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                                 raw.Contains("stale", StringComparison.OrdinalIgnoreCase) ||
                                 raw.Contains("fault", StringComparison.OrdinalIgnoreCase);
            var hasWeather = raw.Contains("temperature", StringComparison.OrdinalIgnoreCase) ||
                             raw.Contains("humidity", StringComparison.OrdinalIgnoreCase) ||
                             raw.Contains("wind", StringComparison.OrdinalIgnoreCase);
            var notes = hasDegradation
                ? "Scenario parameters include degradation/fault-like keys."
                : "No degradation/fault-like parameter keys found in ParametersJson.";

            return new(
                hasSimulatorOptions,
                hasDegradation,
                hasWeather,
                simulatorOptionsSummary,
                "unknown",
                notes);
        }
        catch (JsonException)
        {
            return new(false, false, false, "invalid JSON", "unknown", "ParametersJson is invalid JSON.");
        }
    }

    private static string ExtractOverridesSummary(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return "empty";
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("run_overrides", out var overrides))
            {
                return SummarizeMetadata(metadataJson);
            }

            return Regex.Replace(overrides.GetRawText(), "\\s+", " ");
        }
        catch (JsonException)
        {
            return "invalid JSON";
        }
    }

    private static IReadOnlyList<string> ExtractDegradationProfiles(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            if (root.TryGetProperty("run_overrides", out var overrides) &&
                overrides.TryGetProperty("resolved", out var resolved) &&
                resolved.ValueKind == JsonValueKind.Object)
            {
                var resolvedValues = ReadOverrideValues(resolved);
                if (resolvedValues?.DegradationProfiles is { Count: > 0 } resolvedProfiles)
                {
                    return resolvedProfiles;
                }

                if (!string.IsNullOrWhiteSpace(resolvedValues?.DegradationProfile))
                {
                    return [resolvedValues.DegradationProfile];
                }
            }

            if (root.TryGetProperty("degradation_profiles", out var profilesElement))
            {
                return ReadStringArray(profilesElement);
            }

            var profile = GetStringProperty(root, "degradation_profile");
            return string.IsNullOrWhiteSpace(profile) ? [] : [profile];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record ScenarioParameterInspection(
        bool HasSimulatorOptions,
        bool HasDegradationParameters,
        bool HasWeatherParameters,
        string SimulatorOptionsSummary,
        string LikelyEquivalentTo,
        string Notes);

    private static bool IsControlledValidationEnvironmentAllowed(string? environmentName)
        => string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(environmentName, "Evidence", StringComparison.OrdinalIgnoreCase);

    // </phase5-slice>

}
