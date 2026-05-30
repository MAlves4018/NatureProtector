using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public sealed record RiskInput(
    Guid AreaId,
    Guid SensorId,
    Guid SourceEventId,
    SensorMetricType MetricType,
    double Value,
    MeasurementUnit Unit,
    DateTimeOffset EventTime)
{
    public const string MissingDailyCellStateFlag = "daily_cell_state_missing";
    public const string LowCoverageFlag = "low_coverage";

    private static readonly IReadOnlyList<string> EmptyQualityFlags = Array.Empty<string>();
    private static readonly IReadOnlyList<ClassifierResult> EmptyClassifierResults = Array.Empty<ClassifierResult>();

    public Guid? SimulationRunId { get; init; }

    public Guid? GridCellId { get; init; }

    public Guid? ConfigurationVersionId { get; init; }

    public DateTimeOffset ValidFrom { get; init; } = EventTime;

    public DateTimeOffset ValidTo { get; init; } = EventTime;

    public IReadOnlyList<RiskInputSourceReading> SourceReadings { get; init; } = Array.Empty<RiskInputSourceReading>();

    public RiskInputMetricSet Metrics { get; init; } = new(null, null, null);

    public DailyCellState? DailyCellState { get; init; }

    public DailyCellStateStatus DailyCellStateStatus { get; init; } = DailyCellStateStatus.NotEvaluated;

    public TerritorialRiskContext TerritorialContext { get; init; } = TerritorialRiskContext.Unknown(null);

    public FireWeatherIndexContext FireWeatherIndexContext { get; init; } = FireWeatherIndexContext.Absent;

    public string ParameterSetVersion { get; init; } = CandidateParameterSetV1.Version;

    public RiskInputStatus InputStatus { get; init; } = RiskInputStatus.CompleteEligible;

    public RiskEligibilityReason EligibilityReason { get; init; } = RiskEligibilityReason.Eligible;

    public ObservationalConfidenceLevel ObservationalConfidence { get; init; } = ObservationalConfidenceLevel.High;

    public OperationalIntegrityLevel OperationalIntegrity { get; init; } = OperationalIntegrityLevel.Intact;

    public IReadOnlyList<string> QualityFlags { get; init; } = EmptyQualityFlags;

    public IReadOnlyList<QualityFlag> TypedQualityFlags => QualityFlagCatalog.ParseMany(QualityFlags);

    public IReadOnlyList<ClassifierResult> ClassifierResults { get; init; } = EmptyClassifierResults;

    public static RiskInput FromNormalizedReading(NormalizedReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return CreateBaseInput(reading) with
        {
            QualityFlags = reading.QualityFlags ?? EmptyQualityFlags,
            ClassifierResults = reading.ClassifierResults ?? EmptyClassifierResults
        };
    }

    public static RiskInput FromNormalizedReading(
        NormalizedReading reading,
        RiskEligibilityResult eligibility)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(eligibility);

        return CreateBaseInput(reading) with
        {
            InputStatus = eligibility.Status,
            EligibilityReason = eligibility.ReasonCode,
            ObservationalConfidence = eligibility.ObservationalConfidence,
            OperationalIntegrity = eligibility.OperationalIntegrity,
            QualityFlags = MergeQualityFlags(reading.QualityFlags, eligibility.QualityFlags),
            ClassifierResults = ResolveClassifierResults(reading.ClassifierResults, eligibility.ClassifierResults)
        };
    }

    public static RiskInput FromNormalizedReading(
        NormalizedReading reading,
        RiskEligibilityResult eligibility,
        DailyCellState? dailyCellState,
        Guid? simulationRunId,
        Guid? gridCellId,
        Guid? configurationVersionId,
        TerritorialRiskContext? territorialContext = null)
    {
        return FromWindow(
            [reading],
            eligibility,
            dailyCellState,
            simulationRunId,
            gridCellId,
            configurationVersionId,
            territorialContext);
    }

    public static RiskInput FromWindow(
        IReadOnlyList<NormalizedReading> readings,
        RiskEligibilityResult eligibility,
        DailyCellState? dailyCellState,
        Guid? simulationRunId,
        Guid? gridCellId,
        Guid? configurationVersionId,
        TerritorialRiskContext? territorialContext = null)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(eligibility);

        if (readings.Count == 0)
        {
            throw new ArgumentException("At least one normalized reading is required.", nameof(readings));
        }

        var orderedReadings = readings
            .OrderBy(item => item.EventTime)
            .ThenBy(item => item.EventId)
            .ToArray();
        var latest = orderedReadings[^1];
        var metrics = RiskInputMetricSet.FromReadings(orderedReadings).Merge(dailyCellState);
        var sourceReadings = orderedReadings
            .Select(RiskInputSourceReading.FromNormalizedReading)
            .ToArray();
        var qualityFlags = MergeQualityFlags(
            orderedReadings.SelectMany(item => item.QualityFlags).ToArray(),
            eligibility.QualityFlags);
        var classifierResults = ResolveClassifierResults(
            orderedReadings.SelectMany(item => item.ClassifierResults).ToArray(),
            eligibility.ClassifierResults);
        var inputStatus = ResolveWindowStatus(eligibility, metrics);
        var confidence = ResolveWindowConfidence(eligibility.ObservationalConfidence, inputStatus, metrics);
        var integrity = ResolveWindowIntegrity(eligibility.OperationalIntegrity, inputStatus, metrics);
        var effectiveQualityFlags = dailyCellState is null
            ? MergeQualityFlags(qualityFlags, [MissingDailyCellStateFlag])
            : qualityFlags;
        if (metrics.V1MetricCount <= 1)
        {
            effectiveQualityFlags = MergeQualityFlags(effectiveQualityFlags, [LowCoverageFlag]);
        }

        var input = new RiskInput(
            AreaId: latest.AreaId,
            SensorId: latest.SensorId,
            SourceEventId: latest.EventId,
            MetricType: latest.MetricType,
            Value: latest.Value,
            Unit: latest.Unit,
            EventTime: latest.EventTime)
        {
            SimulationRunId = simulationRunId,
            GridCellId = dailyCellState?.GridCellId ?? gridCellId,
            ConfigurationVersionId = dailyCellState?.ConfigurationVersionId ?? configurationVersionId,
            ValidFrom = orderedReadings[0].EventTime,
            ValidTo = orderedReadings[^1].EventTime,
            SourceReadings = sourceReadings,
            Metrics = metrics,
            DailyCellState = dailyCellState,
            DailyCellStateStatus = dailyCellState is null
                ? DailyCellStateStatus.Missing
                : DailyCellStateStatus.Present,
            TerritorialContext = territorialContext ??
                TerritorialRiskContext.Unknown(dailyCellState?.GridCellId ?? gridCellId),
            FireWeatherIndexContext = dailyCellState is null
                ? FireWeatherIndexContext.Absent
                : new FireWeatherIndexContext(
                    dailyCellState.FireWeatherIndex,
                    dailyCellState.KeetchByramDroughtIndex,
                    dailyCellState.FireIndexProvenance,
                    FineFuelMoistureCode: dailyCellState.FineFuelMoistureCode,
                    DuffMoistureCode: dailyCellState.DuffMoistureCode,
                    DroughtCode: dailyCellState.DroughtCode,
                    InitialSpreadIndex: dailyCellState.InitialSpreadIndex,
                    BuildupIndex: dailyCellState.BuildupIndex,
                    NormalizedFireWeatherIndex: dailyCellState.NormalizedFireWeatherIndex,
                    PreviousKeetchByramDroughtIndex: dailyCellState.PreviousKeetchByramDroughtIndex,
                    NormalizedKeetchByramDroughtIndex: dailyCellState.NormalizedKeetchByramDroughtIndex,
                    CalculationStatus: dailyCellState.FireWeatherCalculationStatus,
                    KbdiStatus: dailyCellState.KbdiCalculationStatus,
                    Limitations: MergeLimitations(dailyCellState.FireWeatherLimitations, dailyCellState.KbdiLimitations)),
            InputStatus = inputStatus,
            EligibilityReason = inputStatus == RiskInputStatus.Blocked
                ? RiskEligibilityReason.MissingRequiredValue
                : eligibility.ReasonCode,
            ObservationalConfidence = confidence,
            OperationalIntegrity = integrity,
            QualityFlags = effectiveQualityFlags,
            ClassifierResults = classifierResults
        };

        return input;
    }

    private static RiskInput CreateBaseInput(NormalizedReading reading)
    {
        return new RiskInput(
            AreaId: reading.AreaId,
            SensorId: reading.SensorId,
            SourceEventId: reading.EventId,
            MetricType: reading.MetricType,
            Value: reading.Value,
            Unit: reading.Unit,
            EventTime: reading.EventTime)
        {
            SourceReadings = [RiskInputSourceReading.FromNormalizedReading(reading)],
            Metrics = RiskInputMetricSet.FromReading(reading.MetricType, reading.Unit, reading.Value)
        };
    }

    private static IReadOnlyList<string> MergeQualityFlags(
        IReadOnlyList<string>? readingFlags,
        IReadOnlyList<string>? eligibilityFlags)
    {
        if ((readingFlags is null || readingFlags.Count == 0) &&
            (eligibilityFlags is null || eligibilityFlags.Count == 0))
        {
            return EmptyQualityFlags;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<string>();

        AppendFlags(merged, seen, readingFlags);
        AppendFlags(merged, seen, eligibilityFlags);

        return merged.Count == 0
            ? EmptyQualityFlags
            : merged;
    }

    private static IReadOnlyList<ClassifierResult> ResolveClassifierResults(
        IReadOnlyList<ClassifierResult>? readingResults,
        IReadOnlyList<ClassifierResult>? eligibilityResults)
    {
        if (eligibilityResults is { Count: > 0 })
        {
            return eligibilityResults;
        }

        if (readingResults is { Count: > 0 })
        {
            return readingResults;
        }

        return EmptyClassifierResults;
    }

    private static void AppendFlags(
        List<string> target,
        HashSet<string> seen,
        IReadOnlyList<string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return;
        }

        foreach (var flag in source)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                continue;
            }

            var normalizedFlag = flag.Trim();
            if (seen.Add(normalizedFlag))
            {
                target.Add(normalizedFlag);
            }
        }
    }

    private static RiskInputStatus ResolveWindowStatus(
        RiskEligibilityResult eligibility,
        RiskInputMetricSet metrics)
    {
        if (eligibility.Status == RiskInputStatus.Blocked || !eligibility.IsEligible)
        {
            return RiskInputStatus.Blocked;
        }

        if (metrics.IsCompleteV1)
        {
            return RiskInputStatus.CompleteEligible;
        }

        if (metrics.V1MetricCount >= 1)
        {
            return RiskInputStatus.PartialButUsable;
        }

        return RiskInputStatus.Blocked;
    }

    private static ObservationalConfidenceLevel ResolveWindowConfidence(
        ObservationalConfidenceLevel current,
        RiskInputStatus status,
        RiskInputMetricSet metrics)
    {
        if (status == RiskInputStatus.Blocked || metrics.V1MetricCount <= 1)
        {
            return ObservationalConfidenceLevel.Low;
        }

        if (status == RiskInputStatus.PartialButUsable && current == ObservationalConfidenceLevel.High)
        {
            return ObservationalConfidenceLevel.Medium;
        }

        return current;
    }

    private static OperationalIntegrityLevel ResolveWindowIntegrity(
        OperationalIntegrityLevel current,
        RiskInputStatus status,
        RiskInputMetricSet metrics)
    {
        if (status == RiskInputStatus.Blocked || metrics.V1MetricCount <= 1)
        {
            return OperationalIntegrityLevel.Compromised;
        }

        if (status == RiskInputStatus.PartialButUsable && current == OperationalIntegrityLevel.Intact)
        {
            return OperationalIntegrityLevel.Degraded;
        }

        return current;
    }

    private static string? MergeLimitations(string? first, string? second)
    {
        var values = new[] { first, second }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .SelectMany(item => item!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return values.Length == 0 ? null : string.Join(";", values);
    }
}

public enum DailyCellStateStatus
{
    NotEvaluated = 0,
    Present = 1,
    Missing = 2
}
