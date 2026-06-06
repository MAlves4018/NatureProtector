namespace NatureProtector.Prevention.Host.Processing;

public sealed class ControlledValidationProcessingFaultException(
    ProcessingFailureKind kind,
    string errorCode,
    string message) : Exception(message)
{
    public ProcessingFailureKind Kind { get; } = kind;

    public string ErrorCode { get; } = errorCode;
}
