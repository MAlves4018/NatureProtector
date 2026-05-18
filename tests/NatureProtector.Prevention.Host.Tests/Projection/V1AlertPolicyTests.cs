using NatureProtector.Prevention.Host.Projection;

namespace NatureProtector.Prevention.Host.Tests.Projection;

public sealed class V1AlertPolicyTests
{
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InferCurrentState_NonFinitePreviousScore_ThrowsArgumentOutOfRangeException(double score)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            V1AlertPolicy.InferCurrentState(hasOpenAlert: true, previousAdjustedScore: score));

        Assert.Equal("previousAdjustedScore", exception.ParamName);
        Assert.Contains("Adjusted score must be finite.", exception.Message);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void EvaluateTransition_ScoreOutsideUnitRange_ThrowsArgumentOutOfRangeException(double score)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            V1AlertPolicy.EvaluateTransition(V1AlertState.None, score));

        Assert.Equal("adjustedScore", exception.ParamName);
        Assert.Contains("Adjusted score must be in the range [0, 1].", exception.Message);
    }

    [Fact]
    public void InferCurrentState_NoOpenAlert_ReturnsNone()
    {
        var state = V1AlertPolicy.InferCurrentState(
            hasOpenAlert: false,
            previousAdjustedScore: V1AlertPolicy.AlarmOpenThreshold);

        Assert.Equal(V1AlertState.None, state);
    }

    [Theory]
    [InlineData(0.69, 1)]
    [InlineData(0.70, 2)]
    public void InferCurrentState_OpenAlertUsesAlarmCloseThreshold_ReturnsExpectedState(
        double previousScore,
        int expectedState)
    {
        var state = V1AlertPolicy.InferCurrentState(
            hasOpenAlert: true,
            previousAdjustedScore: previousScore);

        Assert.Equal((V1AlertState)expectedState, state);
    }

    [Theory]
    [InlineData(0, 0.59, 0)]
    [InlineData(0, 0.60, 1)]
    [InlineData(0, 0.80, 2)]
    [InlineData(1, 0.49, 0)]
    [InlineData(1, 0.50, 1)]
    [InlineData(1, 0.80, 2)]
    [InlineData(2, 0.69, 1)]
    [InlineData(2, 0.70, 2)]
    [InlineData(2, 0.49, 0)]
    public void EvaluateTransition_BoundaryScore_ReturnsExpectedState(
        int currentState,
        double adjustedScore,
        int expectedState)
    {
        var state = V1AlertPolicy.EvaluateTransition((V1AlertState)currentState, adjustedScore);

        Assert.Equal((V1AlertState)expectedState, state);
    }

    [Fact]
    public void EvaluateTransition_UnknownCurrentState_ReturnsNone()
    {
        var state = V1AlertPolicy.EvaluateTransition((V1AlertState)99, 0.95);

        Assert.Equal(V1AlertState.None, state);
    }

    [Fact]
    public void EvaluateTransition_WithPersistence_DoesNotOpenOnSingleSpike()
    {
        var now = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

        var decision = V1AlertPolicy.EvaluateTransition(
            V1AlertState.None,
            adjustedScore: 0.85,
            pendingState: V1AlertState.None,
            pendingCycles: 0,
            evaluatedAt: now,
            cooldownUntil: null,
            interval: TimeSpan.FromSeconds(60));

        Assert.Equal(V1AlertState.None, decision.State);
        Assert.Equal(V1AlertState.Alarm, decision.PendingState);
        Assert.Equal(1, decision.PendingCycles);
    }

    [Fact]
    public void EvaluateTransition_WithPersistence_OpensOnSecondConsecutiveCycle()
    {
        var now = new DateTimeOffset(2026, 5, 18, 12, 1, 0, TimeSpan.Zero);

        var decision = V1AlertPolicy.EvaluateTransition(
            V1AlertState.None,
            adjustedScore: 0.85,
            pendingState: V1AlertState.Alarm,
            pendingCycles: 1,
            evaluatedAt: now,
            cooldownUntil: null,
            interval: TimeSpan.FromSeconds(60));

        Assert.Equal(V1AlertState.Alarm, decision.State);
        Assert.Equal(V1AlertState.None, decision.PendingState);
        Assert.Equal(0, decision.PendingCycles);
    }

    [Fact]
    public void EvaluateTransition_WithCooldown_SuppressesImmediateReopen()
    {
        var now = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

        var decision = V1AlertPolicy.EvaluateTransition(
            V1AlertState.None,
            adjustedScore: 0.85,
            pendingState: V1AlertState.None,
            pendingCycles: 0,
            evaluatedAt: now,
            cooldownUntil: now.AddSeconds(180),
            interval: TimeSpan.FromSeconds(60));

        Assert.Equal(V1AlertState.None, decision.State);
        Assert.Equal(0, decision.PendingCycles);
    }

    [Fact]
    public void EvaluateTransition_WhenAlertCloses_StartsCooldown()
    {
        var now = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

        var decision = V1AlertPolicy.EvaluateTransition(
            V1AlertState.Warning,
            adjustedScore: 0.49,
            pendingState: V1AlertState.None,
            pendingCycles: 0,
            evaluatedAt: now,
            cooldownUntil: null,
            interval: TimeSpan.FromSeconds(60));

        Assert.Equal(V1AlertState.None, decision.State);
        Assert.Equal(now.AddSeconds(180), decision.CooldownUntil);
    }
}
