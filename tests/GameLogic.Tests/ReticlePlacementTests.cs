using System;
using PanzaDragoonVR.Logic;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class ReticlePlacementTests
{
    private const float Tolerance = 1e-4f;

    private static float DistanceFrom(ReticlePoint point, float x, float y, float z) =>
        MathF.Sqrt((point.X - x) * (point.X - x) + (point.Y - y) * (point.Y - y) + (point.Z - z) * (point.Z - z));

    [Fact]
    public void FromTheHead_StraightAhead_IsOneRadiusAlongMinusZ()
    {
        ReticlePoint point = ReticlePlacement.OnSphere(0, 1.6f, 0, 0, 0, -1, 0, 1.6f, 0)!.Value;

        Assert.Equal(0, point.X, Tolerance);
        Assert.Equal(1.6f, point.Y, Tolerance);
        Assert.Equal(-ReticlePlacement.Radius, point.Z, Tolerance);
    }

    [Fact]
    public void FromTheHead_Sideways_IsOneRadiusAlongX()
    {
        ReticlePoint point = ReticlePlacement.OnSphere(0, 1.6f, 0, 1, 0, 0, 0, 1.6f, 0)!.Value;

        Assert.Equal(ReticlePlacement.Radius, point.X, Tolerance);
        Assert.Equal(1.6f, point.Y, Tolerance);
        Assert.Equal(0, point.Z, Tolerance);
    }

    [Fact]
    public void FromTheHead_Up_IsOneRadiusAbove()
    {
        ReticlePoint point = ReticlePlacement.OnSphere(0, 1.6f, 0, 0, 1, 0, 0, 1.6f, 0)!.Value;

        Assert.Equal(0, point.X, Tolerance);
        Assert.Equal(1.6f + ReticlePlacement.Radius, point.Y, Tolerance);
        Assert.Equal(0, point.Z, Tolerance);
    }

    [Fact]
    public void ANonUnitDirection_GivesTheSamePointAsTheUnitOne()
    {
        ReticlePoint unit = ReticlePlacement.OnSphere(0.1f, 1.3f, -0.2f, 0.6f, 0, -0.8f, 0, 1.6f, 0)!.Value;
        ReticlePoint longer = ReticlePlacement.OnSphere(0.1f, 1.3f, -0.2f, 3, 0, -4, 0, 1.6f, 0)!.Value;

        Assert.Equal(unit.X, longer.X, Tolerance);
        Assert.Equal(unit.Y, longer.Y, Tolerance);
        Assert.Equal(unit.Z, longer.Z, Tolerance);
    }

    [Fact]
    public void AnOriginOffTheCentre_IsWhereTheRayLeavesTheSphere()
    {
        // A hand below, right of and ahead of the head, pointing straight ahead.
        ReticlePoint point = ReticlePlacement.OnSphere(0.2f, 1.2f, -0.3f, 0, 0, -1, 0, 1.6f, 0)!.Value;

        Assert.Equal(0.2f, point.X, Tolerance);
        Assert.Equal(1.2f, point.Y, Tolerance);
        Assert.Equal(-MathF.Sqrt(36 - 0.2f * 0.2f - 0.4f * 0.4f), point.Z, Tolerance);
        Assert.Equal(ReticlePlacement.Radius, DistanceFrom(point, 0, 1.6f, 0), Tolerance);
    }

    [Fact]
    public void FromOutside_TowardsTheSphere_IsTheFarMeeting()
    {
        ReticlePoint point = ReticlePlacement.OnSphere(0, 0, 10, 0, 0, -1, 0, 0, 0)!.Value;

        Assert.Equal(0, point.X, Tolerance);
        Assert.Equal(0, point.Y, Tolerance);
        Assert.Equal(-ReticlePlacement.Radius, point.Z, Tolerance);
    }

    [Fact]
    public void FromOutside_PassingTheSphereBy_GivesNoPoint()
    {
        Assert.Null(ReticlePlacement.OnSphere(0, 10, 10, 0, 0, -1, 0, 0, 0));
    }

    [Fact]
    public void FromOutside_WithTheSphereBehind_GivesNoPoint()
    {
        Assert.Null(ReticlePlacement.OnSphere(0, 0, 10, 0, 0, 1, 0, 0, 0));
    }

    [Fact]
    public void AZeroDirection_GivesNoPoint()
    {
        Assert.Null(ReticlePlacement.OnSphere(0, 1.6f, 0, 0, 0, 0, 0, 1.6f, 0));
    }
}
