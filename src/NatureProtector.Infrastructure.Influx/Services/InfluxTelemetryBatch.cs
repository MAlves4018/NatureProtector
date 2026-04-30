using NatureProtector.Core.Risk;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Infrastructure.Influx.Services;

public sealed class InfluxTelemetryBatch
{
    private readonly List<EventEnvelope<SensorReadingProducedPayload>> _acceptedReadings = [];
    private readonly List<InfluxRiskAssessmentWrite> _riskAssessments = [];
    private readonly List<InfluxAreaRiskSnapshotWrite> _areaRiskSnapshots = [];

    public IReadOnlyList<EventEnvelope<SensorReadingProducedPayload>> AcceptedReadings => _acceptedReadings;
    public IReadOnlyList<InfluxRiskAssessmentWrite> RiskAssessments => _riskAssessments;
    public IReadOnlyList<InfluxAreaRiskSnapshotWrite> AreaRiskSnapshots => _areaRiskSnapshots;

    public int AcceptedReadingCount => _acceptedReadings.Count;
    public int RiskAssessmentCount => _riskAssessments.Count;
    public int AreaRiskSnapshotCount => _areaRiskSnapshots.Count;
    public int PointCount => AcceptedReadingCount + RiskAssessmentCount + AreaRiskSnapshotCount;
    public bool IsEmpty => PointCount == 0;

    public InfluxTelemetryBatch AddAcceptedReading(EventEnvelope<SensorReadingProducedPayload> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _acceptedReadings.Add(envelope);
        return this;
    }

    public InfluxTelemetryBatch AddRiskAssessment(Guid areaId, Guid sensorId, RiskAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(assessment);
        _riskAssessments.Add(new InfluxRiskAssessmentWrite(areaId, sensorId, assessment));
        return this;
    }

    public InfluxTelemetryBatch AddAreaRiskSnapshot(Guid areaId, int assessmentCount, AreaRiskSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _areaRiskSnapshots.Add(new InfluxAreaRiskSnapshotWrite(areaId, assessmentCount, snapshot));
        return this;
    }

    public InfluxTelemetryBatch CloneFiltered(
        bool includeAcceptedReadings,
        bool includeRiskAssessments,
        bool includeAreaRiskSnapshots)
    {
        var filtered = new InfluxTelemetryBatch();

        if (includeAcceptedReadings)
        {
            foreach (var envelope in _acceptedReadings)
            {
                filtered.AddAcceptedReading(envelope);
            }
        }

        if (includeRiskAssessments)
        {
            foreach (var write in _riskAssessments)
            {
                filtered.AddRiskAssessment(write.AreaId, write.SensorId, write.Assessment);
            }
        }

        if (includeAreaRiskSnapshots)
        {
            foreach (var write in _areaRiskSnapshots)
            {
                filtered.AddAreaRiskSnapshot(write.AreaId, write.AssessmentCount, write.Snapshot);
            }
        }

        return filtered;
    }
}

public readonly record struct InfluxRiskAssessmentWrite(
    Guid AreaId,
    Guid SensorId,
    RiskAssessment Assessment);

public readonly record struct InfluxAreaRiskSnapshotWrite(
    Guid AreaId,
    int AssessmentCount,
    AreaRiskSnapshot Snapshot);
