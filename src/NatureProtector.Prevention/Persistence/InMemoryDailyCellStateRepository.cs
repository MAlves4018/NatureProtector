using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Persistence;

public sealed class InMemoryDailyCellStateRepository : IDailyCellStateRepository
{
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
                ConfigurationVersionId: null);
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
            var key = StateKey.From(input.AreaId, input.SensorId, NormalizeDay(input.EventTime), input.SimulationRunId);
            var next = _states.TryGetValue(key, out var existing)
                ? existing.ApplyRiskInput(input)
                : DailyCellState.FromRiskInput(
                    input,
                    antecedentState: "runtime-observed",
                    candidateParameterSetVersion: "Candidate Parameter Set V1.0",
                    provenance: "prevention_pipeline");

            _states[key] = next;
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

    private readonly record struct StateKey(
        Guid AreaId,
        Guid SensorId,
        DateTimeOffset Day,
        Guid? SimulationRunId)
    {
        public static StateKey From(Guid areaId, Guid sensorId, DateTimeOffset day, Guid? simulationRunId)
            => new(areaId, sensorId, day, simulationRunId);
    }
}
