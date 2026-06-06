namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed record ControlledValidationMessage(
    ValidationFaultCase FaultCase,
    int Sequence,
    ControlledValidationMessageKind Kind,
    Guid? EventId,
    string? CorrelationId,
    byte[] Body,
    string BodySha256,
    bool IsSetupMessage);
