#nullable enable
using System;
using System.Globalization;
using System.Linq;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene: a ground, a sky and a flyer circling the player, to look all around at. Loads what the
/// asset folder supplies for them, dressing the ground and sky in its images and flying its model in
/// place of the grey cube, with each one's placeholder for whatever it does not supply; shows what the
/// launch check found on a board in front of the starting view; starts OpenXR, asks the headset for
/// 90 Hz and tells the frame-rate readout the rate it is running at.
/// </summary>
public partial class Main : Node3D
{
    /// <summary>Metres of ground covered by one copy of the ground image.</summary>
    private const float GroundTileMetres = 2.0f;

    /// <summary>Metres of sky covered by one copy of the sky image.</summary>
    private const float SkyTileMetres = 20.0f;

    private FrameRateReadout _readout = null!;
    private OpenXRInterface? _openXr;
    private Shader _tiling = null!;
    private RefreshRateChoice? _choice;
    private string _lastWarning = "";
    private bool _askedAgain;

    public override void _Ready()
    {
        _readout = GetNode<FrameRateReadout>("XROrigin3D/XRCamera3D/FrameRateReadout");

        _tiling = new Shader { Code = TilingShader };
        (ImageTexture? ground, ImageTexture? sky, Node3D? flyer, LaunchReport report) = LoadAssets();

        // What the launch check found, readable in the headset; it stays for as long as the app runs.
        // A panel that fails to show must not stop OpenXR from starting; the report is in the log already.
        try
        {
            GetNode<LaunchReportPanel>("LaunchReport").Display(report);
        }
        catch (Exception e)
        {
            GD.PushWarning($"{AssetFolder.LogTag} the launch report could not be shown ({e.GetType().Name}: {e.Message})");
        }

        Dress(GetNode<MeshInstance3D>("Ground"), ground ?? PlaceholderTextures.Ground(), GroundTileMetres);
        Dress(GetNode<MeshInstance3D>("Sky"), sky ?? PlaceholderTextures.Sky(), SkyTileMetres);
        if (flyer != null)
        {
            try
            {
                GetNode<FlyingCube>("Cube").Carry(flyer);
            }
            catch (Exception e)
            {
                // The grey cube flies instead; the ground and sky keep what they loaded.
                GD.PushWarning($"{AssetFolder.LogTag} {AssetIds.E03Flyer}: the model could not be flown ({e.GetType().Name}: {e.Message}); using the placeholder");
                if (IsInstanceValid(flyer))
                {
                    flyer.Free();
                }
            }
        }

        _openXr = StartOpenXr();
        if (_openXr == null)
        {
            GD.PushWarning("OpenXR is not initialised; showing the scene on a flat screen.");
            _readout.Begin(RefreshRate.NotInVr(DisplayServer.ScreenGetRefreshRate()));
            return;
        }

        // The headset's rates can be read and set once its session has begun.
        _openXr.SessionBegun += RequestRefreshRate;
        _openXr.SessionFocussed += RequestRefreshRateAgain;
        _openXr.RefreshRateChanged += OnRefreshRateChanged;
        // Only frames the headset paces are measured: those while it is showing the app.
        _openXr.SessionVisible += ResumeMeasuring;
        _openXr.SessionFocussed += ResumeMeasuring;
        _openXr.SessionSynchronized += PauseMeasuring;
        _openXr.SessionStopping += PauseMeasuring;
        _openXr.SessionLossPending += PauseMeasuring;
        if (SessionHasBegun(_openXr))
        {
            RequestRefreshRate();
        }
        else
        {
            _readout.ShowWaiting("Waiting for the headset");
        }
    }

    /// <summary>
    /// The images and model the asset folder supplies, null for each placeholder, and the launch
    /// report, which is logged here once. Extraction from a disc image in the folder runs first.
    /// Whatever goes wrong in loading, the scene and OpenXR still start: everything is a placeholder
    /// and the report says loading failed.
    /// </summary>
    private static (ImageTexture? Ground, ImageTexture? Sky, Node3D? Flyer, LaunchReport Report) LoadAssets()
    {
        AssetFolder? assets = null;
        ImageTexture? ground = null;
        ImageTexture? sky = null;
        Node3D? flyer = null;
        LaunchReport report;
        try
        {
            string? folder = AssetFolder.Locate();
            // Synchronously for now: the scene waits for extraction (story 9 moves it off the main thread).
            DiscExtraction.Run(folder);
            assets = AssetFolder.Open(folder);
            ground = assets.Image(AssetIds.E03Floor);
            sky = assets.Image(AssetIds.E03Canopy);
            flyer = assets.Model(AssetIds.E03Flyer);
            assets.LogSummary();
            report = assets.Report();
        }
        catch (Exception e)
        {
            ground = null;
            sky = null;
            flyer?.Free();
            flyer = null;
            string reason = $"{e.GetType().Name}: {e.Message}";
            GD.PushWarning($"{AssetFolder.LogTag} loading the asset folder failed ({reason}); running on placeholders");
            report = FailedReport(assets, reason);
        }

        AssetFolder.LogReport(report);
        return (ground, sky, flyer, report);
    }

    /// <summary>
    /// The report when loading threw: the folder found and the problems found before the failure are
    /// kept on the board when they can still be had.
    /// </summary>
    private static LaunchReport FailedReport(AssetFolder? assets, string reason)
    {
        if (assets == null)
        {
            return LaunchReport.Failed(null, reason);
        }

        try
        {
            return assets.FailedReport(reason);
        }
        catch (Exception)
        {
            return LaunchReport.Failed(assets.Path, reason);
        }
    }

    public override void _ExitTree()
    {
        if (_openXr != null)
        {
            _openXr.SessionBegun -= RequestRefreshRate;
            _openXr.SessionFocussed -= RequestRefreshRateAgain;
            _openXr.RefreshRateChanged -= OnRefreshRateChanged;
            _openXr.SessionVisible -= ResumeMeasuring;
            _openXr.SessionFocussed -= ResumeMeasuring;
            _openXr.SessionSynchronized -= PauseMeasuring;
            _openXr.SessionStopping -= PauseMeasuring;
            _openXr.SessionLossPending -= PauseMeasuring;
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
        OpenXRInterface.SessionState.Ready or
        OpenXRInterface.SessionState.Synchronized or
        OpenXRInterface.SessionState.Visible or
        OpenXRInterface.SessionState.Focused;

    private static bool SessionIsShown(OpenXRInterface openXr) => openXr.GetSessionState() is
        OpenXRInterface.SessionState.Visible or
        OpenXRInterface.SessionState.Focused;

    private void PauseMeasuring() => _readout.Pause();

    private void ResumeMeasuring() => _readout.Resume();

    /// <summary>
    /// Asks once more, and reads back the rate in use, the first time the session gains focus,
    /// whatever came of the first request: a headset can give an empty list of rates, or not yet
    /// have acted on a request, when its session has only just begun.
    /// </summary>
    private void RequestRefreshRateAgain()
    {
        if (_askedAgain)
        {
            return;
        }

        _askedAgain = true;
        RequestRefreshRate();
    }

    /// <summary>
    /// Asks the headset for 90 Hz when it offers it, then reads back the rate it is running at
    /// and tells the readout what happened.
    /// </summary>
    private void RequestRefreshRate()
    {
        OpenXRInterface openXr = _openXr!;
        double[] offered = openXr.GetAvailableDisplayRefreshRates().Select(rate => rate.AsDouble()).ToArray();
        RefreshRateChoice choice = RefreshRate.Choose(offered, openXr.DisplayRefreshRate);

        if (choice.RateToRequest is double rate)
        {
            openXr.DisplayRefreshRate = (float)rate;
        }

        string offeredList = offered.Length == 0
            ? "none"
            : string.Join(", ", offered.Select(hz => hz.ToString(CultureInfo.InvariantCulture)));
        // The headset can accept the request and stay at its old rate, so read the rate back.
        ShowRateInUse(choice, openXr.DisplayRefreshRate, $" (rates offered: {offeredList})");
    }

    /// <summary>The headset says its rate has changed, which is when a request takes effect.</summary>
    private void OnRefreshRateChanged(double refreshRate)
    {
        if (_choice is { RateToRequest: not null } choice)
        {
            ShowRateInUse(choice, refreshRate, "");
        }
    }

    /// <summary>Measures, steps physics and reports against the rate the headset is running at.</summary>
    private void ShowRateInUse(RefreshRateChoice asked, double inUseHz, string warningDetail)
    {
        RefreshRateChoice choice = RefreshRate.Confirm(asked, inUseHz);
        _choice = choice;

        if (choice.RateToRequest != null)
        {
            // Physics steps once a frame, so movement does not judder against the display.
            Engine.PhysicsTicksPerSecond = Mathf.RoundToInt(choice.TargetHz);
        }

        if (!choice.IsWarning)
        {
            _lastWarning = "";
        }
        else if (choice.Summary != _lastWarning)
        {
            _lastWarning = choice.Summary;
            GD.PushWarning($"{choice.Summary}{warningDetail}.");
        }

        _readout.Begin(choice);
        OpenXRInterface openXr = _openXr!;
        // A session that has begun is not shown yet; measuring starts when the headset shows it.
        if (!SessionIsShown(openXr))
        {
            _readout.Pause();
        }
    }

    /// <summary>
    /// Tiles an image over a flat plane, unlit. The coordinates are wrapped here rather than left
    /// to the texture's repeat mode: on the Quest 3 a repeating texture came out clamped to its
    /// edge on these planes, showing one flat colour. The gradients keep the smaller copies of the
    /// image correct across the wrap.
    /// </summary>
    private const string TilingShader = """
        shader_type spatial;
        render_mode unshaded;

        uniform sampler2D image : source_color, filter_nearest_mipmap_anisotropic, repeat_enable;
        uniform vec2 tiles = vec2(1.0);

        void fragment() {
            vec2 uv = UV * tiles;
            ALBEDO = textureGrad(image, fract(uv), dFdx(uv), dFdy(uv)).rgb;
        }
        """;

    /// <summary>Gives a flat plane a tiling image that needs no light.</summary>
    private void Dress(MeshInstance3D plane, Texture2D image, float tileMetres)
    {
        Vector2 size = ((PlaneMesh)plane.Mesh).Size;
        var material = new ShaderMaterial { Shader = _tiling };
        material.SetShaderParameter("image", image);
        material.SetShaderParameter("tiles", new Vector2(size.X / tileMetres, size.Y / tileMetres));
        plane.MaterialOverride = material;
    }
}
