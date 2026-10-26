#nullable enable
using System;

namespace PanzaDragoonVR.Logic;

/// <summary>
/// The scroll of the images on the floor and canopy planes: each frame an image is shifted along the
/// direction of flight, and its offset is kept within one tile so the pattern repeats for ever with no
/// seam and the offset never grows. Engine-free, so it is tested on the PC; the game feeds it each
/// frame's delta and gives the shader the offset it returns.
/// </summary>
public static class PlaneScroll
{
    /// <summary>
    /// The offset, in tiles and within [0, 1), after one frame of <paramref name="deltaSeconds"/> at
    /// <paramref name="speed"/> metres a second over tiles of <paramref name="tileMetres"/>. The offset
    /// decreases as the pattern travels, however many tiles the frame spans. A tile size that is not a
    /// positive number leaves the offset where it is; a speed or delta that is not finite gives 0.
    /// </summary>
    public static float Next(float offset, float speed, float deltaSeconds, float tileMetres)
    {
        double next = offset;
        if (tileMetres > 0 && float.IsFinite(tileMetres))
        {
            next -= (double)speed * deltaSeconds / tileMetres;
        }

        if (!double.IsFinite(next))
        {
            return 0f;
        }

        next -= Math.Floor(next);
        var wrapped = (float)next;
        // A value just below a whole number can round up to 1 when narrowed.
        return wrapped >= 1f ? 0f : wrapped;
    }

    /// <summary>
    /// The canopy's speed, in metres a second, that makes it cross the same angle a second overhead as the
    /// floor does below: the floor's <paramref name="floorSpeed"/> times the canopy's
    /// <paramref name="canopyHeight"/> above the eye over the floor's <paramref name="floorDepth"/> below it.
    /// Either distance not a positive number gives the floor's speed.
    /// </summary>
    public static float CanopySpeed(float floorSpeed, float floorDepth, float canopyHeight)
    {
        bool usable = floorDepth > 0 && float.IsFinite(floorDepth) && canopyHeight > 0 && float.IsFinite(canopyHeight);
        return usable ? floorSpeed * canopyHeight / floorDepth : floorSpeed;
    }
}
