using NatureProtector.Simulator.Host.TemporalLoad;

namespace NatureProtector.Simulator.Host.Tests.TemporalLoad;

public sealed class TemporalLoadSchedulerTests
{
    [Theory]
    [InlineData(1.0, 10.0, 10)]
    [InlineData(0.7, 10.0, 7)]
    [InlineData(1.75, 8.0, 14)]
    [InlineData(2.25, 8.0, 18)]
    [InlineData(2.75, 8.0, 22)]
    public void Build_SupportsIntegerAndFractionalConstantRates(
        double rate,
        double durationSeconds,
        int expectedCount)
    {
        var workload = new TemporalWorkloadDefinition
        {
            Id = "fractional",
            Segments =
            [
                new TemporalWorkloadSegment
                {
                    Id = "steady",
                    Kind = "constant",
                    DurationSeconds = durationSeconds,
                    RequestedRate = rate
                }
            ]
        };

        var schedule = TemporalLoadScheduler.Build(workload);

        Assert.Equal(expectedCount, schedule.Entries.Count);
        Assert.Equal(TimeSpan.FromSeconds(durationSeconds), schedule.ActiveDuration);
        Assert.Equal(TimeSpan.Zero, schedule.Entries[0].DueOffset);
        Assert.Equal(TimeSpan.FromSeconds(1 / rate), schedule.Entries[1].DueOffset);
        Assert.All(schedule.Entries, entry => Assert.Equal(rate, entry.RequestedRate));
    }

    [Fact]
    public void Build_UsesMonotonicOffsetsAcrossSegmentsAndWarmUp()
    {
        var workload = new TemporalWorkloadDefinition
        {
            Id = "step",
            WarmUpSeconds = 2,
            Segments =
            [
                new TemporalWorkloadSegment { Id = "low", DurationSeconds = 4, RequestedRate = 1.0 },
                new TemporalWorkloadSegment { Id = "high", DurationSeconds = 2, RequestedRate = 2.0 }
            ]
        };

        var schedule = TemporalLoadScheduler.Build(workload);

        Assert.Equal(8, schedule.Entries.Count);
        Assert.Equal(TimeSpan.FromSeconds(6), schedule.ActiveDuration);
        Assert.Equal(TimeSpan.FromSeconds(2), schedule.Entries[0].DueOffset);
        Assert.Equal(TimeSpan.FromSeconds(6), schedule.Entries[4].DueOffset);
        Assert.True(schedule.Entries.Zip(schedule.Entries.Skip(1)).All(pair =>
            pair.First.DueOffset <= pair.Second.DueOffset));
    }

    [Fact]
    public void Build_RampUsesIncreasingRequestedRate()
    {
        var workload = new TemporalWorkloadDefinition
        {
            Id = "ramp",
            Segments =
            [
                new TemporalWorkloadSegment
                {
                    Id = "ramp-up",
                    Kind = "ramp",
                    DurationSeconds = 4,
                    StartRate = 1.0,
                    EndRate = 3.0
                }
            ]
        };

        var schedule = TemporalLoadScheduler.Build(workload);

        Assert.NotEmpty(schedule.Entries);
        Assert.True(schedule.Entries.Last().RequestedRate > schedule.Entries.First().RequestedRate);
        Assert.True(schedule.Entries.Zip(schedule.Entries.Skip(1)).All(pair =>
            pair.First.DueOffset <= pair.Second.DueOffset));
    }

    [Fact]
    public void CalculatePrecision_FlagsFractionalRateWithinBudget()
    {
        var precision = TemporalLoadScheduler.CalculatePrecision(
            requestedRate: 1.75,
            scheduledCount: 35,
            confirmedCount: 35,
            publishWindow: TimeSpan.FromSeconds(20),
            actualIntervalsMs: [571, 572, 570],
            delaysMs: [0, 1, 0]);

        Assert.True(precision.WithinFivePercent);
        Assert.Equal(1.75, precision.ActualPublishRate, precision: 6);
        Assert.Equal(35, precision.ConfirmedCount);
    }

    [Fact]
    public void Build_BurstSchedulesAllEventsAtSegmentStart()
    {
        var workload = new TemporalWorkloadDefinition
        {
            Id = "burst",
            Segments =
            [
                new TemporalWorkloadSegment
                {
                    Id = "burst-1",
                    Kind = "burst",
                    DurationSeconds = 1,
                    BurstCount = 5
                }
            ]
        };

        var schedule = TemporalLoadScheduler.Build(workload);

        Assert.Equal(5, schedule.Entries.Count);
        Assert.All(schedule.Entries, entry => Assert.Equal(TimeSpan.Zero, entry.DueOffset));
    }
}
