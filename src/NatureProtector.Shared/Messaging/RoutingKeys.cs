namespace NatureProtector.Shared.Messaging;

public static class RoutingKeys
{
    public const string SensorReadingProduced = "simulation.reading.produced";
    public const string ReadingAccepted = "ingestion.reading.accepted";
    public const string ReadingRejected = "ingestion.reading.rejected";
    public const string ReadingNormalized = "ingestion.reading.normalized";
}