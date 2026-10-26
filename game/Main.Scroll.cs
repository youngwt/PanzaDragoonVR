#nullable enable
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's scroll job: the floor and canopy images slide along the direction of flight, the play
/// area's forward (-Z), so their patterns travel towards +Z under and over the player. The planes stay
/// where they are. The floor moves at the tuning's steady flight speed; the canopy moves as a flat backdrop,
/// at the same angle a second overhead as the floor below, times the tuning's canopy speed scale (see
/// <see cref="Tuning.CanopySpeed"/>). The offsets outlast a re-dress after extraction.
/// </summary>
public partial class Main
{
    private ShaderMaterial? _floorMaterial;
    private ShaderMaterial? _canopyMaterial;
    private float _floorOffset;
    private float _canopyOffset;

    /// <summary>Moves both images on by one frame's flight.</summary>
    private void Scroll(double delta)
    {
        _floorOffset = PlaneScroll.Next(_floorOffset, _tuning.FlightSpeed, (float)delta, _tuning.FloorTile);
        _canopyOffset = PlaneScroll.Next(_canopyOffset, _tuning.CanopySpeed, (float)delta, _tuning.CanopyTile);
        ApplyScroll();
    }

    /// <summary>
    /// Gives each material its offset. The plane's V runs along +Z, so the offset decreasing in V moves
    /// the pattern towards +Z.
    /// </summary>
    private void ApplyScroll()
    {
        _floorMaterial?.SetShaderParameter("offset", new Vector2(0, _floorOffset));
        _canopyMaterial?.SetShaderParameter("offset", new Vector2(0, _canopyOffset));
    }
}
