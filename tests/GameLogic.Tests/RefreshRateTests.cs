using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class RefreshRateTests
{
    private static readonly double[] Quest3Rates = { 72.0, 80.0, 90.0, 120.0 };

    [Fact]
    public void NinetyOffered_IsRequestedAndBecomesTheTarget()
    {
        RefreshRateChoice choice = RefreshRate.Choose(Quest3Rates, currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.Set, choice.Outcome);
        Assert.Equal(90.0, choice.RateToRequest);
        Assert.Equal(90.0, choice.TargetHz);
        Assert.Equal("Target 90 Hz", choice.Summary);
        Assert.False(choice.IsWarning);
    }

    [Fact]
    public void NinetyOfferedAsANearbyFloat_RequestsTheHeadsetsOwnFigure()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new[] { 72.0, 89.99 }, currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.Set, choice.Outcome);
        Assert.Equal(89.99, choice.RateToRequest);
        Assert.Equal(90.0, choice.TargetHz);
    }

    [Fact]
    public void NinetyNotOffered_LeavesTheRateAndNamesTheOneInUse()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new[] { 60.0, 72.0, 120.0 }, currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.NotOffered, choice.Outcome);
        Assert.Null(choice.RateToRequest);
        Assert.Equal(72.0, choice.TargetHz);
        Assert.Equal("Running at 72 Hz; 90 Hz is not offered", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void EmptyRateList_LeavesTheRateAndSaysItCouldNotBeSet()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new double[0], currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.NoRateList, choice.Outcome);
        Assert.Null(choice.RateToRequest);
        Assert.Equal(72.0, choice.TargetHz);
        Assert.Equal("Running at 72 Hz; 90 Hz could not be set", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void NullRateList_IsTreatedAsEmpty()
    {
        Assert.Equal(RefreshRateOutcome.NoRateList, RefreshRate.Choose(null, currentHz: 72.0).Outcome);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void UnknownCurrentRate_IsSaidSoAndFramesAreMeasuredAgainstNinety(double currentHz)
    {
        RefreshRateChoice choice = RefreshRate.Choose(new double[0], currentHz);

        Assert.Equal(90.0, choice.TargetHz);
        Assert.Equal("Running at an unknown rate; 90 Hz could not be set", choice.Summary);
    }

    [Fact]
    public void NotInVr_SaysSoAndMeasuresAgainstTheScreen()
    {
        RefreshRateChoice choice = RefreshRate.NotInVr(screenHz: 144.0);

        Assert.Equal(RefreshRateOutcome.NotInVr, choice.Outcome);
        Assert.Null(choice.RateToRequest);
        Assert.Equal(144.0, choice.TargetHz);
        Assert.Equal("Not in VR", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void NotInVr_WithAnUnknownScreenRate_FallsBackToSixty()
    {
        Assert.Equal(60.0, RefreshRate.NotInVr(screenHz: -1.0).TargetHz);
    }
}
