#nullable enable
using System;

namespace PanzaDragoonVR.Logic;

/// <summary>A point on a cube's path, in metres from the player's origin.</summary>
public readonly record struct CubePoint(float X, float Y, float Z);

/// <summary>
/// Flies the six target cubes. Each circles the player's origin, a sixth of a lap from the next, at its own radius
/// and height, and twists round its own circle as it goes, so its path is a corkscrew closed on itself each lap.
/// It also keeps which cubes are destroyed: a destroyed cube flies on unseen and is present again
/// <see cref="ReturnSeconds"/> later, wherever its path has reached. The feel values are built in, not tuning
/// settings. Engine-free, so it is tested on the PC; the game advances it each frame and places the cubes from it.
/// </summary>
public sealed class CubeFlight
{
    /// <summary>How many cubes fly.</summary>
    public const int Count = 6;

    /// <summary>Seconds a destroyed cube stays away before it is present again.</summary>
    public const double ReturnSeconds = 4;

    /// <summary>Seconds for one lap round the player.</summary>
    public const double LapSeconds = 14;

    /// <summary>How far the corkscrew strays from a cube's circle, in metres.</summary>
    public const float TwistRadius = 0.5f;

    /// <summary>How many times the corkscrew turns round the circle in one lap: whole, so the path closes.</summary>
    public const int TwistsPerLap = 3;

    /// <summary>Each cube's circle's radius about the player's origin, in metres: 4 m and 5 m in turn.</summary>
    private static readonly float[] Radii = { 4f, 5f, 4f, 5f, 4f, 5f };

    /// <summary>
    /// The height of each cube's circle above the floor, in metres: 1.8 m and 2.4 m in turn, so that no cube goes
    /// below 1.3 m and each passes over the dragon's head.
    /// </summary>
    private static readonly float[] Heights = { 1.8f, 2.4f, 1.8f, 2.4f, 1.8f, 2.4f };

    // Kept within one lap so the angle stays exact however long the app runs.
    private double _lapTime;

    // Seconds until each cube returns; 0 when it is present.
    private readonly double[] _returnIn = new double[Count];

    /// <summary>Moves every cube on by <paramref name="seconds"/>, and brings back those whose time away is up.</summary>
    public void Advance(double seconds)
    {
        _lapTime = (_lapTime + seconds) % LapSeconds;
        if (_lapTime < 0)
        {
            _lapTime += LapSeconds;
        }

        for (int i = 0; i < Count; i++)
        {
            if (_returnIn[i] > 0)
            {
                _returnIn[i] = Math.Max(0, _returnIn[i] - seconds);
            }
        }
    }

    /// <summary>Where cube <paramref name="cube"/>'s path has reached, whether it is present or not.</summary>
    public CubePoint Position(int cube)
    {
        Check(cube);
        double lap = 2 * Math.PI * _lapTime / LapSeconds + cube * 2 * Math.PI / Count;
        double twist = TwistsPerLap * lap + cube * Math.PI / 3;

        // Out from the player along the circle's radius, and the twist round the circle in the plane of that and up.
        double outward = Radii[cube] + TwistRadius * Math.Cos(twist);
        return new CubePoint(
            (float)(outward * Math.Sin(lap)),
            (float)(Heights[cube] + TwistRadius * Math.Sin(twist)),
            (float)(-outward * Math.Cos(lap)));
    }

    /// <summary>Whether cube <paramref name="cube"/> is present: false from its destruction until it returns.</summary>
    public bool IsPresent(int cube)
    {
        Check(cube);
        return _returnIn[cube] <= 0;
    }

    /// <summary>
    /// Destroys cube <paramref name="cube"/>: it is not present for the next <see cref="ReturnSeconds"/>. A cube
    /// destroyed already is left as it is, returning when it would have.
    /// </summary>
    public void Destroy(int cube)
    {
        Check(cube);
        if (_returnIn[cube] <= 0)
        {
            _returnIn[cube] = ReturnSeconds;
        }
    }

    private static void Check(int cube)
    {
        if (cube < 0 || cube >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(cube), cube, $"there are {Count} cubes, numbered from 0");
        }
    }
}
