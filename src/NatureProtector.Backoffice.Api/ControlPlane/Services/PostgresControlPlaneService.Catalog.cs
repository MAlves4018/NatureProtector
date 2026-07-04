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

// Feature slice: Catalog. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    // <phase5-slice id="catalog-queries">
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
        var areaId = await dbContext.SimulationRuns
            .AsNoTracking()
            .Where(entity => entity.Id == runId)
            .Select(entity => (Guid?)entity.AreaId)
            .SingleOrDefaultAsync(cancellationToken);
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
            .Where(entity =>
                TryGetSimulationRunId(entity.PayloadJson) == runId ||
                TryGetSimulationRunId(entity.EnvelopeJson) == runId)
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
        var scoreComponents = await BuildLatestScoreComponentSummaryAsync(
            dbContext,
            areaId,
            runId,
            cancellationToken);
        var indexComparison = await BuildLatestIndexComparisonSummaryAsync(
            dbContext,
            areaId,
            runId,
            cancellationToken);

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
            ],
            scoreComponents,
            indexComparison,
            BuildRunDataScope(
                runId,
                runId,
                runId,
                "PostgreSQL runtime tables",
                "simulation-run"));
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
                entity.CoverageStatus,
                entity.FreshnessStatus,
                entity.CarryForwardStatus,
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
            ParseAlertState(projectedState.OpenAlertMessage),
            projectedState.CoverageStatus,
            projectedState.FreshnessStatus,
            projectedState.CarryForwardStatus,
            projectedState.SnapshotTimestamp,
            projectedState.UpdatedAt,
            BuildOperationalStatusReason(projectedState.CoverageStatus, projectedState.FreshnessStatus, projectedState.CarryForwardStatus));
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
                entity.UpdatedAt,
                entity.CoverageStatus,
                entity.FreshnessStatus,
                entity.CarryForwardStatus,
                null,
                entity.SnapshotTimestamp,
                BuildOperationalStatusReason(entity.CoverageStatus, entity.FreshnessStatus, entity.CarryForwardStatus)))
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

    // </phase5-slice>

}
