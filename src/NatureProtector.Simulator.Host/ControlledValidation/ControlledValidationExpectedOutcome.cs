namespace NatureProtector.Simulator.Host.ControlledValidation;

public enum ControlledValidationExpectedOutcome
{
    Rejected = 0,
    Quarantined = 1,
    Accepted = 2,
    RetryThenSuccess = 3,
    CoverageGap = 4,
    IdempotentDuplicate = 5,
    ValueDegraded = 6,
    BlockedEligibility = 7,
    TemporalQuality = 8
}
