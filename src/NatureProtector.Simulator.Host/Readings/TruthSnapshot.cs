using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Simulator.Host.Readings;

public sealed record TruthSnapshot(
    Guid Id,
    Guid SimulationRunId,
    Guid ScenarioId,
    string? ScenarioCode,
    Guid AreaId,
    Guid SensorId,
    string SensorName,
    string? GridCellId,
    int CycleIndex,
    DateTimeOffset LogicalTimestamp,
    SensorMetricType MetricType,
    MeasurementUnit Unit,
    double PhysicalValue);
