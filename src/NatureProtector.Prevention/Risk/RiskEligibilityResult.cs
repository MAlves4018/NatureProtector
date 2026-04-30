namespace NatureProtector.Prevention.Risk;

public sealed class RiskEligibilityResult
{
    private RiskEligibilityResult(
        bool isEligible,
        RiskEligibilityReason reasonCode,
        string? message)
    {
        IsEligible = isEligible;
        ReasonCode = reasonCode;
        Message = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();
    }

    public bool IsEligible { get; }

    public RiskEligibilityReason ReasonCode { get; }

    public string? Message { get; }

    public static RiskEligibilityResult Eligible { get; } =
        new(true, RiskEligibilityReason.Eligible, null);

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

        return new RiskEligibilityResult(false, reasonCode, message);
    }

    public static RiskEligibilityResult Ineligible(
        RiskEligibilityReason reasonCode,
        string? message = null)
    {
        return NotEligible(reasonCode, message);
    }
}
