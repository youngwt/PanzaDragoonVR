using System;
using PanzaDragoonVR.Logic;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class BoardPlacementTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void LookingAlongMinusZ_IsTheSpotTheSceneUsedToUse()
    {
        BoardPose pose = BoardPlacement.InFrontOf(0, 1.6f, 0, 0, 0, -1)!.Value;

        Assert.Equal(0, pose.X, Tolerance);
        Assert.Equal(1.6f, pose.Y, Tolerance);
        Assert.Equal(-2.2f, pose.Z, Tolerance);
        Assert.Equal(0, pose.Yaw, Tolerance);
    }

    [Fact]
    public void FacingTheOtherWay_PutsTheBoardBehindTheOriginTurnedRound()
    {
        BoardPose pose = BoardPlacement.InFrontOf(0, 1.6f, 0, 0, 0, 1)!.Value;

        Assert.Equal(2.2f, pose.Z, Tolerance);
        Assert.Equal(MathF.PI, MathF.Abs(pose.Yaw), Tolerance);
    }

    [Fact]
    public void LookingAlongPlusX_FromAnOffsetHead_IsAheadAndFacesBack()
    {
        BoardPose pose = BoardPlacement.InFrontOf(1, 1.2f, 3, 1, 0, 0)!.Value;

        Assert.Equal(3.2f, pose.X, Tolerance);
        Assert.Equal(1.2f, pose.Y, Tolerance);
        Assert.Equal(3, pose.Z, Tolerance);
        // The board's front (+Z turned by yaw) points back along -X, at the head.
        Assert.Equal(-1, MathF.Sin(pose.Yaw), Tolerance);
        Assert.Equal(0, MathF.Cos(pose.Yaw), Tolerance);
    }

    [Fact]
    public void LookingDownwards_UsesOnlyTheHorizontalDirection()
    {
        BoardPose pose = BoardPlacement.InFrontOf(0, 1.6f, 0, 0, -0.8f, -0.6f)!.Value;

        Assert.Equal(-2.2f, pose.Z, Tolerance);
        Assert.Equal(1.6f, pose.Y, Tolerance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void LookingStraightUpOrDown_GivesNoPlacement(float y)
    {
        Assert.Null(BoardPlacement.InFrontOf(0, 1.6f, 0, 0, y, 0));
    }
}
