namespace NatureProtector.Prevention.Risk;

public sealed class RiskEligibilityResult
{
    private static readonly IReadOnlyList<string> EmptyFlags = Array.Empty<string>();
    private static readonly IReadOnlyList<ClassifierResult> EmptyClassifierResults = Array.Empty<ClassifierResult>();

    private RiskEligibilityResult(
        RiskInputStatus status,
        IReadOnlyList<RiskEligibilityReason> reasons,
        string? message,
        ObservationalConfidenceLevel observationalConfidence,
        OperationalIntegrityLevel operationalIntegrity,
        IReadOnlyList<string> qualityFlags,
        IReadOnlyList<ClassifierResult> classifierResults)
    {
        Status = status;
        IsEligible = status != RiskInputStatus.Blocked;
        Reasons = reasons;
        ReasonCode = reasons.Count == 0
            ? RiskEligibilityReason.Eligible
            : reasons[0];
        Message = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();
        ObservationalConfidence = observationalConfidence;
        OperationalIntegrity = operationalIntegrity;
        QualityFlags = qualityFlags;
        ClassifierResults = classifierResults;
    }

    public bool IsEligible { get; }

    public RiskInputStatus Status { get; }

    public IReadOnlyList<RiskEligibilityReason> Reasons { get; }

    public RiskEligibilityReason ReasonCode { get; }

    public string? Message { get; }

    public ObservationalConfidenceLevel ObservationalConfidence { get; }

    public OperationalIntegrityLevel OperationalIntegrity { get; }

    public IReadOnlyList<string> QualityFlags { get; }

    public IReadOnlyList<ClassifierResult> ClassifierResults { get; }

    public static RiskEligibilityResult Eligible { get; } =
        CompleteEligible();

    public static RiskEligibilityResult CompleteEligible(
        string? message = null,
        IReadOnlyList<string>? qualityFlags = null,
        IReadOnlyList<ClassifierResult>? classifierResults = null)
    {
        return new RiskEligibilityResult(
            RiskInputStatus.CompleteEligible,
            [RiskEligibilityReason.Eligible],
            message,
            ObservationalConfidenceLevel.High,
            OperationalIntegrityLevel.Intact,
            qualityFlags ?? EmptyFlags,
            classifierResults ?? EmptyClassifierResults);
    }

    public static RiskEligibilityResult PartialButUsable(
        RiskEligibilityReason reasonCode,
        string? message = null,
        IReadOnlyList<string>? qualityFlags = null,
        IReadOnlyList<ClassifierResult>? classifierResults = null)
    {
        if (reasonCode == RiskEligibilityReason.Eligible)
        {
            throw new ArgumentException(
                "Partial results must include a non-eligible reason code.",
                nameof(reasonCode));
        }

        return new RiskEligibilityResult(
            RiskInputStatus.PartialButUsable,
            [reasonCode],
            message,
            ObservationalConfidenceLevel.Medium,
            OperationalIntegrityLevel.Degraded,
            qualityFlags ?? EmptyFlags,
            classifierResults ?? EmptyClassifierResults);
    }

    public static RiskEligibilityResult Blocked(
        RiskEligibilityReason reasonCode,
        string? message = null,
        IReadOnlyList<string>? qualityFlags = null,
        IReadOnlyList<ClassifierResult>? classifierResults = null)
    {
        if (reasonCode == RiskEligibilityReason.Eligible)
        {
            throw new ArgumentException(
                "Blocked results must include a non-eligible reason code.",
                nameof(reasonCode));
        }

        return new RiskEligibilityResult(
            RiskInputStatus.Blocked,
            [reasonCode],
            message,
            ObservationalConfidenceLevel.Low,
            OperationalIntegrityLevel.Compromised,
            qualityFlags ?? EmptyFlags,
            classifierResults ?? EmptyClassifierResults);
    }

    public static RiskEligibilityResult NotEligible(
        RiskEligibilityReason reasonCode,
        string? message = null)
    {
        if (reasonCode == RiskEligibilityReason.Eligible)
        {
            throw new ArgumentException(
                "Use the Eligible singleton for eligible results.",
                nameof(reasonCode));
        }

        return Blocked(reasonCode, message);
    }

    public static RiskEligibilityResult Ineligible(
        RiskEligibilityReason reasonCode,
        string? message = null)
    {
        return NotEligible(reasonCode, message);
    }
}

public enum ObservationalConfidenceLevel
{
    High = 0,
    Medium = 1,
    Low = 2
}

public enum OperationalIntegrityLevel
{
    Intact = 0,
    Degraded = 1,
    Compromised = 2
}
