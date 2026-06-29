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

// Feature slice: RuntimeSummary. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    // <phase5-slice id="runtime-summary-api">
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
        var scoreComponents = await BuildLatestScoreComponentSummaryAsync(
            dbContext,
            effectiveAreaId,
            latestRun?.Id,
            cancellationToken);
        var indexComparison = await BuildLatestIndexComparisonSummaryAsync(
            dbContext,
            effectiveAreaId,
            latestRun?.Id,
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
            scoreComponents,
            indexComparison,
            RuntimeLimitations.Default,
            warnings);
    }

    // </phase5-slice>

    // <phase5-slice id="runtime-summary-helpers">
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

    private static async Task<RuntimeScoreComponentSummaryResponse?> BuildLatestScoreComponentSummaryAsync(
        NatureProtectorControlDbContext dbContext,
        Guid? areaId,
        Guid? latestRunId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RiskAssessmentLogs.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.AreaId == areaId.Value);
        }

        if (latestRunId.HasValue)
        {
            query = query.Where(entity => entity.SimulationRunId == latestRunId.Value);
        }

        var rows = await query
            .Select(entity => new
            {
                entity.RiskScore,
                entity.BaseRisk,
                entity.AdjustedScore,
                entity.Score100,
                entity.MeteorologyComponent,
                entity.DroughtComponent,
                entity.TerritoryComponent,
                entity.HazardComponent,
                entity.FuelComponent,
                entity.GeomorphologyComponent,
                entity.ConfidenceFactor,
                entity.IntegrityFactor,
                entity.DominantDriver,
                entity.ParameterSetVersion,
                entity.CalculationStatus,
                entity.Limitations,
                entity.Timestamp,
                entity.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var latest = rows
            .OrderByDescending(entity => entity.Timestamp)
            .ThenByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();

        return latest is null
            ? null
            : BuildScoreComponentSummary(latest.RiskScore,
                latest.BaseRisk,
                latest.AdjustedScore,
                latest.Score100,
                latest.MeteorologyComponent,
                latest.DroughtComponent,
                latest.TerritoryComponent,
                latest.HazardComponent,
                latest.FuelComponent,
                latest.GeomorphologyComponent,
                latest.ConfidenceFactor,
                latest.IntegrityFactor,
                latest.DominantDriver,
                latest.ParameterSetVersion,
                latest.CalculationStatus,
                latest.Limitations,
                latest.Timestamp);
    }

    private static async Task<RuntimeIndexComparisonSummaryResponse?> BuildLatestIndexComparisonSummaryAsync(
        NatureProtectorControlDbContext dbContext,
        Guid? areaId,
        Guid? latestRunId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.DailyCellStates.AsNoTracking().AsQueryable();
        if (areaId.HasValue)
        {
            query = query.Where(entity => entity.AreaId == areaId.Value);
        }

        if (latestRunId.HasValue)
        {
            query = query.Where(entity => entity.SimulationRunId == latestRunId.Value);
        }

        var rows = await query
            .Select(entity => new
            {
                entity.FireWeatherIndex,
                entity.NormalizedFireWeatherIndex,
                entity.FireWeatherCalculationStatus,
                entity.KeetchByramDroughtIndex,
                entity.NormalizedKeetchByramDroughtIndex,
                entity.KbdiCalculationStatus,
                entity.Provenance,
                entity.FireIndexProvenance,
                entity.FireWeatherLimitations,
                entity.KbdiLimitations,
                entity.DailyPrecipitationMillimeters,
                entity.LogicalDate,
                entity.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var latest = rows
            .OrderByDescending(entity => entity.LogicalDate)
            .ThenByDescending(entity => entity.UpdatedAt)
            .FirstOrDefault();

        if (latest is null)
        {
            return null;
        }

        var limitations = string.Join(
            "; ",
            new[] { latest.FireWeatherLimitations, latest.KbdiLimitations }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var fwiClass = ClassifyFireWeatherIndex(
            latest.FireWeatherIndex,
            latest.NormalizedFireWeatherIndex,
            latest.FireWeatherCalculationStatus);
        var kbdiClass = ClassifyKbdi(
            latest.KeetchByramDroughtIndex,
            latest.NormalizedKeetchByramDroughtIndex,
            latest.KbdiCalculationStatus,
            latest.KbdiLimitations);
        var riskRows = await dbContext.RiskAssessmentLogs
            .AsNoTracking()
            .Where(entity => (!areaId.HasValue || entity.AreaId == areaId.Value) &&
                (!latestRunId.HasValue || entity.SimulationRunId == latestRunId.Value))
            .Select(entity => new
            {
                entity.TerritoryComponent,
                entity.Timestamp,
                entity.CreatedAt
            })
            .ToListAsync(cancellationToken);
        var latestRisk = riskRows
            .OrderByDescending(entity => entity.Timestamp)
            .ThenByDescending(entity => entity.CreatedAt)
            .FirstOrDefault();
        var portugueseProxy = BuildPortugueseContextProxy(fwiClass.IpmaClass, latestRisk?.TerritoryComponent);
        var localPercentile = LocalFwiPercentileNotAvailable();
        var fwiValueSource = ResolveIndexValueSource(
            latest.FireWeatherIndex,
            latest.FireWeatherCalculationStatus,
            latest.FireIndexProvenance,
            latest.Provenance,
            "candidate_fwi_calculator");

        var kbdiValueSource = ResolveIndexValueSource(
            latest.KeetchByramDroughtIndex,
            latest.KbdiCalculationStatus,
            latest.FireIndexProvenance,
            latest.Provenance,
            "candidate_kbdi_calculator");
        var kbdiAntecedentDays = latest.KbdiLimitations?.Contains("antecedent_kbdi_candidate_default", StringComparison.OrdinalIgnoreCase) == true ||
            string.Equals(latest.KbdiCalculationStatus, "LimitedAntecedentHistory", StringComparison.OrdinalIgnoreCase)
                ? 0
                : (int?)null;

        return new RuntimeIndexComparisonSummaryResponse(
            latest.FireWeatherIndex,
            latest.NormalizedFireWeatherIndex,
            latest.FireWeatherCalculationStatus,
            latest.KeetchByramDroughtIndex,
            latest.NormalizedKeetchByramDroughtIndex,
            latest.KbdiCalculationStatus,
            latest.FireIndexProvenance ?? latest.Provenance,
            string.IsNullOrWhiteSpace(limitations) ? null : limitations,
            latest.DailyPrecipitationMillimeters,
            latest.LogicalDate,
            fwiValueSource == "calculated_candidate" ? latest.FireWeatherIndex : null,
            fwiValueSource == "reference_or_imported" ? latest.FireWeatherIndex : null,
            fwiValueSource,
            fwiClass.IpmaClass,
            fwiClass.IpmaLabel,
            fwiClass.EffisClass,
            fwiClass.DistanceToNext,
            fwiClass.NextIpmaClass,
            kbdiValueSource == "calculated_candidate" ? latest.KeetchByramDroughtIndex : null,
            kbdiValueSource == "reference_or_imported" ? latest.KeetchByramDroughtIndex : null,
            kbdiValueSource,
            kbdiClass.Code,
            kbdiClass.Label,
            kbdiClass.AntecedentQuality,
            kbdiAntecedentDays,
            portugueseProxy.Code,
            portugueseProxy.Label,
            portugueseProxy.TerritoryClass,
            localPercentile.Status,
            localPercentile.Percentile,
            localPercentile.Reason);
    }

    private static string ResolveIndexValueSource(
        double? value,
        string? calculationStatus,
        string? indexProvenance,
        string? generalProvenance,
        string calculatorMarker)
    {
        if (!value.HasValue)
        {
            return "missing";
        }

        if (!string.IsNullOrWhiteSpace(indexProvenance) &&
            indexProvenance.Contains(calculatorMarker, StringComparison.OrdinalIgnoreCase))
        {
            return "calculated_candidate";
        }

        if (string.Equals(calculationStatus, "CompleteWithCandidateDefaults", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(calculationStatus, "CalculatedFromHistory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(calculationStatus, "Complete", StringComparison.OrdinalIgnoreCase))
        {
            return "calculated_candidate";
        }

        if ((!string.IsNullOrWhiteSpace(indexProvenance) &&
             (indexProvenance.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
              indexProvenance.Contains("import", StringComparison.OrdinalIgnoreCase))) ||
            (!string.IsNullOrWhiteSpace(generalProvenance) &&
             (generalProvenance.Contains("reference", StringComparison.OrdinalIgnoreCase) ||
              generalProvenance.Contains("import", StringComparison.OrdinalIgnoreCase))))
        {
            return "reference_or_imported";
        }

        return "calculated_candidate";
    }


    private static RuntimeScoreComponentSummaryResponse BuildScoreComponentSummary(
        double? riskScore,
        double? baseRisk,
        double? adjustedScore,
        int? score100,
        double? meteorology,
        double? drought,
        double? territory,
        double? hazard,
        double? fuel,
        double? geomorphology,
        double? confidence,
        double? integrity,
        string? dominantDriver,
        string? parameterSetVersion,
        string? calculationStatus,
        string? limitations,
        DateTimeOffset? timestamp)
    {
        var classification = ClassifyNatureProtector(riskScore);
        return new RuntimeScoreComponentSummaryResponse(
            riskScore,
            baseRisk,
            adjustedScore,
            score100,
            meteorology,
            drought,
            territory,
            hazard,
            fuel,
            geomorphology,
            confidence,
            integrity,
            dominantDriver,
            parameterSetVersion,
            calculationStatus,
            limitations,
            timestamp,
            classification.Code,
            classification.Label);
    }

    private static ApiRiskClass ClassifyNatureProtector(double? score)
    {
        if (!score.HasValue)
        {
            return new ApiRiskClass(null, null);
        }

        var value = Math.Clamp(score.Value, 0.0, 1.0);
        return value switch
        {
            < 0.2 => new ApiRiskClass("VeryLow", "Muito baixo"),
            < 0.4 => new ApiRiskClass("Low", "Baixo"),
            < 0.6 => new ApiRiskClass("Moderate", "Moderado"),
            < 0.8 => new ApiRiskClass("High", "Elevado"),
            _ => new ApiRiskClass("VeryHigh", "Muito elevado")
        };
    }

    private static ApiFwiClass ClassifyFireWeatherIndex(
        double? fireWeatherIndex,
        double? normalizedFireWeatherIndex,
        string? status)
    {
        if (!fireWeatherIndex.HasValue ||
            string.Equals(status, "Missing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiFwiClass(null, null, null, null, null);
        }

        var value = fireWeatherIndex.Value;
        (string Code, string Label, string? Next, double? Threshold) item = value switch
        {
            < 8.2 => ("Low", "Baixo/Reduzido", "High", 8.2),
            < 17.2 => ("Moderate", "Moderado", "High", 17.2),
            < 24.6 => ("High", "Elevado", "VeryHigh", 24.6),
            < 38.3 => ("VeryHigh", "Muito Elevado", "Maximum", 38.3),
            < 50.1 => ("Maximum", "Maximo", "Extreme", 50.1),
            < 64.0 => ("Extreme", "Extremo", "Exceptional", 64.0),
            _ => ("Exceptional", "Excecional", null, (double?)null)
        };

        return new ApiFwiClass(
            item.Code,
            item.Label,
            ClassifyEffis(value),
            item.Threshold.HasValue ? Math.Round(item.Threshold.Value - value, 3) : null,
            item.Next);
    }

    private static string ClassifyEffis(double value)
    {
        return value switch
        {
            < 5.2 => "VeryLow",
            < 11.2 => "Low",
            < 21.3 => "Moderate",
            < 38.0 => "High",
            < 50.0 => "VeryHigh",
            _ => "Extreme"
        };
    }

    private static ApiKbdiClass ClassifyKbdi(
        double? kbdi,
        double? normalizedKbdi,
        string? status,
        string? limitations)
    {
        if (!kbdi.HasValue ||
            string.Equals(status, "Missing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Partial", StringComparison.OrdinalIgnoreCase))
        {
            return new ApiKbdiClass(null, null, "NotAvailable");
        }

        var value = Math.Clamp(kbdi.Value, 0.0, 800.0);
        var (code, label) = value switch
        {
            < 200.0 => ("VeryLowDryness", "Secura muito baixa"),
            < 400.0 => ("LowModerateDryness", "Secura baixa a moderada"),
            < 600.0 => ("HighDryness", "Secura elevada"),
            < 700.0 => ("SevereDryness", "Secura severa"),
            _ => ("ExtremeDryness", "Secura extrema")
        };
        var history = status switch
        {
            "LimitedAntecedentHistory" => "LimitedAntecedentHistory",
            "CompleteWithCandidateDefaults" => "CandidateDefaults",
            "CalculatedFromHistory" => "CalculatedFromHistory",
            "ReferenceImported" => "ReferenceImported",
            "Complete" => limitations?.Contains("antecedent_kbdi_candidate_default", StringComparison.OrdinalIgnoreCase) == true
                ? "LimitedAntecedentHistory"
                : "Complete",
            _ => status ?? "NotAvailable"
        };

        return new ApiKbdiClass(code, label, history);
    }

    private static ApiPortugueseProxy BuildPortugueseContextProxy(string? fwiIpmaClass, double? territoryComponent)
    {
        var territory = territoryComponent.HasValue ? ClassifyTerritory(territoryComponent.Value) : null;
        if (string.IsNullOrWhiteSpace(fwiIpmaClass) || string.IsNullOrWhiteSpace(territory))
        {
            return new ApiPortugueseProxy("Missing", null, null, territory, "not_official_rcm;missing_fwi_or_territory");
        }

        var fwiRank = fwiIpmaClass switch
        {
            "Low" => 0,
            "Moderate" => 1,
            "High" => 2,
            "VeryHigh" => 3,
            "Maximum" => 4,
            "Extreme" => 5,
            "Exceptional" => 6,
            _ => -1
        };
        var territoryRank = territory switch
        {
            "VeryLow" => 0,
            "Low" => 1,
            "Moderate" => 2,
            "High" => 3,
            "VeryHigh" => 4,
            _ => -1
        };
        if (fwiRank < 0 || territoryRank < 0)
        {
            return new ApiPortugueseProxy("Partial", null, null, territory, "not_official_rcm;unmapped_fwi_or_territory_class");
        }

        var code = (fwiRank, territoryRank) switch
        {
            (>= 4, >= 3) => "Extreme",
            (>= 3, >= 3) => "VeryHigh",
            (>= 2, >= 3) => "VeryHigh",
            (>= 1, >= 3) => "High",
            _ => Math.Max(fwiRank, territoryRank) switch
            {
                <= 1 => "Low",
                2 => "Moderate",
                3 => "High",
                _ => "VeryHigh"
            }
        };

        return new ApiPortugueseProxy("Complete", code, LabelPortugueseProxy(code), territory, "not_official_rcm;does_not_use_official_icnf_rural_hazard");
    }

    private static string ClassifyTerritory(double territoryComponent)
    {
        var value = Math.Clamp(territoryComponent, 0.0, 1.0);
        return value switch
        {
            < 0.2 => "VeryLow",
            < 0.4 => "Low",
            < 0.6 => "Moderate",
            < 0.8 => "High",
            _ => "VeryHigh"
        };
    }

    private static string LabelPortugueseProxy(string code)
    {
        return code switch
        {
            "Low" => "Baixo",
            "Moderate" => "Moderado",
            "High" => "Elevado",
            "VeryHigh" => "Muito elevado",
            "Extreme" => "Extremo",
            _ => code
        };
    }

    private static ApiLocalFwiPercentile LocalFwiPercentileNotAvailable()
        => new("NotAvailable", null, "historical_local_fwi_distribution_not_materialized");

    private sealed record ApiRiskClass(string? Code, string? Label);

    private sealed record ApiFwiClass(
        string? IpmaClass,
        string? IpmaLabel,
        string? EffisClass,
        double? DistanceToNext,
        string? NextIpmaClass);

    private sealed record ApiKbdiClass(
        string? Code,
        string? Label,
        string AntecedentQuality);

    private sealed record ApiPortugueseProxy(
        string Status,
        string? Code,
        string? Label,
        string? TerritoryClass,
        string Limitations);

    private sealed record ApiLocalFwiPercentile(
        string Status,
        double? Percentile,
        string Reason);

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
                entity.CoverageStatus,
                entity.FreshnessStatus,
                entity.CarryForwardStatus,
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
                ParseAlertState(openAlertMessage),
                projectedState.CoverageStatus,
                projectedState.FreshnessStatus,
                projectedState.CarryForwardStatus,
                projectedState.SnapshotTimestamp,
                projectedState.UpdatedAt,
                BuildOperationalStatusReason(projectedState.CoverageStatus, projectedState.FreshnessStatus, projectedState.CarryForwardStatus));
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

    // </phase5-slice>

}
