#nullable enable
using System;

namespace PanzaDragoonVR.Logic;

/// <summary>Where the board stands, and the yaw that turns its front (+Z) towards the player.</summary>
public readonly record struct BoardPose(float X, float Y, float Z, float Yaw);

/// <summary>
/// Places the launch report board in front of the player's head: a set distance along the head's
/// horizontal facing direction, at the head's height, turned to face back at the head. Engine-free,
/// so it is tested on the PC; the game feeds it the camera's position and forward direction.
/// </summary>
public static class BoardPlacement
{
    /// <summary>Metres between the head and the board.</summary>
    public const float Distance = 2.2f;

    /// <summary>
    /// The board's pose for a head at (<paramref name="headX"/>, <paramref name="headY"/>,
    /// <paramref name="headZ"/>) looking along (<paramref name="forwardX"/>, <paramref name="forwardY"/>,
    /// <paramref name="forwardZ"/>); null when the head looks straight up or down, which gives no
    /// horizontal direction.
    /// </summary>
    public static BoardPose? InFrontOf(float headX, float headY, float headZ, float forwardX, float forwardY, float forwardZ)
    {
        _ = forwardY;
        float length = MathF.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
        if (length < 1e-3f)
        {
            return null;
        }

        float fx = forwardX / length;
        float fz = forwardZ / length;
        // The board's front is its +Z; turned by yaw θ about +Y it points along (sin θ, 0, cos θ),
        // which must point back at the head, along -forward.
        float yaw = MathF.Atan2(-fx, -fz);
        return new BoardPose(headX + fx * Distance, headY, headZ + fz * Distance, yaw);
    }
}
