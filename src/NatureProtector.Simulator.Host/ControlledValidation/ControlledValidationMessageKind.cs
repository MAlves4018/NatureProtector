namespace NatureProtector.Simulator.Host.ControlledValidation;

public enum ControlledValidationMessageKind
{
    RawInvalidJson = 0,
    EnvelopeWithoutPayload = 1,
    EnvelopeWithPayload = 2
}
