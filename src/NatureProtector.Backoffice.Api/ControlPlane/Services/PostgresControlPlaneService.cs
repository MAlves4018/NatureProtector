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

/*
 * Este serviço projeta para a API os dados persistidos no control plane e nas
 * projeções operacionais.
 *
 * Rationale:
 * - Os controladores não devem conter queries nem detalhe relacional.
 * - A camada de serviço define a fronteira entre o esquema persistido e os
 *   contratos de resposta do backoffice.
 *
 * Design considerations:
 * - As queries usam projeções diretas para evitar transportar entidades
 *   completas para a API.
 * - Quando a versão de configuração não é indicada, usa-se a configuração ativa
 *   mais recente.
 * - A paginação aplica limites defensivos para proteger a API.
 */

public sealed class PostgresControlPlaneService : IControlPlaneService
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const int DefaultRecentMinutes = 30;
    private const int MinRecentMinutes = 1;
    private const int MaxRecentMinutes = 24 * 60;

    private readonly IDbContextFactory<NatureProtectorControlDbContext> _dbContextFactory;
    private readonly string _repositoryRoot;
    private readonly bool _enableRuntimeProcessLaunch;

    private static readonly IReadOnlyList<RuntimeDiagnosticDefinitionResponse> RuntimeDiagnostics =
    [
        new("runtime-table-counts", "Runtime table counts", "Counts rows in runtime/control, pipeline and projection tables."),
        new("active-runs", "Active runs", "Lists simulation runs with no EndedAt timestamp."),
        new("latest-runs", "Latest runs", "Lists the latest 10 simulation runs."),
        new("inbox-by-status", "Inbox by status", "Groups pipeline.event_inbox by Status."),
        new("attempts-by-outcome", "Attempts by outcome", "Groups pipeline.processing_attempts by Outcome."),
        new("failed-attempts-by-error", "Failed attempts by error", "Groups recent failed/retry/quarantined attempts by outcome and error."),
        new("latest-rejected-events", "Latest rejected events", "Lists latest rejected events."),
        new("latest-quarantined-events", "Latest quarantined events", "Lists latest quarantined events."),
        new("latest-run-expected-vs-observed", "Latest run expected vs observed", "Compares latest run expected event count with accepted readings observed for that run."),
        new("latest-run-events-by-cycle", "Latest run events by cycle", "Groups latest run accepted readings by logical cycle."),
        new("latest-run-risk-by-metric", "Latest run risk by metric", "Groups latest run persisted risk assessments by metric type."),
        new("area-operational-state", "Area operational state", "Shows the current persisted area operational projection."),
        new("cell-operational-states", "Cell operational states", "Shows recent persisted cell operational projections."),
        new("active-alerts", "Active alerts", "Lists open alerts."),
        new("recent-alert-transitions", "Recent alert transitions", "Lists recent alert rows including resolved alerts."),
        new("scenario-definition-details", "Scenario definition details", "Shows persisted scenario parameters and simulator options for the selected area."),
        new("compare-latest-b-vs-c", "Compare latest B vs C", "Compares latest scenario_b and scenario_c runs for the selected area using persisted runtime data.")
    ];

    public PostgresControlPlaneService(
        IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
        string? contentRootPath = null,
        bool enableRuntimeProcessLaunch = false)
    {
        _dbContextFactory = dbContextFactory;
        _repositoryRoot = ResolveRepositoryRoot(contentRootPath ?? AppContext.BaseDirectory);
        _enableRuntimeProcessLaunch = enableRuntimeProcessLaunch;
    }

    public async Task<RuntimeRunTimingSummaryResponse?> GetRuntimeRunTimingsAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.SimulationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var inboxEvents = await dbContext.InboxEvents
            .Include(entity => entity.Attempts)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var runInboxEvents = inboxEvents
            .Where(entity =>
                TryGetSimulationRunId(entity.PayloadJson) == runId ||
                TryGetSimulationRunId(entity.EnvelopeJson) == runId)
            .ToArray();
        var attempts = runInboxEvents
            .SelectMany(entity => entity.Attempts)
            .OrderBy(entity => entity.StartedAt)
            .ToArray();

        var riskAssessments = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

        var alerts = await dbContext.AlertStates
            .AsNoTracking()
            .Where(entity => entity.AreaOperationalState!.SimulationRunId == runId)
            .ToListAsync(cancellationToken);

        var limitations = new List<string>
        {
            "Logger stopwatch timings are emitted in logs but are not structurally associated with SimulationRunId yet."
        };
        if (runInboxEvents.Length == 0)
        {
            limitations.Add("No pipeline.event_inbox rows were associated with this SimulationRunId.");
        }

        if (attempts.Length == 0)
        {
            limitations.Add("No pipeline.processing_attempts rows were associated with this SimulationRunId.");
        }

        if (riskAssessments.Count == 0)
        {
            limitations.Add("No projection.risk_assessment_log rows were associated with this SimulationRunId.");
        }

        if (alerts.Count == 0)
        {
            limitations.Add("No projection.alert_state rows were associated with this SimulationRunId.");
        }

        DateTimeOffset? firstInboxReceivedAt = runInboxEvents.Length == 0 ? null : runInboxEvents.Min(entity => entity.ReceivedAt);
        DateTimeOffset? firstProcessingAttemptStartedAt = attempts.Length == 0 ? null : attempts.Min(entity => entity.StartedAt);
        var lastProcessingAttemptFinishedAt = MaxFinishedAt(attempts);
        DateTimeOffset? firstRiskAssessmentCreatedAt = riskAssessments.Count == 0 ? null : riskAssessments.Min(entity => entity.CreatedAt);
        DateTimeOffset? firstAlertTriggeredAt = alerts.Count == 0 ? null : alerts.Min(entity => entity.TriggeredAt);
        var attemptDurations = attempts
            .Select(CalculateDurationMilliseconds)
            .Where(duration => duration.HasValue)
            .Select(duration => duration!.Value)
            .ToArray();

        var stages = attempts
            .GroupBy(entity => new
            {
                Stage = string.IsNullOrWhiteSpace(entity.Stage) ? "Unknown" : entity.Stage,
                Outcome = entity.Outcome.ToString(),
                entity.ErrorCode
            })
            .OrderBy(group => group.Key.Stage, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Outcome, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ErrorCode, StringComparer.Ordinal)
            .Select(group =>
            {
                var rows = group.ToArray();
                var durations = rows
                    .Select(CalculateDurationMilliseconds)
                    .Where(duration => duration.HasValue)
                    .Select(duration => duration!.Value)
                    .ToArray();

                return new RuntimeStageTimingSummaryResponse(
                    group.Key.Stage,
                    group.Key.Outcome,
                    group.Key.ErrorCode,
                    rows.Length,
                    rows.Min(entity => entity.StartedAt),
                    MaxFinishedAt(rows),
                    durations.Length == 0 ? null : durations.Min(),
                    durations.Length == 0 ? null : durations.Average(),
                    durations.Length == 0 ? null : durations.Max());
            })
            .ToArray();

        return new RuntimeRunTimingSummaryResponse(
            run.Id,
            CalculateDurationMilliseconds(run.StartedAt, run.EndedAt),
            run.StartedAt,
            run.EndedAt,
            firstInboxReceivedAt,
            firstProcessingAttemptStartedAt,
            lastProcessingAttemptFinishedAt,
            firstRiskAssessmentCreatedAt,
            firstAlertTriggeredAt,
            CalculateDurationMilliseconds(run.StartedAt, firstInboxReceivedAt),
            CalculateDurationMilliseconds(run.StartedAt, firstProcessingAttemptStartedAt),
            CalculateDurationMilliseconds(run.StartedAt, firstRiskAssessmentCreatedAt),
            CalculateDurationMilliseconds(run.StartedAt, firstAlertTriggeredAt),
            new RuntimeAttemptTimingSummaryResponse(
                attempts.Length,
                attempts.Count(entity => entity.Outcome == ProcessingAttemptOutcome.Succeeded),
                attempts.Count(entity =>
                    entity.Outcome == ProcessingAttemptOutcome.Failed ||
                    entity.Outcome == ProcessingAttemptOutcome.RetryScheduled),
                attempts.Count(entity => entity.Outcome == ProcessingAttemptOutcome.Quarantined),
                attemptDurations.Length == 0 ? null : attemptDurations.Min(),
                attemptDurations.Length == 0 ? null : attemptDurations.Average(),
                attemptDurations.Length == 0 ? null : attemptDurations.Max()),
            stages,
            limitations);
    }

    /// <summary>
    /// Indica que a implementação PostgreSQL do control plane está disponível.
    /// </summary>
    public bool IsAvailable => true;

    /// <summary>
    /// Mensagem curta de disponibilidade exposta pelos endpoints da API.
    /// </summary>
    public string AvailabilityMessage => "PostgreSQL-backed control plane is available.";

    /// <summary>
    /// Lista as versões de configuração conhecidas e respetivos contadores
    /// agregados.
    /// </summary>
    public async Task<IReadOnlyList<ConfigurationVersionResponse>> ListConfigurationsAsync(CancellationToken cancellationToken)
    {
        using var activity = BackofficeApiTelemetry.ActivitySource.StartActivity("natureprotector.backoffice.list_configurations");
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await dbContext.ConfigurationVersions
            .AsNoTracking()
            .OrderByDescending(entity => entity.VersionNumber)
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
            .ToListAsync(cancellationToken);
        stopwatch.Stop();
        BackofficeApiTelemetry.Requests.Add(1, new TagList { { TelemetryTags.Operation, "list_configurations" } });
        BackofficeApiTelemetry.QueryDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Operation, "list_configurations" } });
        return result;
    }

    /// <summary>
    /// Obtém a configuração atualmente marcada como ativa.
    /// </summary>
    public async Task<ConfigurationVersionResponse?> GetActiveConfigurationAsync(CancellationToken cancellationToken)
    {
        using var activity = BackofficeApiTelemetry.ActivitySource.StartActivity("natureprotector.backoffice.get_active_configuration");
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var result = await ProjectConfigurationAsync(
            dbContext,
            dbContext.ConfigurationVersions
                .AsNoTracking()
                .Where(entity => entity.IsActive)
                .OrderByDescending(entity => entity.VersionNumber),
            cancellationToken);
        stopwatch.Stop();
        BackofficeApiTelemetry.Requests.Add(1, new TagList { { TelemetryTags.Operation, "get_active_configuration" } });
        BackofficeApiTelemetry.QueryDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Operation, "get_active_configuration" } });
        return result;
    }

    /// <summary>
    /// Ativa explicitamente uma versão de configuração.
    /// </summary>
    public async Task<ConfigurationVersionResponse?> ActivateConfigurationAsync(int versionNumber, CancellationToken cancellationToken)
    {
        using var activity = BackofficeApiTelemetry.ActivitySource.StartActivity("natureprotector.backoffice.activate_configuration");
        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var target = await dbContext.ConfigurationVersions
            .SingleOrDefaultAsync(entity => entity.VersionNumber == versionNumber, cancellationToken);

        if (target is null)
        {
            return null;
        }

        var versions = await dbContext.ConfigurationVersions.ToListAsync(cancellationToken);

        foreach (var configurationVersion in versions)
        {
            configurationVersion.IsActive = configurationVersion.Id == target.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var result = await ProjectConfigurationAsync(
            dbContext,
            dbContext.ConfigurationVersions
                .AsNoTracking()
                .Where(entity => entity.Id == target.Id),
            cancellationToken);
        stopwatch.Stop();
        BackofficeApiTelemetry.Requests.Add(1, new TagList { { TelemetryTags.Operation, "activate_configuration" } });
        BackofficeApiTelemetry.QueryDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { TelemetryTags.Operation, "activate_configuration" } });
        return result;
    }

    /// <summary>
    /// Lista as áreas da versão de configuração resolvida.
    /// </summary>
    public async Task<IReadOnlyList<AreaSummaryResponse>> ListAreasAsync(int? configurationVersion, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return [];
        }

        return await dbContext.Areas
            .AsNoTracking()
            .Where(entity => entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .OrderBy(entity => entity.Name)
            .Select(entity => new AreaSummaryResponse(
		        entity.Id,
                entity.Code,
                entity.Name,
                entity.CountryCode,
                entity.ConfigurationVersion!.VersionNumber,
                dbContext.GridCells.Count(cell => cell.AreaId == entity.Id),
                dbContext.SensorNodes.Count(node => node.AreaId == entity.Id && node.IsActive),
                dbContext.ScenarioDefinitions.Count(scenario => scenario.AreaId == entity.Id)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Obtém o detalhe de uma área concreta.
    /// </summary>
    public async Task<AreaDetailResponse?> GetAreaAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return null;
        }

        return await dbContext.Areas
            .AsNoTracking()
            .Where(entity =>
                entity.Code == areaCode &&
                entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .Select(entity => new AreaDetailResponse(
                entity.Code,
                entity.Name,
                entity.CountryCode,
                entity.ConfigurationVersion!.VersionNumber,
                entity.GeometryGeoJson,
                entity.MetadataJson,
                entity.Context == null
                    ? null
                    : new AreaContextResponse(
                        entity.Context.VegetationType,
                        entity.Context.VegetationDensity,
                        entity.Context.PopulationExposure,
                        entity.Context.CriticalInfrastructureExposure,
                        entity.Context.Seasonality),
                dbContext.GridCells.Count(cell => cell.AreaId == entity.Id),
                dbContext.SensorNodes.Count(node => node.AreaId == entity.Id && node.IsActive),
                dbContext.ScenarioDefinitions.Count(scenario => scenario.AreaId == entity.Id)))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AreaGeoJSONResponse?> GetAreaGeoJSONAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return null;
        }

        return await dbContext.Areas
            .AsNoTracking()
            .Where(entity =>
                entity.Code == areaCode &&
                entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .Select(entity => new AreaGeoJSONResponse(
                entity.Id,
                entity.GeometryGeoJson))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lista células da grelha de uma área com paginação defensiva.
    /// </summary>
    public async Task<IReadOnlyList<GridCellResponse>> ListGridCellsAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return [];
        }

        var normalizedSkip = NormalizeSkip(skip);
        var normalizedTake = NormalizeTake(take);

        return await dbContext.GridCells
            .AsNoTracking()
            .Where(entity =>
                entity.Area!.Code == areaCode &&
                entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .OrderBy(entity => entity.CellCode)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .Select(entity => new GridCellResponse(
                entity.CellCode,
                dbContext.SensorNodes
                    .Where(node => node.GridCellId == entity.Id && node.IsActive)
                    .Select(node => Tuple.Create(node.Id, node.Type.ToString()))
                    .ToArray(),
                entity.ConfigurationVersion!.VersionNumber,
                entity.CentroidLatitude,
                entity.CentroidLongitude,
                entity.AltitudeMeters,
                entity.SlopeDegrees,
                entity.AspectDegrees,
                entity.LandCoverClass,
                entity.DominantForestType,
                entity.DominantFuelModel,
                entity.TreeCoverDensity,
                entity.StructuralHazard,
                entity.ConjuncturalHazard,
                dbContext.SensorNodes.Count(node => node.GridCellId == entity.Id && node.IsActive)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lista os sensores configurados para uma área.
    /// </summary>
    public async Task<IReadOnlyList<SensorNodeResponse>> ListSensorNodesAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return [];
        }

        var normalizedSkip = NormalizeSkip(skip);
        var normalizedTake = NormalizeTake(take);

        return await dbContext.SensorNodes
            .AsNoTracking()
            .Where(entity =>
                entity.Area!.Code == areaCode &&
                entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .OrderBy(entity => entity.Name)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .Select(entity => new SensorNodeResponse(
                entity.Id,
                entity.Name,
                entity.Type.ToString(),
                entity.ConfigurationVersion!.VersionNumber,
                entity.GridCell!.CellCode,
                entity.Profile!.Name,
                entity.Profile.SensorFamily,
                entity.Network != null ? entity.Network.Name : null,
                entity.Latitude,
                entity.Longitude,
                entity.AltitudeMeters,
                entity.IsActive,
                entity.InstallationProfile))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lista os cenários conhecidos para uma área.
    /// </summary>
    public async Task<IReadOnlyList<ScenarioResponse>> ListScenariosAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return [];
        }

        return await dbContext.ScenarioDefinitions
            .AsNoTracking()
            .Where(entity =>
                entity.Area!.Code == areaCode &&
                entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .OrderBy(entity => entity.Code)
            .Select(entity => new ScenarioResponse(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.ScenarioKind.ToString(),
                entity.ConfigurationVersion!.VersionNumber,
                entity.Description,
                entity.BaseScenarioId == null
                    ? null
                    : dbContext.ScenarioDefinitions
                        .Where(baseScenario => baseScenario.Id == entity.BaseScenarioId)
                        .Select(baseScenario => baseScenario.Code)
                        .SingleOrDefault(),
                dbContext.ScenarioDatasetBindings.Count(binding => binding.ScenarioId == entity.Id)))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lista execuções de simulação já registadas, com filtros opcionais.
    /// </summary>
    public async Task<IReadOnlyList<SimulationRunResponse>> ListSimulationRunsAsync(
        string? areaCode,
        string? scenarioCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var normalizedSkip = NormalizeSkip(skip);
        var normalizedTake = NormalizeTake(take);

        var query = dbContext.SimulationRuns.AsNoTracking().AsQueryable();

        if (configurationVersion.HasValue)
        {
            query = query.Where(entity => entity.ConfigurationVersion!.VersionNumber == configurationVersion.Value);
        }

        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        if (!string.IsNullOrWhiteSpace(scenarioCode))
        {
            query = query.Where(entity => entity.ScenarioCode == scenarioCode);
        }

        var projectedRuns = await query
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

        return projectedRuns
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToList();
    }

    /// <summary>
    /// Obtém o detalhe de uma execução de simulação.
    /// </summary>
    public async Task<SimulationRunResponse?> GetSimulationRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.SimulationRuns
            .AsNoTracking()
            .Where(entity => entity.Id == runId)
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
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<RuntimeRunAuditResponse?> GetRuntimeRunAuditAsync(Guid runId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await GetSimulationRunAsync(runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var warnings = new List<string>();
        var runtimeRun = ToRuntimeRun(run, warnings)!;
        var readings = await GetAcceptedReadingsForRunAsync(dbContext, runId, cancellationToken);
        var riskAssessments = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .ToListAsync(cancellationToken);
        var inboxEvents = await dbContext.InboxEvents
            .Include(entity => entity.Attempts)
            .Include(entity => entity.Rejections)
            .Include(entity => entity.Quarantines)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var runInboxEvents = inboxEvents
            .Where(entity => TryGetSimulationRunId(entity.PayloadJson) == runId)
            .ToArray();
        var areaSnapshotRows = await dbContext.AreaRiskSnapshotLogs
            .AsNoTracking()
            .Where(entity => entity.SimulationRunId == runId)
            .ToListAsync(cancellationToken);
        var areaSnapshot = areaSnapshotRows
            .OrderByDescending(entity => entity.SnapshotTimestamp)
            .Select(entity => new RuntimeAreaSnapshotAuditResponse(
                entity.SnapshotTimestamp,
                entity.AggregateRiskScore,
                entity.AggregateRiskLevel,
                entity.AssessmentCount,
                entity.Summary))
            .FirstOrDefault();

        var expectedEvents = TryGetResolvedSensorCount(run.MetadataJson) is { } sensorCount
            ? sensorCount * run.NumberOfCycles
            : (int?)null;
        var qualityFlags = BuildQualityFlagSummary(readings, expectedEvents);
        var eligibilitySummary = BuildEligibilitySummary(readings.Count, riskAssessments);

        return new RuntimeRunAuditResponse(
            runtimeRun,
            expectedEvents,
            readings.Count,
            expectedEvents.HasValue ? Math.Max(0, expectedEvents.Value - readings.Count) : null,
            runInboxEvents.SelectMany(entity => entity.Rejections).Count(),
            runInboxEvents.SelectMany(entity => entity.Quarantines).Count(),
            runInboxEvents.SelectMany(entity => entity.Attempts).Count(attempt => attempt.Outcome == ProcessingAttemptOutcome.RetryScheduled),
            riskAssessments.Count,
            qualityFlags,
            eligibilitySummary,
            areaSnapshot,
            [
                new RuntimeLimitationResponse("quality_flags_from_operational_state", "Run audit derives quality flag summary from persisted accepted reading operational states and missing-event arithmetic; detailed classifier payloads are not persisted yet."),
                new RuntimeLimitationResponse("diagnostics_do_not_recalculate_risk", "Run audit reads persisted risk assessments and snapshots only; it does not recalculate risk.")
            ]);
    }

    /// <summary>
    /// Obtém o estado operacional agregado mais recente da área.
    /// </summary>
    public async Task<AreaOperationalStateResponse?> GetAreaOperationalStateAsync(
        string areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return null;
        }

        var projectedState = await dbContext.AreaOperationalStates
            .AsNoTracking()
            .Where(entity =>
                entity.Area!.Code == areaCode &&
                entity.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .Select(entity => new
            {
                entity.Area!.Code,
                ConfigurationVersionNumber = entity.ConfigurationVersion!.VersionNumber,
                entity.SnapshotTimestamp,
                entity.AggregateRiskScore,
                entity.AggregateRiskLevel,
                entity.Severity,
                entity.Summary,
                entity.AssessmentCount,
                entity.UpdatedAt,
                OpenAlertMessage = dbContext.AlertStates
                    .Where(alert =>
                        alert.AreaId == entity.AreaId &&
                        alert.Status == "Open")
                    .Select(alert => alert.Message)
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (projectedState is null)
        {
            return null;
        }

        return new AreaOperationalStateResponse(
            projectedState.Code,
            projectedState.ConfigurationVersionNumber,
            projectedState.SnapshotTimestamp,
            projectedState.AggregateRiskScore,
            projectedState.AggregateRiskLevel,
            projectedState.Severity,
            projectedState.Summary,
            projectedState.AssessmentCount,
            projectedState.UpdatedAt,
            ParseAlertState(projectedState.OpenAlertMessage));
    }

    /// <summary>
    /// Lista os estados operacionais por célula de uma área.
    /// </summary>
    public async Task<IReadOnlyList<CellOperationalStateResponse>> ListCellOperationalStatesAsync(
        string areaCode,
        int? configurationVersion,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var resolvedConfigurationVersion = await ResolveConfigurationVersionAsync(dbContext, configurationVersion, cancellationToken);

        if (resolvedConfigurationVersion is null)
        {
            return [];
        }

        var normalizedSkip = NormalizeSkip(skip);
        var normalizedTake = NormalizeTake(take);

        var projectedCellStates = await dbContext.CellOperationalStates
            .AsNoTracking()
            .Where(entity =>
                entity.Area!.Code == areaCode &&
                entity.GridCell!.ConfigurationVersion!.VersionNumber == resolvedConfigurationVersion.Value)
            .Select(entity => new CellOperationalStateResponse(
                entity.Area!.Code,
                entity.GridCell!.CellCode,
                entity.GridCell.ConfigurationVersion!.VersionNumber,
                entity.SnapshotTimestamp,
                entity.RiskScore,
                entity.RiskLevel,
                entity.Severity,
                entity.Summary,
                entity.SensorId,
                entity.SensorNode != null ? entity.SensorNode.Name : null,
                entity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return projectedCellStates
            .OrderByDescending(entity => entity.UpdatedAt)
            .ThenBy(entity => entity.CellCode)
            .Skip(normalizedSkip)
            .Take(normalizedTake)
            .ToList();
    }

    /// <summary>
    /// Lista os alertas operacionais atualmente abertos.
    /// </summary>
    public async Task<IReadOnlyList<AlertStateResponse>> ListActiveAlertsAsync(
        string? areaCode,
        int? configurationVersion,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.AlertStates
            .AsNoTracking()
            .Where(entity => entity.Status == "Open");

        if (configurationVersion.HasValue)
        {
            query = query.Where(entity => entity.ConfigurationVersion!.VersionNumber == configurationVersion.Value);
        }

        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var projectedAlerts = await query
            .Select(entity => new
            {
                entity.Id,
                AreaCode = entity.Area!.Code,
                ConfigurationVersionNumber = entity.ConfigurationVersion!.VersionNumber,
                entity.AlertCode,
                entity.Severity,
                entity.Status,
                entity.Message,
                entity.TriggeredAt,
                entity.UpdatedAt,
                entity.ResolvedAt
            })
            .ToListAsync(cancellationToken);

        return projectedAlerts
            .Select(entity => new AlertStateResponse(
                entity.Id,
                entity.AreaCode,
                entity.ConfigurationVersionNumber,
                entity.AlertCode,
                entity.Severity,
                entity.Status,
                entity.Message,
                entity.TriggeredAt,
                entity.UpdatedAt,
                entity.ResolvedAt,
                ParseAlertState(entity.Message)))
            .OrderByDescending(entity => entity.UpdatedAt)
            .ToList();
    }

    public async Task<RuntimeSummaryResponse> GetRuntimeSummaryAsync(
        string? areaCode,
        int recentMinutes,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var normalizedRecentMinutes = NormalizeRecentMinutes(recentMinutes);
        var recentSince = generatedAtUtc.AddMinutes(-normalizedRecentMinutes);
        var warnings = new List<string>();

        var normalizedAreaCode = string.IsNullOrWhiteSpace(areaCode) ? null : areaCode.Trim();
        Guid? areaId = normalizedAreaCode is null
            ? null
            : await dbContext.Areas
                .AsNoTracking()
                .Where(entity => entity.Code == normalizedAreaCode)
                .Select(entity => (Guid?)entity.Id)
                .FirstOrDefaultAsync(cancellationToken);
        Guid? effectiveAreaId = normalizedAreaCode is null
            ? null
            : areaId ?? Guid.Empty;

        var runsQuery = dbContext.SimulationRuns.AsNoTracking().AsQueryable();
        if (normalizedAreaCode is not null)
        {
            runsQuery = runsQuery.Where(entity => entity.Area!.Code == normalizedAreaCode);
        }

        var projectedRuntimeRuns = await runsQuery
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

        var currentRun = projectedRuntimeRuns
            .Where(entity => entity.EndedAt == null)
            .OrderByDescending(entity => entity.StartedAt ?? entity.CreatedAt)
            .FirstOrDefault();

        var latestRun = projectedRuntimeRuns
            .OrderByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();

        var pipeline = await BuildPipelineSummaryAsync(dbContext, effectiveAreaId, recentSince, cancellationToken);
        var risk = await BuildRiskSummaryAsync(dbContext, effectiveAreaId, recentSince, cancellationToken);
        var areaOperationalState = await GetLatestAreaOperationalSummaryAsync(
            dbContext,
            normalizedAreaCode,
            cancellationToken);
        var cellOperationalStateCount = await CountCellOperationalStatesAsync(
            dbContext,
            normalizedAreaCode,
            cancellationToken);
        var activeAlerts = await ListRuntimeActiveAlertsAsync(
            dbContext,
            normalizedAreaCode,
            cancellationToken);
        var freshness = await BuildFreshnessSummaryAsync(
            dbContext,
            normalizedAreaCode,
            generatedAtUtc,
            cancellationToken);

        return new RuntimeSummaryResponse(
            generatedAtUtc,
            normalizedRecentMinutes,
            normalizedAreaCode,
            ToRuntimeRun(currentRun, warnings),
            ToRuntimeRun(latestRun, warnings),
            pipeline,
            risk,
            areaOperationalState,
            cellOperationalStateCount,
            activeAlerts,
            freshness,
            RuntimeLimitations.Default,
            warnings);
    }

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

    public async Task<RuntimeRunStartResponse> StartRuntimeRunAsync(
        RuntimeRunStartRequest request,
        CancellationToken cancellationToken)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var orchestratorCorrelationId = Guid.NewGuid().ToString("D");
        var warnings = new List<string>();

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.AreaCode) || string.IsNullOrWhiteSpace(request.ScenarioCode))
        {
            return RuntimeRunResponse("Rejected", "areaCode and scenarioCode are required.", null, null);
        }

        if (request.SensorCount is <= 0 || request.NumberOfCycles is <= 0 || request.IntervalSeconds is <= 0)
        {
            return RuntimeRunResponse("Rejected", "sensorCount, numberOfCycles and intervalSeconds must be positive when provided.", null, null);
        }

        var area = await dbContext.Areas
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Code == request.AreaCode, cancellationToken);
        if (area is null)
        {
            return RuntimeRunResponse("Rejected", $"Area '{request.AreaCode}' was not found.", null, null);
        }

        var scenarioExists = await dbContext.ScenarioDefinitions
            .AsNoTracking()
            .AnyAsync(entity => entity.AreaId == area.Id && entity.Code == request.ScenarioCode, cancellationToken);
        if (!scenarioExists)
        {
            return RuntimeRunResponse("Rejected", $"Scenario '{request.ScenarioCode}' was not found for area '{request.AreaCode}'.", null, null);
        }

        if (!request.AllowParallelRun)
        {
            var activeRunCount = await dbContext.SimulationRuns
                .AsNoTracking()
                .CountAsync(entity => entity.EndedAt == null, cancellationToken);
            if (activeRunCount > 0)
            {
                return RuntimeRunResponse("Rejected", $"Parallel runs are blocked by default. Found {activeRunCount} active run(s).", null, null);
            }
        }

        if (request.SensorCount.HasValue)
        {
            var activeSensorCount = await dbContext.SensorNodes
                .AsNoTracking()
                .CountAsync(entity => entity.AreaId == area.Id && entity.IsActive, cancellationToken);
            if (request.SensorCount.Value > activeSensorCount)
            {
                return RuntimeRunResponse("Rejected", $"sensorCount {request.SensorCount.Value} exceeds {activeSensorCount} active sensor(s) for area '{request.AreaCode}'.", null, null);
            }
        }

        if (string.Equals(request.ScenarioCode, "scenario_c", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(request.DegradationProfile) ||
             string.Equals(request.DegradationProfile, "none", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("scenario_c is intended for degraded/operational comparison. With degradationProfile=none it may behave like a clean scenario.");
            warnings.Add("No calibrated scientific degradation is inferred by the API; use a non-none degradationProfile only when simulator support is explicit.");
        }

        if (!_enableRuntimeProcessLaunch)
        {
            warnings.Add("Runtime process launch is disabled for this service instance; request was validated only.");
            return RuntimeRunResponse("Validated", "Run request is valid; process launch is disabled in this context.", null, null);
        }

        var logDirectory = PrepareApiRunLogDirectory(requestedAtUtc, request.RunLabel ?? request.ScenarioCode);
        var markerPath = Path.Combine(logDirectory, "request.json");
        await File.WriteAllTextAsync(
            markerPath,
            JsonSerializer.Serialize(request with { }, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        if (request.CollectEvidence)
        {
            await WriteRuntimeSummaryEvidenceAsync(logDirectory, "runtime-summary-before.json", request.AreaCode, cancellationToken);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = request.CollectEvidence,
            RedirectStandardError = request.CollectEvidence
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--configfile");
        startInfo.ArgumentList.Add("NuGet.Config");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("src/NatureProtector.Simulator.Host");
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["Simulator__ControlPlaneEnabled"] = "true";
        startInfo.Environment["Simulator__ControlPlaneAreaCode"] = request.AreaCode;
        startInfo.Environment["Simulator__ControlPlaneScenarioCode"] = request.ScenarioCode;
        startInfo.Environment["Simulator__RunOverrides__OrchestratorCorrelationId"] = orchestratorCorrelationId;
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__SensorCount", request.SensorCount);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__NumberOfCycles", request.NumberOfCycles);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__IntervalSeconds", request.IntervalSeconds);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__Seed", request.Seed);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__DegradationProfile", request.DegradationProfile);

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return RuntimeRunResponse("FailedToStart", "Simulator.Host process could not be started.", null, logDirectory);
        }

        Task<string>? stdoutTask = request.CollectEvidence ? process.StandardOutput.ReadToEndAsync(cancellationToken) : null;
        Task<string>? stderrTask = request.CollectEvidence ? process.StandardError.ReadToEndAsync(cancellationToken) : null;

        if (request.WaitForCompletion)
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                warnings.Add($"Simulator.Host did not exit within {timeout.TotalSeconds:0} seconds.");
            }
        }

        var run = await FindRuntimeRunByCorrelationAsync(
            dbContext,
            request.AreaCode,
            request.ScenarioCode,
            requestedAtUtc.AddSeconds(-5),
            orchestratorCorrelationId,
            cancellationToken);

        var response = RuntimeRunResponse(
            run is null ? "Started" : run.Status,
            run is null ? "Simulator.Host was started; the run has not appeared in control.simulation_runs yet." : "Simulator.Host was started and the run was observed.",
            ToRuntimeRun(run, warnings),
            logDirectory);

        if (request.CollectEvidence)
        {
            await WriteJsonEvidenceAsync(logDirectory, "response.json", response, cancellationToken);
            _ = Task.Run(() => CompleteRunEvidenceBundleAsync(
                logDirectory,
                request,
                response,
                process,
                stdoutTask,
                stderrTask,
                CancellationToken.None), CancellationToken.None);
        }

        return response;

        RuntimeRunStartResponse RuntimeRunResponse(
            string status,
            string message,
            RuntimeRunSummaryResponse? run,
            string? directory)
            => new(
                requestId,
                orchestratorCorrelationId,
                status,
                message,
                requestedAtUtc,
                new RuntimeRunOverrideValuesResponse(
                    request.SensorCount,
                    request.NumberOfCycles,
                    request.IntervalSeconds,
                    request.Seed,
                    request.DegradationProfile,
                    orchestratorCorrelationId),
                run,
                warnings.ToArray(),
                directory,
                request.CollectEvidence ? directory : null);
    }

    public async Task<RuntimeResetResponse> ResetRuntimeStateAsync(
        RuntimeResetRequest request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var before = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);

        if (!string.Equals(request.Scope, "runtime-only", StringComparison.Ordinal))
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, request.DryRun, "Rejected", "scope must be 'runtime-only'.", before, before);
        }

        if (!request.DryRun && !string.Equals(request.Confirm, "RESET_RUNTIME_STATE", StringComparison.Ordinal))
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, false, "Rejected", "Reset requires exact confirmation text RESET_RUNTIME_STATE.", before, before);
        }

        var activeRuns = await dbContext.SimulationRuns
            .AsNoTracking()
            .CountAsync(entity => entity.EndedAt == null, cancellationToken);
        if (activeRuns > 0)
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, request.DryRun, "Rejected", $"Reset is blocked while {activeRuns} active run(s) exist.", before, before);
        }

        if (request.DryRun)
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, true, "DryRun", "No data was changed.", before, before);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.ProcessingAttempts.RemoveRange(await dbContext.ProcessingAttempts.ToListAsync(cancellationToken));
        dbContext.RejectedEvents.RemoveRange(await dbContext.RejectedEvents.ToListAsync(cancellationToken));
        dbContext.QuarantinedEvents.RemoveRange(await dbContext.QuarantinedEvents.ToListAsync(cancellationToken));
        dbContext.InboxEvents.RemoveRange(await dbContext.InboxEvents.ToListAsync(cancellationToken));
        dbContext.AcceptedReadingLogs.RemoveRange(await dbContext.AcceptedReadingLogs.ToListAsync(cancellationToken));
        dbContext.RiskAssessmentLogs.RemoveRange(await dbContext.RiskAssessmentLogs.ToListAsync(cancellationToken));
        dbContext.AlertStates.RemoveRange(await dbContext.AlertStates.ToListAsync(cancellationToken));
        dbContext.AreaOperationalStates.RemoveRange(await dbContext.AreaOperationalStates.ToListAsync(cancellationToken));
        dbContext.CellOperationalStates.RemoveRange(await dbContext.CellOperationalStates.ToListAsync(cancellationToken));
        dbContext.AreaRiskSnapshotLogs.RemoveRange(await dbContext.AreaRiskSnapshotLogs.ToListAsync(cancellationToken));
        dbContext.SimulationRuns.RemoveRange(await dbContext.SimulationRuns.ToListAsync(cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var after = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);
        return new RuntimeResetResponse(DateTimeOffset.UtcNow, false, "Completed", "Runtime state was reset. Control plane tables were not cleared.", before, after);
    }

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
                entity.AssessmentCount,
                entity.UpdatedAt,
                entity.Summary
            })
            .ToListAsync(cancellationToken);

        return DiagnosticResult(
            definition,
            ["areaCode", "simulationRunId", "snapshotTimestamp", "aggregateRiskScore", "aggregateRiskLevel", "severity", "assessmentCount", "updatedAt", "summary"],
            rows.Select(entity => Row(
                ("areaCode", entity.AreaCode),
                ("simulationRunId", entity.SimulationRunId),
                ("snapshotTimestamp", entity.SnapshotTimestamp),
                ("aggregateRiskScore", entity.AggregateRiskScore),
                ("aggregateRiskLevel", entity.AggregateRiskLevel),
                ("severity", entity.Severity),
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
                entity.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return DiagnosticResult(
            definition,
            ["areaCode", "cellCode", "sensorId", "sensorName", "snapshotTimestamp", "riskScore", "riskLevel", "severity", "updatedAt"],
            rows.Select(entity => Row(
                ("areaCode", entity.AreaCode),
                ("cellCode", entity.CellCode),
                ("sensorId", entity.SensorId),
                ("sensorName", entity.SensorName),
                ("snapshotTimestamp", entity.SnapshotTimestamp),
                ("riskScore", entity.RiskScore),
                ("riskLevel", entity.RiskLevel),
                ("severity", entity.Severity),
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
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            Row(("scenario", label), ("metric", "simulationRunId"), ("value", run.Id)),
            Row(("scenario", label), ("metric", "status"), ("value", run.Status)),
            Row(("scenario", label), ("metric", "requested/resolved overrides"), ("value", overrides)),
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

    private sealed record ScenarioParameterInspection(
        bool HasSimulatorOptions,
        bool HasDegradationParameters,
        bool HasWeatherParameters,
        string SimulatorOptionsSummary,
        string LikelyEquivalentTo,
        string Notes);

    private string PrepareApiRunLogDirectory(DateTimeOffset requestedAtUtc, string label)
    {
        var safeLabel = new string(label.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray());
        if (string.IsNullOrWhiteSpace(safeLabel))
        {
            safeLabel = "run";
        }

        var path = Path.Combine(
            _repositoryRoot,
            "docs",
            "evidence",
            "dev-runtime",
            $"{requestedAtUtc:yyyyMMdd-HHmmss}-{safeLabel}");
        Directory.CreateDirectory(path);
        return path;
    }

    private async Task CompleteRunEvidenceBundleAsync(
        string logDirectory,
        RuntimeRunStartRequest request,
        RuntimeRunStartResponse response,
        Process process,
        Task<string>? stdoutTask,
        Task<string>? stderrTask,
        CancellationToken cancellationToken)
    {
        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                await WriteTextEvidenceAsync(logDirectory, "evidence-warning.txt", $"Simulator.Host did not exit within {timeout.TotalSeconds:0} seconds.");
            }

            if (stdoutTask is not null)
            {
                await WriteTextEvidenceAsync(logDirectory, "simulator-host.stdout.log", await stdoutTask);
            }

            if (stderrTask is not null)
            {
                await WriteTextEvidenceAsync(logDirectory, "simulator-host.stderr.log", await stderrTask);
            }

            await WriteRuntimeSummaryEvidenceAsync(logDirectory, "runtime-summary-after.json", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "runtime-table-counts", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-runs", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-expected-vs-observed", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-events-by-cycle", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-risk-by-metric", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "area-operational-state", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "cell-operational-states", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "active-alerts", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "recent-alert-transitions", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "compare-latest-b-vs-c", request.AreaCode, cancellationToken);

            await WriteRunEvidenceSummaryAsync(logDirectory, request, response, cancellationToken);
            await WritePostRunReportAsync(logDirectory, request, response, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteTextEvidenceAsync(logDirectory, "evidence-error.txt", exception.ToString());
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task WriteRuntimeSummaryEvidenceAsync(
        string logDirectory,
        string fileName,
        string areaCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await GetRuntimeSummaryAsync(areaCode, DefaultRecentMinutes, cancellationToken);
            await WriteJsonEvidenceAsync(logDirectory, fileName, summary, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteJsonEvidenceAsync(logDirectory, fileName, new { error = exception.Message }, CancellationToken.None);
        }
    }

    private async Task WriteDiagnosticEvidenceAsync(
        string logDirectory,
        string diagnosticId,
        string areaCode,
        CancellationToken cancellationToken)
    {
        var fileName = $"diagnostics-{diagnosticId}.json";
        try
        {
            var result = await ExecuteRuntimeDiagnosticAsync(
                diagnosticId,
                new RuntimeDiagnosticRequest(areaCode, DefaultRecentMinutes),
                cancellationToken);
            await WriteJsonEvidenceAsync(logDirectory, fileName, result is null ? new { error = $"Unknown diagnostic '{diagnosticId}'." } : (object)result, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteJsonEvidenceAsync(logDirectory, fileName, new { error = exception.Message }, CancellationToken.None);
        }
    }

    private static async Task WriteJsonEvidenceAsync(
        string logDirectory,
        string fileName,
        object value,
        CancellationToken cancellationToken)
        => await File.WriteAllTextAsync(
            Path.Combine(logDirectory, fileName),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

    private static async Task WriteTextEvidenceAsync(
        string logDirectory,
        string fileName,
        string value)
        => await File.WriteAllTextAsync(Path.Combine(logDirectory, fileName), value);

    private async Task WriteRunEvidenceSummaryAsync(
        string logDirectory,
        RuntimeRunStartRequest request,
        RuntimeRunStartResponse response,
        CancellationToken cancellationToken)
    {
        var expectedVsObserved = await ExecuteRuntimeDiagnosticAsync(
            "latest-run-expected-vs-observed",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);
        var riskByMetric = await ExecuteRuntimeDiagnosticAsync(
            "latest-run-risk-by-metric",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);
        var alertTransitions = await ExecuteRuntimeDiagnosticAsync(
            "recent-alert-transitions",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);

        var lines = new List<string>
        {
            "# Runtime Run Evidence",
            string.Empty,
            $"- startedAt: {response.RequestedAtUtc:o}",
            $"- completedAt: {DateTimeOffset.UtcNow:o}",
            $"- runLabel: {request.RunLabel}",
            $"- areaCode: {request.AreaCode}",
            $"- scenarioCode: {request.ScenarioCode}",
            $"- simulationRunId: {response.Run?.Id}",
            $"- correlationId: {response.OrchestratorCorrelationId}",
            $"- evidenceDirectory: {response.EvidenceDirectory}",
            string.Empty,
            "## Requested Parameters",
            string.Empty,
            $"- sensorCount: {request.SensorCount}",
            $"- numberOfCycles: {request.NumberOfCycles}",
            $"- intervalSeconds: {request.IntervalSeconds}",
            $"- seed: {request.Seed}",
            $"- degradationProfile: {request.DegradationProfile}",
            $"- collectEvidence: {request.CollectEvidence}",
            $"- waitForCompletion: {request.WaitForCompletion}",
            string.Empty,
            "## Resolved Parameters",
            string.Empty,
            $"- status: {response.Status}",
            $"- message: {response.Message}",
            $"- selectedSensors: {string.Join(", ", response.Run?.RunOverrides?.SelectedSensorNames ?? [])}",
            string.Empty,
            "## Expected Vs Observed",
            string.Empty
        };

        lines.AddRange((expectedVsObserved?.Rows ?? []).Select(row => $"- {row.GetValueOrDefault("metric")}: {row.GetValueOrDefault("value")}"));
        lines.Add(string.Empty);
        lines.Add("## Risk By Metric");
        lines.Add(string.Empty);
        lines.AddRange((riskByMetric?.Rows ?? []).Select(row => $"- {row.GetValueOrDefault("metricType")}: count={row.GetValueOrDefault("count")}; minScore={row.GetValueOrDefault("minScore")}; maxScore={row.GetValueOrDefault("maxScore")}; avgScore={row.GetValueOrDefault("avgScore")}"));
        lines.Add(string.Empty);
        lines.Add("## Alert Transitions");
        lines.Add(string.Empty);
        lines.AddRange((alertTransitions?.Rows ?? []).Select(row => $"- {row.GetValueOrDefault("status")} {row.GetValueOrDefault("alertState")} at {row.GetValueOrDefault("updatedAt")}"));
        lines.Add(string.Empty);
        lines.Add("## Limitations");
        lines.Add(string.Empty);
        lines.Add("- Evidence uses read-only runtime diagnostics and persisted projections; it does not recalculate risk or alert state.");

        await WriteTextEvidenceAsync(logDirectory, "summary.md", string.Join(Environment.NewLine, lines));
    }

    private async Task WritePostRunReportAsync(
        string logDirectory,
        RuntimeRunStartRequest request,
        RuntimeRunStartResponse response,
        CancellationToken cancellationToken)
    {
        var comparison = await ExecuteRuntimeDiagnosticAsync(
            "compare-latest-b-vs-c",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);

        var lines = new List<string>
        {
            "# Post Run Report",
            string.Empty,
            $"Run `{request.ScenarioCode}` was submitted with correlation `{response.OrchestratorCorrelationId}`.",
            string.Empty,
            "## Final State",
            string.Empty,
            $"- status: {response.Status}",
            $"- simulationRunId: {response.Run?.Id}",
            $"- evidenceDirectory: {response.EvidenceDirectory}",
            string.Empty,
            "## Comparison",
            string.Empty
        };

        if (comparison is null || comparison.Rows.Count == 0)
        {
            lines.Add("No B/C comparison data was available.");
        }
        else
        {
            lines.AddRange(comparison.Rows.Select(row => $"- {row.GetValueOrDefault("scenario")} / {row.GetValueOrDefault("metric")}: {row.GetValueOrDefault("value")}"));
        }

        lines.Add(string.Empty);
        lines.Add("## Limitations");
        lines.Add(string.Empty);
        lines.Add("- This report is generated from persisted runtime diagnostics. It does not use screenshots and does not recompute risk.");

        await WriteTextEvidenceAsync(logDirectory, "post-run-report.md", string.Join(Environment.NewLine, lines));
    }

    private static void SetProcessEnvironmentIfDefined(ProcessStartInfo startInfo, string name, object? value)
    {
        if (value is null)
        {
            return;
        }

        startInfo.Environment[name] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string ResolveRepositoryRoot(string startPath)
    {
        var current = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : new FileInfo(startPath).Directory;

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static async Task<RuntimePipelineSummaryResponse> BuildPipelineSummaryAsync(
        NatureProtectorControlDbContext dbContext,
        Guid? areaId,
        DateTimeOffset recentSince,
        CancellationToken cancellationToken)
    {
        var inboxQuery = dbContext.InboxEvents.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            inboxQuery = inboxQuery.Where(entity => entity.AreaId == areaId.Value);
        }

        var attemptsQuery = dbContext.ProcessingAttempts
            .AsNoTracking();
        if (areaId.HasValue)
        {
            attemptsQuery = attemptsQuery.Where(entity => entity.InboxEvent!.AreaId == areaId.Value);
        }

        var rejectedQuery = dbContext.RejectedEvents.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            rejectedQuery = rejectedQuery.Where(entity => entity.InboxEvent != null && entity.InboxEvent.AreaId == areaId.Value);
        }

        var quarantinedQuery = dbContext.QuarantinedEvents.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            quarantinedQuery = quarantinedQuery.Where(entity => entity.InboxEvent!.AreaId == areaId.Value);
        }

        var inboxEvents = await inboxQuery.ToListAsync(cancellationToken);
        var recentInboxEvents = inboxEvents.Where(entity => entity.ReceivedAt >= recentSince).ToArray();
        var inboxByStatus = inboxEvents
            .GroupBy(entity => entity.Status)
            .Select(group => new RuntimeStatusCountResponse(group.Key.ToString(), group.Count()))
            .ToArray();

        var attempts = await attemptsQuery.ToListAsync(cancellationToken);
        var recentAttempts = attempts.Where(entity => entity.StartedAt >= recentSince).ToArray();
        var attemptsByOutcomeAndError = recentAttempts
            .GroupBy(entity => new { entity.Outcome, entity.ErrorCode })
            .Select(group => new RuntimeAttemptCountResponse(group.Key.Outcome.ToString(), group.Key.ErrorCode, group.Count()))
            .ToArray();

        var rejectedItems = await rejectedQuery
            .Select(entity => new RuntimeRejectedEventResponse(
                entity.Id,
                entity.EventId,
                entity.RejectionCode,
                entity.RejectionReason,
                entity.RejectedAt,
                entity.MetadataJson))
            .ToListAsync(cancellationToken);
        var recentRejectedItems = rejectedItems.Where(entity => entity.RejectedAt >= recentSince).ToArray();
        var rejectedByCode = recentRejectedItems
            .GroupBy(entity => entity.RejectionCode)
            .Select(group => new RuntimeCodeCountResponse(group.Key, group.Count()))
            .ToArray();

        var quarantinedItems = await quarantinedQuery
            .Select(entity => new RuntimeQuarantinedEventResponse(
                entity.Id,
                entity.EventId,
                entity.FinalAttemptNumber,
                entity.QuarantineCode,
                entity.QuarantineReason,
                entity.QuarantinedAt,
                entity.MetadataJson))
            .ToListAsync(cancellationToken);
        var recentQuarantinedItems = quarantinedItems.Where(entity => entity.QuarantinedAt >= recentSince).ToArray();
        var quarantinedByCode = recentQuarantinedItems
            .GroupBy(entity => entity.QuarantineCode)
            .Select(group => new RuntimeCodeCountResponse(group.Key, group.Count()))
            .ToArray();

        var latestRejected = recentRejectedItems
            .OrderByDescending(entity => entity.RejectedAt)
            .Take(10)
            .ToArray();

        var latestQuarantined = recentQuarantinedItems
            .OrderByDescending(entity => entity.QuarantinedAt)
            .Take(10)
            .ToArray();

        var latestFailedAttempts = recentAttempts
            .Where(entity => entity.Outcome == ProcessingAttemptOutcome.Failed ||
                             entity.Outcome == ProcessingAttemptOutcome.RetryScheduled ||
                             entity.Outcome == ProcessingAttemptOutcome.Quarantined)
            .OrderByDescending(entity => entity.StartedAt)
            .Take(10)
            .Select(entity => new RuntimeProcessingAttemptResponse(
                entity.Id,
                entity.InboxEventId,
                entity.AttemptNumber,
                entity.Stage,
                entity.StartedAt,
                entity.FinishedAt,
                entity.Outcome.ToString(),
                entity.ErrorCode,
                entity.ErrorMessage))
            .ToArray();

        return new RuntimePipelineSummaryResponse(
            inboxEvents.Count,
            recentInboxEvents.Length,
            inboxByStatus.OrderBy(entity => entity.Status).ToArray(),
            recentAttempts.Length,
            attemptsByOutcomeAndError
                .OrderBy(entity => entity.Outcome)
                .ThenBy(entity => entity.ErrorCode)
                .ToArray(),
            recentRejectedItems.Length,
            rejectedItems.Count,
            rejectedByCode.OrderBy(entity => entity.Code).ToArray(),
            recentQuarantinedItems.Length,
            quarantinedItems.Count,
            quarantinedByCode.OrderBy(entity => entity.Code).ToArray(),
            latestRejected,
            latestQuarantined,
            latestFailedAttempts);
    }

    private static async Task<RuntimeRiskSummaryResponse> BuildRiskSummaryAsync(
        NatureProtectorControlDbContext dbContext,
        Guid? areaId,
        DateTimeOffset recentSince,
        CancellationToken cancellationToken)
    {
        var riskQuery = dbContext.RiskAssessmentLogs
            .AsNoTracking();
        if (areaId.HasValue)
        {
            riskQuery = riskQuery.Where(entity => entity.AreaId == areaId.Value);
        }

        var riskItems = await riskQuery
            .Select(entity => new
            {
                entity.Timestamp,
                entity.CreatedAt,
                entity.RiskScore,
                entity.RiskLevel
            })
            .ToListAsync(cancellationToken);
        var recentScores = riskItems
            .Where(entity => entity.CreatedAt >= recentSince)
            .Select(entity => new RuntimeRiskPointResponse(
                entity.Timestamp,
                entity.RiskScore,
                entity.RiskLevel))
            .OrderBy(entity => entity.Timestamp)
            .ToArray();

        return new RuntimeRiskSummaryResponse(
            recentScores.Length,
            recentScores.Length == 0 ? null : recentScores.Min(entity => entity.RiskScore),
            recentScores.Length == 0 ? null : recentScores.Max(entity => entity.RiskScore),
            recentScores.Length == 0 ? null : recentScores.Max(entity => entity.Timestamp),
            recentScores);
    }

    private static async Task<RuntimeAreaOperationalSummaryResponse?> GetLatestAreaOperationalSummaryAsync(
        NatureProtectorControlDbContext dbContext,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AreaOperationalStates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var projectedStates = await query
            .Select(entity => new
            {
                entity.AreaId,
                AreaCode = entity.Area!.Code,
                ConfigurationVersionNumber = entity.ConfigurationVersion!.VersionNumber,
                entity.SnapshotTimestamp,
                entity.AggregateRiskScore,
                entity.AggregateRiskLevel,
                entity.Severity,
                entity.Summary,
                entity.AssessmentCount,
                entity.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var projectedState = projectedStates
            .OrderByDescending(entity => entity.UpdatedAt)
            .FirstOrDefault();

        if (projectedState is null)
        {
            return null;
        }

        var openAlertMessages = await dbContext.AlertStates
            .AsNoTracking()
            .Where(alert =>
                alert.AreaId == projectedState.AreaId &&
                alert.Status == "Open")
            .Select(alert => new { alert.Message, alert.UpdatedAt })
            .ToListAsync(cancellationToken);

        var openAlertMessage = openAlertMessages
            .OrderByDescending(alert => alert.UpdatedAt)
            .Select(alert => alert.Message)
            .FirstOrDefault();

        return new RuntimeAreaOperationalSummaryResponse(
                projectedState.AreaCode,
                projectedState.ConfigurationVersionNumber,
                projectedState.SnapshotTimestamp,
                projectedState.AggregateRiskScore,
                projectedState.AggregateRiskLevel,
                projectedState.Severity,
                projectedState.Summary,
                projectedState.AssessmentCount,
                projectedState.UpdatedAt,
                ParseAlertState(openAlertMessage));
    }

    private static async Task<int> CountCellOperationalStatesAsync(
        NatureProtectorControlDbContext dbContext,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CellOperationalStates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        return await query.CountAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<RuntimeAlertSummaryResponse>> ListRuntimeActiveAlertsAsync(
        NatureProtectorControlDbContext dbContext,
        string? areaCode,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AlertStates
            .AsNoTracking()
            .Where(entity => entity.Status == "Open");

        if (!string.IsNullOrWhiteSpace(areaCode))
        {
            query = query.Where(entity => entity.Area!.Code == areaCode);
        }

        var alerts = await query
            .Select(entity => new
            {
                entity.Id,
                AreaCode = entity.Area!.Code,
                ConfigurationVersionNumber = entity.ConfigurationVersion!.VersionNumber,
                entity.AlertCode,
                entity.Severity,
                entity.Status,
                entity.Message,
                entity.TriggeredAt,
                entity.UpdatedAt,
                entity.ResolvedAt
            })
            .ToListAsync(cancellationToken);

        return alerts
            .OrderByDescending(entity => entity.UpdatedAt)
            .Select(entity => new RuntimeAlertSummaryResponse(
                entity.Id,
                entity.AreaCode,
                entity.ConfigurationVersionNumber,
                entity.AlertCode,
                entity.Severity,
                entity.Status,
                entity.Message,
                entity.TriggeredAt,
                entity.UpdatedAt,
                entity.ResolvedAt,
                ParseAlertState(entity.Message)))
            .ToArray();
    }

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
            GetStringProperty(element, "orchestrator_correlation_id"));
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
}
