using System;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>The six target cubes' corkscrew paths round the player, and their destruction and return.</summary>
public sealed class CubeFlightTests
{
    private const float Tolerance = 1e-4f;

    private static float Horizontal(CubePoint point) => MathF.Sqrt(point.X * point.X + point.Z * point.Z);

    private static float Distance(CubePoint a, CubePoint b) =>
        MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    private static void AssertPoint(CubePoint expected, CubePoint actual)
    {
        Assert.Equal(expected.X, actual.X, Tolerance);
        Assert.Equal(expected.Y, actual.Y, Tolerance);
        Assert.Equal(expected.Z, actual.Z, Tolerance);
    }

    [Fact]
    public void Count_IsSix()
    {
        Assert.Equal(6, CubeFlight.Count);
        Assert.Equal(4, CubeFlight.ReturnSeconds);
    }

    [Fact]
    public void Paths_StayWithinTheBoundsAllLap()
    {
        var flight = new CubeFlight();
        // Three laps in steps that do not divide one, so the samples fall all over the paths.
        for (int step = 0; step < 3100; step++)
        {
            for (int cube = 0; cube < CubeFlight.Count; cube++)
            {
                CubePoint point = flight.Position(cube);
                Assert.InRange(Horizontal(point), 3.5f - Tolerance, 5.5f + Tolerance);
                Assert.InRange(point.Y, 0.5f, 3.5f);
            }

            flight.Advance(0.0137);
        }
    }

    [Fact]
    public void Paths_PassOverTheDragonsHead()
    {
        var flight = new CubeFlight();
        for (int step = 0; step < 3100; step++)
        {
            for (int cube = 0; cube < CubeFlight.Count; cube++)
            {
                Assert.True(flight.Position(cube).Y >= 1.25f, $"cube {cube} drops below 1.25 m");
            }

            flight.Advance(0.0137);
        }
    }

    [Fact]
    public void Paths_CloseOnThemselvesEachLap()
    {
        var flight = new CubeFlight();
        flight.Advance(2.3);
        var start = new CubePoint[CubeFlight.Count];
        for (int cube = 0; cube < CubeFlight.Count; cube++)
        {
            start[cube] = flight.Position(cube);
        }

        flight.Advance(CubeFlight.LapSeconds);

        for (int cube = 0; cube < CubeFlight.Count; cube++)
        {
            AssertPoint(start[cube], flight.Position(cube));
        }
    }

    [Fact]
    public void Paths_MoveDuringALap()
    {
        var flight = new CubeFlight();
        CubePoint start = flight.Position(0);

        flight.Advance(CubeFlight.LapSeconds / 4);

        Assert.True(Distance(start, flight.Position(0)) > 1);
    }

    [Fact]
    public void Start_SpreadsTheCubesAllRound()
    {
        var flight = new CubeFlight();
        for (int a = 0; a < CubeFlight.Count; a++)
        {
            for (int b = a + 1; b < CubeFlight.Count; b++)
            {
                Assert.True(Distance(flight.Position(a), flight.Position(b)) > 1, $"cubes {a} and {b} start within 1 m");
            }
        }

        // The first starts straight ahead, towards -Z.
        CubePoint first = flight.Position(0);
        Assert.Equal(0, first.X, Tolerance);
        Assert.True(first.Z < 0);
    }

    [Fact]
    public void Start_HasEveryCubePresent()
    {
        var flight = new CubeFlight();
        for (int cube = 0; cube < CubeFlight.Count; cube++)
        {
            Assert.True(flight.IsPresent(cube));
        }
    }

    [Fact]
    public void Destroy_HidesTheCubeUntilItsReturnAndNoOther()
    {
        var flight = new CubeFlight();
        flight.Advance(1.25);

        flight.Destroy(2);

        Assert.False(flight.IsPresent(2));
        flight.Advance(3.5);
        Assert.False(flight.IsPresent(2));
        for (int cube = 0; cube < CubeFlight.Count; cube++)
        {
            if (cube != 2)
            {
                Assert.True(flight.IsPresent(cube));
            }
        }
    }

    [Fact]
    public void Destroy_ReturnsTheCubeWhereItsPathHasReached()
    {
        var flight = new CubeFlight();
        var undisturbed = new CubeFlight();
        flight.Advance(1.25);
        undisturbed.Advance(1.25);

        flight.Destroy(2);
        flight.Advance(3.5);
        undisturbed.Advance(3.5);
        Assert.False(flight.IsPresent(2));
        flight.Advance(0.5);
        undisturbed.Advance(0.5);

        Assert.True(flight.IsPresent(2));
        AssertPoint(undisturbed.Position(2), flight.Position(2));
    }

    [Fact]
    public void Destroy_ReturnsWithinAFrameAtNinetyHertz()
    {
        var flight = new CubeFlight();
        flight.Destroy(0);

        int frames = 0;
        while (!flight.IsPresent(0))
        {
            flight.Advance(1.0 / 90);
            frames++;
        }

        Assert.InRange(frames, 360, 361);
    }

    [Fact]
    public void Destroy_WhileDestroyedChangesNothing()
    {
        var flight = new CubeFlight();
        flight.Destroy(4);
        flight.Advance(3);

        flight.Destroy(4);
        flight.Advance(1);

        Assert.True(flight.IsPresent(4));
    }

    [Fact]
    public void Destroy_AfterReturningHidesTheCubeAgain()
    {
        var flight = new CubeFlight();
        flight.Destroy(1);
        flight.Advance(4);

        flight.Destroy(1);

        Assert.False(flight.IsPresent(1));
    }

    [Fact]
    public void Destroy_KeepsTheCubeFlyingOverManyLaps()
    {
        var flight = new CubeFlight();
        var undisturbed = new CubeFlight();
        flight.Destroy(5);
        for (int step = 0; step < 100; step++)
        {
            flight.Advance(0.5);
            undisturbed.Advance(0.5);
        }

        Assert.True(flight.IsPresent(5));
        AssertPoint(undisturbed.Position(5), flight.Position(5));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void BadIndex_Throws(int cube)
    {
        var flight = new CubeFlight();

        Assert.Throws<ArgumentOutOfRangeException>(() => flight.Destroy(cube));
        Assert.Throws<ArgumentOutOfRangeException>(() => flight.Position(cube));
        Assert.Throws<ArgumentOutOfRangeException>(() => flight.IsPresent(cube));
    }
}
