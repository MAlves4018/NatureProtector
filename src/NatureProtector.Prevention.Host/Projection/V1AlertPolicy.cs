namespace NatureProtector.Prevention.Host.Projection;

internal enum V1AlertState
{
    None = 0,
    Warning = 1,
    Alarm = 2
}

internal static class V1AlertPolicy
{
    public const double WarningOpenThreshold = 0.60;
    public const double WarningCloseThreshold = 0.50;
    public const double AlarmOpenThreshold = 0.80;
    public const double AlarmCloseThreshold = 0.70;

    public static V1AlertState InferCurrentState(
        bool hasOpenAlert,
        double previousAdjustedScore)
    {
        ValidateScore(previousAdjustedScore, nameof(previousAdjustedScore));

        if (!hasOpenAlert)
        {
            return V1AlertState.None;
        }

        return previousAdjustedScore >= AlarmCloseThreshold
            ? V1AlertState.Alarm
            : V1AlertState.Warning;
    }

    public static V1AlertState EvaluateTransition(
        V1AlertState currentState,
        double adjustedScore)
    {
        ValidateScore(adjustedScore, nameof(adjustedScore));

        return currentState switch
        {
            V1AlertState.None => adjustedScore switch
            {
                >= AlarmOpenThreshold => V1AlertState.Alarm,
                >= WarningOpenThreshold => V1AlertState.Warning,
                _ => V1AlertState.None
            },

            V1AlertState.Warning => adjustedScore switch
            {
                >= AlarmOpenThreshold => V1AlertState.Alarm,
                < WarningCloseThreshold => V1AlertState.None,
                _ => V1AlertState.Warning
            },

            V1AlertState.Alarm => adjustedScore switch
            {
                >= AlarmCloseThreshold => V1AlertState.Alarm,
                < WarningCloseThreshold => V1AlertState.None,
                _ => V1AlertState.Warning
            },

            _ => V1AlertState.None
        };
    }

    private static void ValidateScore(double score, string paramName)
    {
        if (double.IsNaN(score) || double.IsInfinity(score))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                score,
                "Adjusted score must be finite.");
        }

        if (score is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                score,
                "Adjusted score must be in the range [0, 1].");
        }
    }
}
