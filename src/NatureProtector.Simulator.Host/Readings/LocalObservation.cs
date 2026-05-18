using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Simulator.Host.Readings;

public sealed record LocalObservation(
    Guid Id,
    Guid TruthSnapshotId,
    TruthSnapshot TruthSnapshot,
    double ObservedValue,
    double Latitude,
    double Longitude,
    SensorOperationalState OperationalState,
    string? DegradationProfile,
    bool IsMissing)
{
    public SensorReadingProducedPayload ToPayload()
    {
        if (IsMissing)
        {
            throw new InvalidOperationException(
                "Missing local observations must not be converted into published sensor reading payloads.");
        }

        return new SensorReadingProducedPayload(
            SimulationRunId: TruthSnapshot.SimulationRunId,
            SensorId: TruthSnapshot.SensorId,
            SensorName: TruthSnapshot.SensorName,
            MetricType: TruthSnapshot.MetricType,
            Unit: TruthSnapshot.Unit,
            Value: ObservedValue,
            Latitude: Latitude,
            Longitude: Longitude,
            OperationalState: OperationalState);
    }

    public LocalObservation AsMissing(string degradationProfile)
    {
        if (string.IsNullOrWhiteSpace(degradationProfile))
        {
            throw new ArgumentException(
                "Degradation profile must identify why the observation became missing.",
                nameof(degradationProfile));
        }

        return this with
        {
            DegradationProfile = degradationProfile.Trim(),
            IsMissing = true
        };
    }
}
