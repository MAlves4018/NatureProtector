namespace NatureProtector.Prevention.Risk;

public enum KbdiCalculationStatus
{
    Missing = 0,
    Partial = 1,
    Complete = 2,
    CompleteWithCandidateDefaults = 3,
    LimitedAntecedentHistory = 4,
    CalculatedFromHistory = 5,
    ReferenceImported = 6
}
