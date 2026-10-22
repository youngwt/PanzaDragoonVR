#nullable enable
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's scroll job: the ground and sky images slide along the direction of flight, the play
/// area's forward (-Z), so their patterns travel towards +Z under and over the player. The planes stay
/// where they are. The ground moves at the tuning's steady flight speed; the sky moves as a flat backdrop,
/// at the same angle a second overhead as the ground below, times the tuning's canopy speed scale (see
/// <see cref="Tuning.CanopySpeed"/>). The offsets outlast a re-dress after extraction.
/// </summary>
public partial class Main
{
    private ShaderMaterial? _groundMaterial;
    private ShaderMaterial? _skyMaterial;
    private float _groundOffset;
    private float _skyOffset;

    /// <summary>Moves both images on by one frame's flight.</summary>
    private void Scroll(double delta)
    {
        _groundOffset = PlaneScroll.Next(_groundOffset, _tuning.FlightSpeed, (float)delta, _tuning.FloorTile);
        _skyOffset = PlaneScroll.Next(_skyOffset, _tuning.CanopySpeed, (float)delta, _tuning.CanopyTile);
        ApplyScroll();
    }

    /// <summary>
    /// Gives each material its offset. The plane's V runs along +Z, so the offset decreasing in V moves
    /// the pattern towards +Z.
    /// </summary>
    private void ApplyScroll()
    {
        _groundMaterial?.SetShaderParameter("offset", new Vector2(0, _groundOffset));
        _skyMaterial?.SetShaderParameter("offset", new Vector2(0, _skyOffset));
    }
}
