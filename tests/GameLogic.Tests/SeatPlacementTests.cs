using System;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>The ridden dragon's seat: where it stands, which way it faces and how it scales, for the tuning's settings.</summary>
public sealed class SeatPlacementTests
{
    private const float Tolerance = 1e-5f;
    private const float SceneHeight = 0.4f;

    private static SeatPlacement AtTheOrigin() => new(0, SceneHeight, 0);

    private static void AssertPose(SeatPose expected, SeatPose actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
        Assert.Equal(expected.Yaw, actual.Yaw, Tolerance);
        Assert.Equal(expected.Scale, actual.Scale, Tolerance);
    }

    [Fact]
    public void Start_IsWhereTheScenePutsIt()
    {
        AssertPose(new SeatPose(1, SceneHeight, -2, MathF.PI, 1), new SeatPlacement(1, SceneHeight, -2).Pose);
    }

    [Fact]
    public void BuiltIns_SitBelowTheHeadAtTheSceneHeightTurnedAHalfTurn()
    {
        SeatPlacement seat = AtTheOrigin();
        seat.Adjust(0, 0, 1);

        AssertPose(new SeatPose(1.5f, SceneHeight, -0.25f, MathF.PI, 1), seat.Under(1.5f, -0.25f));
    }

    [Fact]
    public void Forward_MovesTheDragonTowardsPlusZ()
    {
        SeatPlacement seat = AtTheOrigin();
        seat.Under(1.5f, -0.25f);

        SeatPose pose = seat.Adjust(0.75f, 0, 1);

        Assert.Equal(1.5f, pose.X, Tolerance);
        Assert.Equal(SceneHeight, pose.Y, Tolerance);
        Assert.Equal(0.5f, pose.Z, Tolerance);
    }

    [Fact]
    public void Height_MovesTheDragonDown()
    {
        SeatPlacement seat = AtTheOrigin();
        seat.Under(1.5f, -0.25f);

        SeatPose pose = seat.Adjust(0, 0.25f, 1);

        Assert.Equal(SceneHeight - 0.25f, pose.Y, Tolerance);
        Assert.Equal(1.5f, pose.X, Tolerance);
        Assert.Equal(-0.25f, pose.Z, Tolerance);
    }

    [Fact]
    public void Scale_TurnsAboutTheSeatAndLeavesItWhereItIs()
    {
        SeatPlacement seat = AtTheOrigin();
        SeatPose before = seat.Under(1.5f, -0.25f);

        SeatPose pose = seat.Adjust(0, 0, 2);

        AssertPose(before with { Scale = 2 }, pose);
    }

    [Fact]
    public void Order_HeadThenTuningIsTheSameAsTuningThenHead()
    {
        SeatPlacement headFirst = AtTheOrigin();
        headFirst.Under(1.5f, -0.25f);
        SeatPose a = headFirst.Adjust(-0.5f, 0.125f, 1.5f);

        SeatPlacement tuningFirst = AtTheOrigin();
        tuningFirst.Adjust(-0.5f, 0.125f, 1.5f);
        SeatPose b = tuningFirst.Under(1.5f, -0.25f);

        AssertPose(new SeatPose(1.5f, SceneHeight - 0.125f, -0.75f, MathF.PI, 1.5f), a);
        AssertPose(a, b);
    }

    [Fact]
    public void Recentre_KeepsTheTuningAndATuningChangeKeepsTheHead()
    {
        SeatPlacement seat = AtTheOrigin();
        seat.Adjust(0.5f, 0.25f, 2);
        seat.Under(1, 1);

        SeatPose recentred = seat.Under(-1, 3);
        AssertPose(new SeatPose(-1, SceneHeight - 0.25f, 3.5f, MathF.PI, 2), recentred);

        SeatPose retuned = seat.Adjust(0, 0, 1);
        AssertPose(new SeatPose(-1, SceneHeight, 3, MathF.PI, 1), retuned);
    }
}
