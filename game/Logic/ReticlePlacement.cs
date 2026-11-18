#nullable enable
using System;

namespace PanzaDragoonVR.Logic;

/// <summary>A point in the scene, in metres.</summary>
public readonly record struct ReticlePoint(float X, float Y, float Z);

/// <summary>
/// Places the aiming reticle: where the left controller's aim ray leaves a sphere of a fixed radius centred on
/// the head, so it reads as a sprite at a constant distance around the player. Engine-free, so it is tested on
/// the PC; the game feeds it the controller's position and pointing and the camera's position each frame.
/// </summary>
public static class ReticlePlacement
{
    /// <summary>Metres from the head to the reticle: beyond the board (2.2 m) and the cubes' paths (3.5 m to 5.5 m out).</summary>
    public const float Radius = 6f;

    /// <summary>The reticle's width in metres, about 2.4 degrees across at <see cref="Radius"/>.</summary>
    public const float Size = 0.25f;

    /// <summary>
    /// Where the ray from (<paramref name="originX"/>, <paramref name="originY"/>, <paramref name="originZ"/>)
    /// along (<paramref name="directionX"/>, <paramref name="directionY"/>, <paramref name="directionZ"/>), of any
    /// length from 0.001 up, last meets the sphere of <see cref="Radius"/> about (<paramref name="centreX"/>,
    /// <paramref name="centreY"/>, <paramref name="centreZ"/>); null when it does not meet it ahead, or the
    /// direction is shorter than 0.001.
    /// </summary>
    public static ReticlePoint? OnSphere(
        float originX, float originY, float originZ,
        float directionX, float directionY, float directionZ,
        float centreX, float centreY, float centreZ)
    {
        float a = directionX * directionX + directionY * directionY + directionZ * directionZ;
        if (a < 1e-6f)
        {
            return null;
        }

        // |o + t d|² = r², with o the origin relative to the centre: a t² + 2 b t + c = 0.
        float ox = originX - centreX;
        float oy = originY - centreY;
        float oz = originZ - centreZ;
        float b = ox * directionX + oy * directionY + oz * directionZ;
        float c = ox * ox + oy * oy + oz * oz - Radius * Radius;
        float quarterDiscriminant = b * b - a * c;
        if (quarterDiscriminant < 0)
        {
            return null;
        }

        // The far meeting: where the ray leaves the sphere. From inside it is always ahead.
        float t = (-b + MathF.Sqrt(quarterDiscriminant)) / a;
        if (t <= 0)
        {
            return null;
        }

        return new ReticlePoint(originX + t * directionX, originY + t * directionY, originZ + t * directionZ);
    }
}
