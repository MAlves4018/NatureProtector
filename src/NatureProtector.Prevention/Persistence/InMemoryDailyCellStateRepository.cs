using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed class InMemoryDailyCellStateRepository : IDailyCellStateRepository
{
    private static readonly IFireWeatherIndexCalculator FireWeatherIndexCalculator =
        new CanadianFireWeatherIndexCalculator();
    private static readonly IKbdiCalculator KbdiCalculator = new CandidateKbdiCalculator();

    private readonly Dictionary<StateKey, DailyCellState> _states = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<DailyCellStateLookupResult> GetForReadingAsync(
        NormalizedReading reading,
        Guid? simulationRunId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var key = StateKey.From(reading.AreaId, reading.SensorId, NormalizeDay(reading.EventTime), simulationRunId);
            return new DailyCellStateLookupResult(
                _states.TryGetValue(key, out var state) ? state : null,
                GridCellId: null,
                ConfigurationVersionId: null,
                TerritorialContext: TerritorialRiskContext.Unknown(null));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpsertAsync(
        RiskInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var stateScopeId = input.GridCellId ?? input.SensorId;
            var key = StateKey.From(input.AreaId, stateScopeId, NormalizeDay(input.EventTime), input.SimulationRunId);
            var next = _states.TryGetValue(key, out var existing)
                ? existing.ApplyRiskInput(input)
                : DailyCellState.FromRiskInput(
                    input,
                    antecedentState: "runtime-observed",
                    candidateParameterSetVersion: CandidateParameterSetV1.Version,
                    provenance: "prevention_pipeline");

            var updated = ApplyFireWeatherIndex(ApplyKbdi(next));
            _states[key] = existing is null ? MarkFirstDailyKbdiAsLimited(updated) : updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static DateTimeOffset NormalizeDay(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private static DailyCellState ApplyFireWeatherIndex(DailyCellState state)
    {
        if (state.FireWeatherIndex.HasValue &&
            (state.FireIndexProvenance.Contains("import", StringComparison.OrdinalIgnoreCase) ||
             state.FireIndexProvenance.Contains("reference", StringComparison.OrdinalIgnoreCase)))
        {
            return state;
        }

        var result = FireWeatherIndexCalculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: state.MaxTemperatureCelsius,
            RelativeHumidityPercent: state.LatestHumidityPercent,
            WindSpeedMetersPerSecond: state.LatestWindSpeedMetersPerSecond,
            Precipitation24hMillimeters: state.DailyPrecipitationMillimeters,
            Month: state.Day.Month,
            PreviousFineFuelMoistureCode: state.FineFuelMoistureCode,
            PreviousDuffMoistureCode: state.DuffMoistureCode,
            PreviousDroughtCode: state.DroughtCode));

        return result.Status == FireWeatherIndexCalculationStatus.Complete || !state.FireWeatherIndex.HasValue
            ? state.WithFireWeatherIndex(result)
            : state;
    }

    private static DailyCellState ApplyKbdi(DailyCellState state)
    {
        if (state.KeetchByramDroughtIndex.HasValue &&
            (state.FireIndexProvenance.Contains("import", StringComparison.OrdinalIgnoreCase) ||
             state.FireIndexProvenance.Contains("reference", StringComparison.OrdinalIgnoreCase)))
        {
            return state;
        }

        var result = KbdiCalculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: state.MaxTemperatureCelsius,
            Precipitation24hMillimeters: state.DailyPrecipitationMillimeters,
            PreviousKeetchByramDroughtIndex: state.PreviousKeetchByramDroughtIndex));
        result = MarkLimitedAntecedentHistoryForFirstDailyCalculation(state, result);

        return IsUsableKbdi(result.Status) && !state.KeetchByramDroughtIndex.HasValue
            ? state.WithKbdi(result)
            : state;
    }

    private static KbdiResult MarkLimitedAntecedentHistoryForFirstDailyCalculation(
        DailyCellState state,
        KbdiResult result)
    {
        if (state.KeetchByramDroughtIndex.HasValue ||
            result.KeetchByramDroughtIndex is null ||
            result.Status is KbdiCalculationStatus.Missing or KbdiCalculationStatus.Partial)
        {
            return result;
        }

        var limitations = result.Limitations
            .Append("limited_antecedent_history")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new KbdiResult(
            KbdiCalculationStatus.LimitedAntecedentHistory,
            result.InputCompleteness,
            result.PreviousKeetchByramDroughtIndex,
            result.KeetchByramDroughtIndex,
            result.NormalizedKeetchByramDroughtIndex,
            result.Provenance,
            limitations);
    }

    private static bool IsUsableKbdi(KbdiCalculationStatus status)
    {
        return status is KbdiCalculationStatus.Complete or
            KbdiCalculationStatus.CompleteWithCandidateDefaults or
            KbdiCalculationStatus.LimitedAntecedentHistory or
            KbdiCalculationStatus.CalculatedFromHistory or
            KbdiCalculationStatus.ReferenceImported;
    }

    private static DailyCellState MarkFirstDailyKbdiAsLimited(DailyCellState state)
    {
        if (!state.KeetchByramDroughtIndex.HasValue ||
            state.KbdiCalculationStatus != KbdiCalculationStatus.Complete)
        {
            return state;
        }

        return state.WithKbdi(new KbdiResult(
            KbdiCalculationStatus.LimitedAntecedentHistory,
            1.0,
            state.PreviousKeetchByramDroughtIndex,
            state.KeetchByramDroughtIndex,
            state.NormalizedKeetchByramDroughtIndex,
            "candidate_kbdi_calculator",
            (state.KbdiLimitations ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Append("limited_antecedent_history")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()));
    }

    private readonly record struct StateKey(
        Guid AreaId,
        Guid StateScopeId,
        DateTimeOffset Day,
        Guid? SimulationRunId)
    {
        public static StateKey From(Guid areaId, Guid sensorId, DateTimeOffset day, Guid? simulationRunId)
            => new(areaId, sensorId, day, simulationRunId);
    }
}
