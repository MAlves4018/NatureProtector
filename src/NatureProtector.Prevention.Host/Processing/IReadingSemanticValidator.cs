using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public interface IReadingSemanticValidator
{
    Task<ReadingSemanticValidationResult> ValidateAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        CancellationToken cancellationToken);
}

public enum ReadingSemanticValidationReason
{
    None = 0,
    SensorNotFound = 1,
    SensorInactive = 2,
    SensorAreaMismatch = 3
}

public sealed record ReadingSemanticValidationResult(
    bool IsValid,
    ReadingSemanticValidationReason Reason,
    string? Message)
{
    public static readonly ReadingSemanticValidationResult Valid = new(
        IsValid: true,
        Reason: ReadingSemanticValidationReason.None,
        Message: null);

    public string ReasonCode => Reason switch
    {
        ReadingSemanticValidationReason.None => string.Empty,
        ReadingSemanticValidationReason.SensorNotFound => "sensor_not_found",
        ReadingSemanticValidationReason.SensorInactive => "sensor_inactive",
        ReadingSemanticValidationReason.SensorAreaMismatch => "sensor_area_mismatch",
        _ => "semantic_validation_failed"
    };

    public static ReadingSemanticValidationResult Invalid(
        ReadingSemanticValidationReason reason,
        string message)
    {
        return new ReadingSemanticValidationResult(
            IsValid: false,
            Reason: reason,
            Message: message);
    }
}
