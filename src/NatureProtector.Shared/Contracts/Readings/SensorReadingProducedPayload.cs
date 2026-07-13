namespace NatureProtector.Shared.Contracts.Readings;

public enum MetricOrigin
{
    Observed = 0,
    Reference = 1,
    CarriedForward = 2,
    Missing = 3,
    Blocked = 4
}

public sealed record SensorReadingProducedPayload(
    Guid SimulationRunId,
    Guid SensorId,
    string SensorName,
    SensorMetricType MetricType,
    MeasurementUnit Unit,
    double Value,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState,
    int? CycleIndex = null,
    string? GridCellId = null,
    MetricOrigin Origin = MetricOrigin.Observed
    );
