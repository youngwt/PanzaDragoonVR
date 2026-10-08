#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// Flies its node round a circle centred on its parent's origin, rising and falling twice a lap.
/// The path closes on itself and passes behind the player, so following it means turning right round.
/// </summary>
public partial class FlyingCube : Node3D
{
    /// <summary>Distance from the centre, in metres.</summary>
    [Export] public float Radius { get; set; } = 4.0f;

    /// <summary>Seconds for one lap.</summary>
    [Export] public float LapSeconds { get; set; } = 12.0f;

    /// <summary>Height of the middle of the path, in metres.</summary>
    [Export] public float Height { get; set; } = 1.8f;

    /// <summary>How far the cube rises above and drops below that height, in metres.</summary>
    [Export] public float Bob { get; set; } = 0.6f;

    /// <summary>How fast the cube tumbles, in radians a second.</summary>
    [Export] public float TumbleRadiansPerSecond { get; set; } = 1.0f;

    private static readonly Vector3 TumbleAxis = new Vector3(0.3f, 1.0f, 0.2f).Normalized();

    private double _lapPosition;

    public override void _Ready()
    {
        Position = PointOnPath(0.0);
    }

    public override void _Process(double delta)
    {
        if (LapSeconds > 0.0f)
        {
            // Kept within one lap so the angle stays exact however long the app runs.
            _lapPosition = Mathf.PosMod(_lapPosition + delta / LapSeconds, 1.0);
        }

        Position = PointOnPath(_lapPosition);
        Rotate(TumbleAxis, TumbleRadiansPerSecond * (float)delta);
    }

    /// <summary>The point at a given fraction of a lap. Starts straight ahead of the player.</summary>
    private Vector3 PointOnPath(double lapPosition)
    {
        float angle = (float)(lapPosition * Mathf.Tau);
        return new Vector3(
            Radius * Mathf.Sin(angle),
            Height + Bob * Mathf.Sin(2.0f * angle),
            -Radius * Mathf.Cos(angle));
    }
}
