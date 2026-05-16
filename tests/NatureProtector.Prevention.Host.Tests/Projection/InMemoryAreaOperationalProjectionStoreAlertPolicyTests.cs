using NatureProtector.Core.Risk;
using NatureProtector.Prevention.Host.Projection;

namespace NatureProtector.Prevention.Host.Tests.Projection;

public sealed class InMemoryAreaOperationalProjectionStoreAlertPolicyTests
{
    [Fact]
    public async Task SaveAsync_TransitionsFromNoneToWarningAndAlarm()
    {
        var store = new InMemoryAreaOperationalProjectionStore();
        var areaId = Guid.NewGuid();

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.60, "Warning open"),
            assessmentCount: 2,
            CancellationToken.None);

        var warningAlert = Assert.Single(store.Alerts);
        Assert.Equal("Open", warningAlert.Status);
        Assert.Equal("Warning", warningAlert.AlertState);

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.80, "Alarm open"),
            assessmentCount: 2,
            CancellationToken.None);

        var alarmAlert = Assert.Single(store.Alerts);
        Assert.Equal("Open", alarmAlert.Status);
        Assert.Equal("Alarm", alarmAlert.AlertState);
    }

    [Fact]
    public async Task SaveAsync_AppliesHysteresis_ForAlarmAndWarning()
    {
        var store = new InMemoryAreaOperationalProjectionStore();
        var areaId = Guid.NewGuid();

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.82, "Alarm open"),
            assessmentCount: 3,
            CancellationToken.None);

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.72, "Alarm remains open above close threshold"),
            assessmentCount: 3,
            CancellationToken.None);

        var alarmStillOpen = Assert.Single(store.Alerts);
        Assert.Equal("Alarm", alarmStillOpen.AlertState);
        Assert.Equal("Open", alarmStillOpen.Status);

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.65, "Alarm de-escalates to warning"),
            assessmentCount: 3,
            CancellationToken.None);

        var downgradedToWarning = Assert.Single(store.Alerts);
        Assert.Equal("Warning", downgradedToWarning.AlertState);
        Assert.Equal("Open", downgradedToWarning.Status);

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.55, "Warning remains open above close threshold"),
            assessmentCount: 3,
            CancellationToken.None);

        var warningStillOpen = Assert.Single(store.Alerts);
        Assert.Equal("Warning", warningStillOpen.AlertState);
        Assert.Equal("Open", warningStillOpen.Status);
    }

    [Fact]
    public async Task SaveAsync_ResolvesAlert_WhenScoreDropsBelowWarningCloseThreshold()
    {
        var store = new InMemoryAreaOperationalProjectionStore();
        var areaId = Guid.NewGuid();

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.62, "Warning open"),
            assessmentCount: 1,
            CancellationToken.None);

        await store.SaveAsync(
            areaId,
            CreateSnapshot(0.49, "Close warning"),
            assessmentCount: 1,
            CancellationToken.None);

        var resolved = Assert.Single(store.Alerts);
        Assert.Equal("Resolved", resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);
    }

    private static AreaRiskSnapshot CreateSnapshot(double score, string summary)
    {
        return new AreaRiskSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            score,
            summary);
    }
}
