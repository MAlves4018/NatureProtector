using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Readings;

public sealed record NormalizedReading(
    Guid EventId,
    string CorrelationId,
    Guid AreaId,
    Guid SensorId,
    string SensorName,
    SensorMetricType MetricType,
    double Value,
    MeasurementUnit Unit,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState,
    DateTimeOffset EventTime,
    DateTimeOffset? IngestTime,
    int? CycleIndex = null,
    string? GridCellId = null)
{
    private static readonly IReadOnlyList<string> EmptyQualityFlags = Array.Empty<string>();
    private static readonly IReadOnlyList<ClassifierResult> EmptyClassifierResults = Array.Empty<ClassifierResult>();

    public IReadOnlyList<string> QualityFlags { get; init; } = EmptyQualityFlags;

    public IReadOnlyList<ClassifierResult> ClassifierResults { get; init; } = EmptyClassifierResults;

    public static NormalizedReading FromEnvelope(EventEnvelope<SensorReadingProducedPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return FromOperationalEvent(OperationalEvent.FromEnvelope(envelope));
    }

    public static NormalizedReading FromOperationalEvent(OperationalEvent operationalEvent)
    {
        ArgumentNullException.ThrowIfNull(operationalEvent);

        return new NormalizedReading(
            EventId: operationalEvent.EventId,
            CorrelationId: operationalEvent.CorrelationId,
            AreaId: operationalEvent.AreaId,
            SensorId: operationalEvent.SensorId,
            SensorName: operationalEvent.SensorName,
            MetricType: operationalEvent.MetricType,
            Value: operationalEvent.Value,
            Unit: operationalEvent.Unit,
            Latitude: operationalEvent.Latitude,
            Longitude: operationalEvent.Longitude,
            OperationalState: operationalEvent.OperationalState,
            EventTime: operationalEvent.EventTime,
            IngestTime: operationalEvent.IngestTime,
            CycleIndex: operationalEvent.CycleIndex,
            GridCellId: operationalEvent.GridCellId)
        {
            QualityFlags = operationalEvent.QualityFlags ?? EmptyQualityFlags,
            ClassifierResults = operationalEvent.ClassifierResults ?? EmptyClassifierResults
        };
    }
}
