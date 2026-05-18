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
    public const int PersistenceCycles = 2;

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

    public static V1AlertDecision EvaluateTransition(
        V1AlertState currentState,
        double adjustedScore,
        V1AlertState pendingState,
        int pendingCycles,
        DateTimeOffset evaluatedAt,
        DateTimeOffset? cooldownUntil,
        TimeSpan interval)
    {
        var immediateState = EvaluateTransition(currentState, adjustedScore);

        if (currentState is V1AlertState.Warning or V1AlertState.Alarm)
        {
            var nextCooldown = immediateState == V1AlertState.None
                ? evaluatedAt.Add(ResolveCooldown(interval))
                : cooldownUntil;
            return new V1AlertDecision(immediateState, V1AlertState.None, 0, nextCooldown);
        }

        if (immediateState == V1AlertState.None)
        {
            return new V1AlertDecision(V1AlertState.None, V1AlertState.None, 0, cooldownUntil);
        }

        if (cooldownUntil.HasValue && evaluatedAt < cooldownUntil.Value)
        {
            return new V1AlertDecision(V1AlertState.None, immediateState, 0, cooldownUntil);
        }

        var nextPendingCycles = pendingState == immediateState
            ? pendingCycles + 1
            : 1;
        var nextState = nextPendingCycles >= PersistenceCycles
            ? immediateState
            : V1AlertState.None;
        var nextPendingState = nextState == V1AlertState.None ? immediateState : V1AlertState.None;
        var retainedPendingCycles = nextState == V1AlertState.None ? nextPendingCycles : 0;

        return new V1AlertDecision(nextState, nextPendingState, retainedPendingCycles, cooldownUntil);
    }

    public static TimeSpan ResolveCooldown(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(Math.Max(3 * interval.TotalSeconds, 180));
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

internal sealed record V1AlertDecision(
    V1AlertState State,
    V1AlertState PendingState,
    int PendingCycles,
    DateTimeOffset? CooldownUntil);
