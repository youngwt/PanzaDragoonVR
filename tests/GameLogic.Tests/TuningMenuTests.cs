using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>The development mode's tuning list: the story's matrix, and the presses that must not act.</summary>
public sealed class TuningMenuTests
{
    private static readonly TuningMenuReadings Nothing = default;
    private static readonly TuningMenuReadings PressB = new(0, 0, A: false, B: true);
    private static readonly TuningMenuReadings PressA = new(0, 0, A: true, B: false);

    private static TuningMenu Shown()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);
        menu.Step(PressB, 0.1);
        menu.Step(Nothing, 0.1);
        return menu;
    }

    private static TuningMenuReadings Stick(float x, float y) => new(x, y, A: false, B: false);

    [Fact]
    public void Start_IsHiddenWithTheGivenValues()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);

        Assert.False(menu.Shown);
        Assert.Equal(Tuning.BuiltIn, menu.Tuning);
    }

    [Fact]
    public void B_TogglesOncePerPress()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);

        menu.Step(PressB, 0.1);
        menu.Step(PressB, 0.1);
        Assert.True(menu.Shown);

        menu.Step(Nothing, 0.1);
        menu.Step(PressB, 0.1);
        Assert.False(menu.Shown);
    }

    [Fact]
    public void StickRight_RaisesTheValueAtItsRateAndAsksTheSceneToFollow()
    {
        TuningMenu menu = Shown();

        TuningMenuStep step = menu.Step(Stick(1, 0), 0.5);

        Assert.True(step.TuningChanged);
        Assert.Equal(Tuning.BuiltIn.FloorDepth + (3f * 0.5f), menu.Tuning.FloorDepth);
    }

    [Fact]
    public void StickPartway_ScalesTheRateAndLeftLowers()
    {
        TuningMenu menu = Shown();

        menu.Step(Stick(-0.5f, 0), 1);

        Assert.Equal(Tuning.BuiltIn.FloorDepth - 1.5f, menu.Tuning.FloorDepth);
    }

    [Fact]
    public void StickInTheMiddle_ChangesNothing()
    {
        TuningMenu menu = Shown();

        TuningMenuStep step = menu.Step(Stick(TuningMenu.ChangeAbove, 0), 1);

        Assert.False(step.TuningChanged);
        Assert.Equal(Tuning.BuiltIn, menu.Tuning);
    }

    [Theory]
    [InlineData(1, 50f)]
    [InlineData(-1, 3f)]
    public void Change_StopsAtTheRangesEnd(float x, float end)
    {
        TuningMenu menu = Shown();

        Assert.True(menu.Step(Stick(x, 0), 1000).TuningChanged);
        Assert.Equal(end, menu.Tuning.FloorDepth);

        Assert.False(menu.Step(Stick(x, 0), 1).TuningChanged);
        Assert.Equal(end, menu.Tuning.FloorDepth);
    }

    [Fact]
    public void Change_EverySettingCanBeReachedAndMoved()
    {
        TuningMenu menu = Shown();
        foreach (Tuning.Setting setting in Tuning.Settings)
        {
            Tuning before = menu.Tuning;
            Assert.Equal(setting, Tuning.Settings[menu.Picked]);

            Assert.True(menu.Step(Stick(1, 0), 0.1).TuningChanged);

            Assert.Equal(setting.Get(before) + (float)(setting.Rate * 0.1), setting.Get(menu.Tuning), 1e-3f);
            Assert.Equal(before, setting.Set(menu.Tuning, setting.Get(before)));
            menu.Step(Nothing, 0.1);
            menu.Step(Stick(0, -1), 0.1);
            menu.Step(Nothing, 0.1);
        }

        Assert.Equal(0, menu.Picked);
    }

    [Fact]
    public void StickUp_MovesThePickOnceAndWraps()
    {
        TuningMenu menu = Shown();

        menu.Step(Stick(0, 0.8f), 0.1);
        menu.Step(Stick(0, 0.8f), 0.1);

        Assert.Equal(Tuning.Settings.Count - 1, menu.Picked);
    }

    [Fact]
    public void StickDown_MovesThePickTheOtherWay()
    {
        TuningMenu menu = Shown();

        menu.Step(Stick(0, -0.8f), 0.1);

        Assert.Equal(1, menu.Picked);
    }

    [Fact]
    public void Pick_RearmsOnlyBelowTheLowerMark()
    {
        TuningMenu menu = Shown();

        menu.Step(Stick(0, -0.8f), 0.1);
        menu.Step(Stick(0, -0.5f), 0.1);
        menu.Step(Stick(0, -0.8f), 0.1);
        Assert.Equal(1, menu.Picked);

        menu.Step(Stick(0, -0.2f), 0.1);
        menu.Step(Stick(0, -0.8f), 0.1);
        Assert.Equal(2, menu.Picked);
    }

    [Fact]
    public void Value_DoesNotChangeWhilePickIsHeld()
    {
        TuningMenu menu = Shown();

        Assert.False(menu.Step(Stick(1, 0.8f), 1).TuningChanged);
        Assert.False(menu.Step(Stick(1, 0.5f), 1).TuningChanged);
        Assert.Equal(Tuning.BuiltIn, menu.Tuning);

        Assert.True(menu.Step(Stick(1, 0.2f), 0.1).TuningChanged);
    }

    [Fact]
    public void A_AsksForOneSavePerPress()
    {
        TuningMenu menu = Shown();

        Assert.True(menu.Step(PressA, 0.1).SaveAsked);
        Assert.False(menu.Step(PressA, 0.1).SaveAsked);
        menu.Step(Nothing, 0.1);
        Assert.True(menu.Step(PressA, 0.1).SaveAsked);
    }

    [Fact]
    public void AWithTheBThatShows_ShowsTheListAndSavesNothing()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);

        TuningMenuStep step = menu.Step(new TuningMenuReadings(0, 0, A: true, B: true), 0.1);

        Assert.True(menu.Shown);
        Assert.False(step.SaveAsked);
    }

    [Fact]
    public void AOnALaterFrame_AfterTheShowingFrame_Saves()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);
        menu.Step(new TuningMenuReadings(0, 0, A: true, B: true), 0.1);

        Assert.False(menu.Step(PressA, 0.1).SaveAsked);
        menu.Step(Nothing, 0.1);
        Assert.True(menu.Step(PressA, 0.1).SaveAsked);
    }

    [Fact]
    public void AOnTheFrameAfterB_Saves()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);
        menu.Step(PressB, 0.1);

        Assert.True(menu.Step(PressA, 0.1).SaveAsked);
    }

    [Fact]
    public void Save_GivesTheListItsOutcome_AndAChangeClearsIt()
    {
        TuningMenu menu = Shown();

        menu.Saved(null);
        Assert.EndsWith("\nsaved", menu.Text());

        menu.Saved("the disc is full");
        Assert.EndsWith("\nnot saved: the disc is full", menu.Text());

        menu.Step(Stick(1, 0), 0.1);
        Assert.DoesNotContain("saved", menu.Text());
    }

    [Fact]
    public void Hidden_OnlyBDoesAnything()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);

        TuningMenuStep step = menu.Step(new TuningMenuReadings(1, 0.8f, A: true, B: false), 1);

        Assert.Equal(default, step);
        Assert.Equal(Tuning.BuiltIn, menu.Tuning);
        Assert.Equal(0, menu.Picked);
    }

    [Fact]
    public void Showing_DoesNotActOnAPressOrPushHeldSinceHidden()
    {
        var menu = new TuningMenu(Tuning.BuiltIn);
        var held = new TuningMenuReadings(1, 0.8f, A: true, B: false);
        menu.Step(held, 0.1);

        TuningMenuStep step = menu.Step(held with { B = true }, 0.1);

        Assert.True(menu.Shown);
        Assert.False(step.SaveAsked);
        Assert.Equal(0, menu.Picked);

        // With the pick let go, the sideways hold still changes nothing until the stick comes back.
        Assert.False(menu.Step(Stick(1, 0), 0.1).TuningChanged);
        Assert.Equal(Tuning.BuiltIn, menu.Tuning);
        menu.Step(Nothing, 0.1);
        Assert.True(menu.Step(Stick(1, 0), 0.1).TuningChanged);
    }

    [Fact]
    public void ControllersAsleep_NothingMoves()
    {
        TuningMenu menu = Shown();

        TuningMenuStep step = menu.Step(Nothing, 1);

        Assert.Equal(default, step);
        Assert.Equal(Tuning.BuiltIn, menu.Tuning);
        Assert.Equal(0, menu.Picked);
    }

    [Fact]
    public void Text_ListsEverySettingAndMarksThePick()
    {
        TuningMenu menu = Shown();
        menu.Step(Stick(0, -0.8f), 0.1);

        string[] lines = menu.Text().Split('\n');

        Assert.Equal("  floor_depth  8.999", lines[0]);
        Assert.Equal("> canopy_height  10.000", lines[1]);
        Assert.Equal("  dragon_scale  0.997", lines[8]);
        Assert.Equal(10, lines.Length);
    }
}
