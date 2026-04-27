namespace NatureProtector.Shared.Contracts.Readings;

public sealed record SensorReadingProducedPayload(
    Guid SimulationRunId,
    Guid SensorId,
    string SensorName,
    SensorMetricType MetricType,
    MeasurementUnit Unit,
    double Value,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState
    );