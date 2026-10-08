#nullable enable
using System.Linq;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// Engine spike, story 1.3: a ground, a sky and a flying cube to look all around at, held at 90 Hz.
/// Starts OpenXR, asks the headset for 90 Hz and dresses the ground and sky in their placeholders.
/// </summary>
public partial class Main : Node3D
{
    /// <summary>Metres of ground covered by one copy of the ground image.</summary>
    private const float GroundTileMetres = 2.0f;

    /// <summary>Metres of sky covered by one copy of the sky image.</summary>
    private const float SkyTileMetres = 20.0f;

    private FrameRateReadout _readout = null!;
    private OpenXRInterface? _openXr;

    public override void _Ready()
    {
        _readout = GetNode<FrameRateReadout>("XROrigin3D/XRCamera3D/FrameRateReadout");

        Dress(GetNode<MeshInstance3D>("Ground"), PlaceholderTextures.Ground(), GroundTileMetres);
        Dress(GetNode<MeshInstance3D>("Sky"), PlaceholderTextures.Sky(), SkyTileMetres);

        _openXr = StartOpenXr();
        if (_openXr == null)
        {
            GD.PushWarning("OpenXR is not initialised; showing the scene on a flat screen.");
            _readout.Begin(RefreshRate.NotInVr(DisplayServer.ScreenGetRefreshRate()));
            return;
        }

        // The headset's rates can be read and set once its session has begun.
        _openXr.SessionBegun += RequestRefreshRate;
        if (SessionHasBegun(_openXr))
        {
            RequestRefreshRate();
        }
        else
        {
            _readout.ShowWaiting("Waiting for the headset");
        }
    }

    public override void _ExitTree()
    {
        if (_openXr != null)
        {
            _openXr.SessionBegun -= RequestRefreshRate;
        }
    }

    private OpenXRInterface? StartOpenXr()
    {
        if (XRServer.FindInterface("OpenXR") is not OpenXRInterface openXr || !openXr.IsInitialized())
        {
            return null;
        }

        // The headset's compositor paces the frames, not the display's vsync.
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        GetViewport().UseXR = true;
        return openXr;
    }

    private static bool SessionHasBegun(OpenXRInterface openXr) => openXr.GetSessionState() is
        OpenXRInterface.SessionState.Synchronized or
        OpenXRInterface.SessionState.Visible or
        OpenXRInterface.SessionState.Focused;

    /// <summary>Asks the headset for 90 Hz when it offers it, and tells the readout what happened.</summary>
    private void RequestRefreshRate()
    {
        OpenXRInterface openXr = _openXr!;
        double[] offered = openXr.GetAvailableDisplayRefreshRates().Select(rate => rate.AsDouble()).ToArray();
        RefreshRateChoice choice = RefreshRate.Choose(offered, openXr.DisplayRefreshRate);

        if (choice.RateToRequest is double rate)
        {
            openXr.DisplayRefreshRate = (float)rate;
            // Physics steps once a frame, so movement does not judder against the display.
            Engine.PhysicsTicksPerSecond = Mathf.RoundToInt(choice.TargetHz);
        }

        if (choice.IsWarning)
        {
            string offeredList = offered.Length == 0 ? "none" : string.Join(", ", offered);
            GD.PushWarning($"{choice.Summary} (rates offered: {offeredList}).");
        }

        _readout.Begin(choice);
    }

    /// <summary>Gives a flat plane a tiling image that needs no light.</summary>
    private static void Dress(MeshInstance3D plane, Texture2D image, float tileMetres)
    {
        Vector2 size = ((PlaneMesh)plane.Mesh).Size;
        plane.MaterialOverride = new StandardMaterial3D
        {
            AlbedoTexture = image,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            // Sharp squares close to, smaller copies of the image far off.
            TextureFilter = BaseMaterial3D.TextureFilterEnum.NearestWithMipmapsAnisotropic,
            Uv1Scale = new Vector3(size.X / tileMetres, size.Y / tileMetres, 1.0f),
        };
    }
}
