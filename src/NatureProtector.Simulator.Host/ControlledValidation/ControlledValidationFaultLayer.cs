namespace NatureProtector.Simulator.Host.ControlledValidation;

public enum ControlledValidationFaultLayer
{
    EventTransport = 0,
    Processing = 1,
    CoverageGap = 2,
    Idempotency = 3,
    ValueDegradation = 4,
    Eligibility = 5,
    TemporalQuality = 6
}
