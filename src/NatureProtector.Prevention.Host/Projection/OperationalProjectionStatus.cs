using NatureProtector.Core.Risk;
using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Host.Projection;

public static class OperationalProjectionStatus
{
    public const string Complete = "Complete";
    public const string Partial = "Partial";
    public const string LowCoverage = "LowCoverage";
    public const string Blocked = "Blocked";
    public const string NoRecentData = "NoRecentData";

    public const string Fresh = "Fresh";
    public const string Stale = "Stale";
    public const string Expired = "Expired";

    public const string Current = "Current";
    public const string CarriedForward = "CarriedForward";
    public const string ExpiredCarryForward = "ExpiredCarryForward";
    public const string NotAvailable = "NotAvailable";

    public static string ResolveCoverage(int assessmentCount)
    {
        return assessmentCount switch
        {
            <= 0 => NoRecentData,
            1 => LowCoverage,
            2 => Partial,
            _ => Complete
        };
    }

    public static string ResolveCoverage(RiskAssessment assessment)
    {
        if (string.Equals(assessment.CalculationStatus, RiskInputStatus.PartialButUsable.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Partial;
        }

        if (assessment.Limitations?.Contains("low_coverage", StringComparison.OrdinalIgnoreCase) == true)
        {
            return LowCoverage;
        }

        return Complete;
    }

    public static string ResolveFreshness(DateTimeOffset snapshotTimestamp, DateTimeOffset observedAt, int intervalSeconds = 60)
    {
        var ageSeconds = Math.Max(0, (observedAt - snapshotTimestamp).TotalSeconds);
        var freshThreshold = CandidateParameterSetV1.ResolveStaleThreshold(TimeSpan.FromSeconds(intervalSeconds)).TotalSeconds;
        var expiredThreshold = freshThreshold * 2;

        if (ageSeconds <= freshThreshold)
        {
            return Fresh;
        }

        return ageSeconds <= expiredThreshold ? Stale : Expired;
    }

    public static string ResolveCarryForward(string freshness)
        => freshness switch
        {
            Fresh => Current,
            Stale => CarriedForward,
            Expired => ExpiredCarryForward,
            _ => NotAvailable
        };
}
