namespace NatureProtector.Prevention.Host.Persistence;

public sealed class MissingSimulationRunReferenceException : InvalidOperationException
{
    public const string ErrorCode = "orphan_simulation_run_reference";

    public MissingSimulationRunReferenceException(
        Guid simulationRunId,
        Guid areaId,
        Guid sensorId,
        Guid sourceEventId)
        : base($"SimulationRunId '{simulationRunId}' is not present in control.simulation_runs for area '{areaId}', sensor '{sensorId}', source event '{sourceEventId}'.")
    {
        SimulationRunId = simulationRunId;
        AreaId = areaId;
        SensorId = sensorId;
        SourceEventId = sourceEventId;
    }

    public Guid SimulationRunId { get; }

    public Guid AreaId { get; }

    public Guid SensorId { get; }

    public Guid SourceEventId { get; }
}
