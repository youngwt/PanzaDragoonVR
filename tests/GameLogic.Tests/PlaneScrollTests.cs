using PanzaDragoonVR.Logic;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class PlaneScrollTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void SteadyStep_MovesBackByTheTilesFlown()
    {
        float next = PlaneScroll.Next(0.25f, 5f, 1f / 90f, 2f);

        Assert.Equal(0.25f - 5f / 90f / 2f, next, Tolerance);
        Assert.InRange(next, 0f, 1f);
        Assert.True(next < 1f);
    }

    [Fact]
    public void Wrap_StaysWithinOneTile()
    {
        // A step of 0.03 tile: 0.06 m over a 2 m tile.
        float next = PlaneScroll.Next(0.01f, 0.06f, 1f, 2f);

        Assert.Equal(0.98f, next, Tolerance);
        Assert.True(next >= 0f && next < 1f);
    }

    [Theory]
    [InlineData(0.3f, 2f, 0.05f)]
    [InlineData(0.25f, 2f, 0f)]
    [InlineData(0.7f, 0.1f, 0.7f)]
    public void LongFrame_WrapsHoweverManyTilesItSpans(float offset, float tile, float expected)
    {
        // A half-second hitch at 5 m/s: 2.5 m, more than a whole tile.
        float next = PlaneScroll.Next(offset, 5f, 0.5f, tile);

        Assert.True(next >= 0f && next < 1f, $"offset {next} is outside [0, 1)");
        Assert.Equal(expected, next, 1e-4f);
    }

    [Fact]
    public void SkySpeed_CrossesTheSameAnglePerSecondAsTheFloor()
    {
        Assert.Equal(120f, PlaneScroll.SkySpeed(5f, 1.6f, 38.4f), 1e-3f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1.6f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void SkySpeed_OnBadDistances_IsTheFloorSpeed(float floorDepth)
    {
        Assert.Equal(5f, PlaneScroll.SkySpeed(5f, floorDepth, 38.4f));
    }
}
