using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class RefreshRateTests
{
    private static readonly double[] Quest3Rates = { 72.0, 80.0, 90.0, 120.0 };

    [Fact]
    public void NinetyOffered_IsRequested()
    {
        RefreshRateChoice choice = RefreshRate.Choose(Quest3Rates, currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.Requested, choice.Outcome);
        Assert.Equal(90.0, choice.RateToRequest);
        Assert.Equal("Asking for 90 Hz (4 rates offered)", choice.Summary);
        Assert.False(choice.IsWarning);
    }

    [Fact]
    public void RequestThatTookEffect_MakesNinetyTheTarget()
    {
        RefreshRateChoice asked = RefreshRate.Choose(Quest3Rates, currentHz: 72.0);

        RefreshRateChoice choice = RefreshRate.Confirm(asked, inUseHz: 90.0);

        Assert.Equal(RefreshRateOutcome.Set, choice.Outcome);
        Assert.Equal(90.0, choice.TargetHz);
        Assert.Equal("Target 90 Hz (4 rates offered)", choice.Summary);
        Assert.False(choice.IsWarning);
    }

    [Fact]
    public void RequestThatDidNotTakeEffect_SaysSoAndMeasuresAgainstTheRateInUse()
    {
        RefreshRateChoice asked = RefreshRate.Choose(Quest3Rates, currentHz: 72.0);

        RefreshRateChoice choice = RefreshRate.Confirm(asked, inUseHz: 72.0);

        Assert.Equal(RefreshRateOutcome.RequestNotApplied, choice.Outcome);
        Assert.Equal(72.0, choice.TargetHz);
        Assert.Equal("Asked for 90 Hz; running at 72 Hz (4 rates offered)", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void RequestThatTakesEffectLater_IsConfirmedFromTheEarlierResult()
    {
        RefreshRateChoice asked = RefreshRate.Choose(Quest3Rates, currentHz: 72.0);
        RefreshRateChoice notYet = RefreshRate.Confirm(asked, inUseHz: 72.0);

        RefreshRateChoice choice = RefreshRate.Confirm(notYet, inUseHz: 90.0);

        Assert.Equal(RefreshRateOutcome.Set, choice.Outcome);
        Assert.Equal(90.0, choice.TargetHz);
        Assert.Equal("Target 90 Hz (4 rates offered)", choice.Summary);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(double.NaN)]
    public void RequestWithTheRateInUseUnknown_SaysSoAndMeasuresAgainstTheRateAskedFor(double inUseHz)
    {
        RefreshRateChoice asked = RefreshRate.Choose(Quest3Rates, currentHz: 72.0);

        RefreshRateChoice choice = RefreshRate.Confirm(asked, inUseHz);

        Assert.Equal(RefreshRateOutcome.RequestNotApplied, choice.Outcome);
        Assert.Equal(90.0, choice.TargetHz);
        Assert.Equal("Asked for 90 Hz; running at an unknown rate (4 rates offered)", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void ConfirmingAChoiceThatAskedForNothing_ChangesNothing()
    {
        RefreshRateChoice notOffered = RefreshRate.Choose(new[] { 60.0, 72.0, 120.0 }, currentHz: 72.0);

        Assert.Equal(notOffered, RefreshRate.Confirm(notOffered, inUseHz: 120.0));
    }

    [Fact]
    public void NinetyOfferedAsANearbyFloat_RequestsTheHeadsetsOwnFigure()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new[] { 72.0, 89.99 }, currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.Requested, choice.Outcome);
        Assert.Equal(89.99, choice.RateToRequest);
        Assert.Equal(RefreshRateOutcome.Set, RefreshRate.Confirm(choice, inUseHz: 90.0).Outcome);
    }

    [Fact]
    public void NinetyNotOffered_LeavesTheRateAndNamesTheOneInUse()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new[] { 60.0, 72.0, 120.0 }, currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.NotOffered, choice.Outcome);
        Assert.Null(choice.RateToRequest);
        Assert.Equal(72.0, choice.TargetHz);
        Assert.Equal("Running at 72 Hz; 90 Hz is not offered (3 rates offered)", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void EmptyRateList_LeavesTheRateAndSaysItCouldNotBeSet()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new double[0], currentHz: 72.0);

        Assert.Equal(RefreshRateOutcome.NoRateList, choice.Outcome);
        Assert.Null(choice.RateToRequest);
        Assert.Equal(72.0, choice.TargetHz);
        Assert.Equal("Running at 72 Hz; 90 Hz could not be set (0 rates offered)", choice.Summary);
        Assert.True(choice.IsWarning);
    }

    [Fact]
    public void ASingleRateOffered_IsCountedInTheSingular()
    {
        RefreshRateChoice choice = RefreshRate.Choose(new[] { 72.0 }, currentHz: 72.0);

        Assert.Equal("Running at 72 Hz; 90 Hz is not offered (1 rate offered)", choice.Summary);
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
        Assert.Equal("Running at an unknown rate; 90 Hz could not be set (0 rates offered)", choice.Summary);
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
