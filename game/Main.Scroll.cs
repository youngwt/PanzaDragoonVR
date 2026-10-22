#nullable enable
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's scroll job: the ground and sky images slide along the direction of flight, the play
/// area's forward (-Z), so their patterns travel towards +Z under and over the player. The planes stay
/// where they are. The ground moves at a steady flight speed; the sky moves as a flat backdrop, at the
/// same angle a second overhead as the ground below. The offsets outlast a re-dress after extraction.
/// </summary>
public partial class Main
{
    /// <summary>Metres a second the ground's pattern moves past.</summary>
    private const float FlightSpeed = 4.0f;

    /// <summary>Metres the eye is assumed to be above the ground: the camera's height in the scene.</summary>
    private const float EyeHeight = 1.6f;

    /// <summary>Metres the sky plane is above the ground in the scene.</summary>
    private const float SkyHeight = 40.0f;

    /// <summary>Metres a second the sky's pattern moves past.</summary>
    private static readonly float SkySpeed = PlaneScroll.SkySpeed(FlightSpeed, EyeHeight, SkyHeight - EyeHeight);

    private ShaderMaterial? _groundMaterial;
    private ShaderMaterial? _skyMaterial;
    private float _groundOffset;
    private float _skyOffset;

    /// <summary>Moves both images on by one frame's flight.</summary>
    private void Scroll(double delta)
    {
        _groundOffset = PlaneScroll.Next(_groundOffset, FlightSpeed, (float)delta, GroundTileMetres);
        _skyOffset = PlaneScroll.Next(_skyOffset, SkySpeed, (float)delta, SkyTileMetres);
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
