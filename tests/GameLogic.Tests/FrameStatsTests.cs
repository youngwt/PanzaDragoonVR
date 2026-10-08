using System;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class FrameStatsTests
{
    private const double Interval90 = 1.0 / 90.0;

    /// <summary>Feeds frames of one length until a window is reported.</summary>
    private static FrameReport RunUntilReport(FrameStats stats, double frameSeconds)
    {
        for (int i = 0; i < 100_000; i++)
        {
            if (stats.AddFrame(frameSeconds, out FrameReport report))
            {
                return report;
            }
        }

        throw new InvalidOperationException("No window was reported.");
    }

    [Fact]
    public void SteadyNinety_AveragesNinetyWithNoSlowFrames()
    {
        var stats = new FrameStats(90.0);

        FrameReport report = RunUntilReport(stats, Interval90);

        Assert.Equal(90.0, report.TargetHz);
        Assert.Equal(90.0, report.AverageHz, precision: 6);
        Assert.Equal(0, report.SlowFrames);
        Assert.Equal(0, report.TotalSlowFrames);
        Assert.InRange(report.TotalFrames, 90, 91);
    }

    [Fact]
    public void NoReportBeforeTheWindowIsFull()
    {
        var stats = new FrameStats(90.0);

        for (int i = 0; i < 45; i++)
        {
            Assert.False(stats.AddFrame(Interval90, out _));
        }

        Assert.Equal(45, stats.TotalFrames);
    }

    [Fact]
    public void FramesLongerThanOneAndAHalfIntervals_AreCountedAsSlow()
    {
        var stats = new FrameStats(90.0);

        stats.AddFrame(Interval90 * 1.6, out _);
        stats.AddFrame(Interval90 * 2.0, out _);
        stats.AddFrame(Interval90 * 1.4, out _);
        FrameReport report = RunUntilReport(stats, Interval90);

        Assert.Equal(2, report.SlowFrames);
        Assert.Equal(2, report.TotalSlowFrames);
    }

    [Fact]
    public void AFrameOfExactlyOneAndAHalfIntervals_IsNotSlow()
    {
        var stats = new FrameStats(100.0, windowSeconds: 0.015);

        Assert.True(stats.AddFrame(0.015, out FrameReport report));

        Assert.Equal(0, report.SlowFrames);
    }

    [Fact]
    public void SlowCountForTheLastSecondResets_WhileTheCountSinceStartKeepsGrowing()
    {
        var stats = new FrameStats(90.0);

        stats.AddFrame(Interval90 * 3.0, out _);
        FrameReport first = RunUntilReport(stats, Interval90);
        FrameReport second = RunUntilReport(stats, Interval90);
        stats.AddFrame(Interval90 * 3.0, out _);
        FrameReport third = RunUntilReport(stats, Interval90);

        Assert.Equal(1, first.SlowFrames);
        Assert.Equal(0, second.SlowFrames);
        Assert.Equal(1, second.TotalSlowFrames);
        Assert.Equal(1, third.SlowFrames);
        Assert.Equal(2, third.TotalSlowFrames);
        Assert.True(third.TotalFrames > second.TotalFrames);
    }

    [Fact]
    public void HalfRate_AveragesFortyFiveAndEveryFrameIsSlow()
    {
        var stats = new FrameStats(90.0);

        FrameReport report = RunUntilReport(stats, Interval90 * 2.0);

        Assert.Equal(45.0, report.AverageHz, precision: 6);
        Assert.Equal(report.TotalFrames, report.SlowFrames);
        Assert.Equal(100.0, report.SlowPercent, precision: 6);
    }

    [Fact]
    public void ALongStall_IsOneSlowFrameAndPullsTheAverageDown()
    {
        var stats = new FrameStats(90.0);

        Assert.True(stats.AddFrame(2.0, out FrameReport report));

        Assert.Equal(0.5, report.AverageHz, precision: 6);
        Assert.Equal(1, report.SlowFrames);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void FrameTimesThatAreNotRealTimes_AreIgnored(double frameSeconds)
    {
        var stats = new FrameStats(90.0);

        Assert.False(stats.AddFrame(frameSeconds, out _));

        Assert.Equal(0, stats.TotalFrames);
        Assert.Equal(0, stats.TotalSlowFrames);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-90.0)]
    [InlineData(double.NaN)]
    public void ATargetThatIsNotAboveZero_IsRefused(double targetHz)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStats(targetHz));
    }

    [Fact]
    public void LogLine_HasTheFiguresAsKeyValuePairs()
    {
        var report = new FrameReport(90.0, 89.96, SlowFrames: 1, TotalSlowFrames: 3, TotalFrames: 5400);

        Assert.Equal(
            "target=90 avg=90.0 slow=1 slow_total=3 frames_total=5400 slow_pct=0.06",
            report.ToLogLine());
    }

    [Fact]
    public void ReadoutText_ShowsTheMeasuredRateAndBothSlowCounts()
    {
        var report = new FrameReport(90.0, 88.42, SlowFrames: 2, TotalSlowFrames: 7, TotalFrames: 900);

        Assert.Equal(
            "Measured 88.4 Hz\nSlow frames: 2 last second, 7 since start",
            report.ToReadoutText());
    }
}
