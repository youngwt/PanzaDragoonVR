#nullable enable
using System;

namespace PanzaDragoonVR.Logic;

/// <summary>Where the ridden dragon's seat stands, its yaw about +Y and the scale it is drawn at, turning about the seat.</summary>
public readonly record struct SeatPose(float X, float Y, float Z, float Yaw, float Scale);

/// <summary>
/// Places the ridden dragon's seat: below the head's X and Z at the scene's height, then moved by the tuning
/// file's offsets the other way to the player, and scaled about the seat. It remembers the last head and the
/// last settings, so placing the head and changing the tuning give the same pose in either order. Engine-free,
/// so it is tested on the PC; the game applies the pose as the dragon's global transform.
/// </summary>
public sealed class SeatPlacement
{
    /// <summary>
    /// The half-turn about +Y that points a model's front the way of flight: models face +Z, as glTF does,
    /// and the game flies towards -Z.
    /// </summary>
    public const float Facing = MathF.PI;

    private readonly float _sceneHeight;
    private float _headX;
    private float _headZ;
    private float _forward;
    private float _height;
    private float _scale = 1;

    /// <summary>A seat at (<paramref name="x"/>, <paramref name="sceneHeight"/>, <paramref name="z"/>), where the scene puts it, until the head is placed.</summary>
    public SeatPlacement(float x, float sceneHeight, float z)
    {
        _headX = x;
        _sceneHeight = sceneHeight;
        _headZ = z;
    }

    /// <summary>The seat's pose from the last head and the last settings.</summary>
    public SeatPose Pose => new(_headX, _sceneHeight - _height, _headZ + _forward, Facing, _scale);

    /// <summary>Puts the seat below a head at this X and Z; the settings stay as they are.</summary>
    public SeatPose Under(float headX, float headZ)
    {
        _headX = headX;
        _headZ = headZ;
        return Pose;
    }

    /// <summary>
    /// Moves the player <paramref name="forward"/> metres towards the head and <paramref name="height"/> metres
    /// up the dragon, by moving the dragon the other way: +Z, since the game flies towards -Z, and down. The
    /// dragon is drawn <paramref name="scale"/> times its size about the seat. The head stays where it was.
    /// </summary>
    public SeatPose Adjust(float forward, float height, float scale)
    {
        _forward = forward;
        _height = height;
        _scale = scale;
        return Pose;
    }
}
