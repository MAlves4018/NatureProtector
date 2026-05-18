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

    public string ParameterSetVersion { get; init; } = "Candidate Parameter Set V1.0";

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
        Guid? configurationVersionId)
    {
        var baseInput = FromNormalizedReading(reading, eligibility);
        var input = baseInput with
        {
            SimulationRunId = simulationRunId,
            GridCellId = dailyCellState?.GridCellId ?? gridCellId,
            ConfigurationVersionId = dailyCellState?.ConfigurationVersionId ?? configurationVersionId,
            Metrics = baseInput.Metrics.Merge(dailyCellState),
            DailyCellState = dailyCellState,
            DailyCellStateStatus = dailyCellState is null
                ? DailyCellStateStatus.Missing
                : DailyCellStateStatus.Present,
            TerritorialContext = TerritorialRiskContext.Unknown(dailyCellState?.GridCellId ?? gridCellId),
            FireWeatherIndexContext = dailyCellState is null
                ? FireWeatherIndexContext.Absent
                : new FireWeatherIndexContext(
                    dailyCellState.FireWeatherIndex,
                    dailyCellState.KeetchByramDroughtIndex,
                    dailyCellState.FireIndexProvenance)
        };

        return dailyCellState is null
            ? input with
            {
                QualityFlags = MergeQualityFlags(input.QualityFlags, [MissingDailyCellStateFlag])
            }
            : input;
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
}

public enum DailyCellStateStatus
{
    NotEvaluated = 0,
    Present = 1,
    Missing = 2
}
