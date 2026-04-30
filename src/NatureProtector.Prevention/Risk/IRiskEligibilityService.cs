using NatureProtector.Prevention.Readings;

namespace NatureProtector.Prevention.Risk;

public interface IRiskEligibilityService
{
    Task<RiskEligibilityResult> EvaluateAsync(
        NormalizedReading reading,
        CancellationToken cancellationToken);
}
