namespace NatureProtector.Prevention.Risk;

public enum RiskEligibilityReason
{
    Eligible = 0,
    UnsupportedMetric = 1,
    MissingRequiredValue = 2,
    InvalidUnit = 3,
    InvalidOperationalState = 4,
    DelayedReading = 5,
    RetransmittedReading = 6,
    DegradedButUsable = 7
}
