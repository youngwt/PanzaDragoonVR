#nullable enable
using System;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene: a ground, a sky and a flyer circling the player, to look all around at. Loads what the
/// asset folder supplies for them, dressing the ground and sky in its images and flying its model in
/// place of the grey cube, with each one's placeholder for whatever it does not supply; shows what the
/// launch check found on a board in front of the starting view; starts OpenXR, asks the headset for
/// 90 Hz and tells the frame-rate readout the rate it is running at. A disc image in the folder is
/// extracted behind the scene, its progress on the same board, and the folder is read again when it ends.
/// The ground and sky scroll past as though the player were flying forwards, placed and paced by the
/// asset folder's <c>tuning.json</c> or the built-in values.
/// This file holds the scene and its assets; the other jobs are in the <c>Main.*.cs</c> files beside it:
/// extraction, the board, the OpenXR session, the refresh rate, the scroll and the tuning.
/// </summary>
public partial class Main : Node3D
{
    private FrameRateReadout _readout = null!;
    private Shader _tiling = null!;
    private string? _folder;
    private bool _located;

    public override void _Ready()
    {
        _readout = GetNode<FrameRateReadout>("XROrigin3D/XRCamera3D/FrameRateReadout");

        _tiling = new Shader { Code = TilingShader };
        // The scene starts on what the folder already holds; extraction, if any, runs behind it.
        (ImageTexture? ground, ImageTexture? sky, Node3D? flyer, LaunchReport report) = LoadAssets();
        _extraction = StartExtraction(ref report);
        AssetFolder.LogReport(report);
        ShowAssets(ground, sky, flyer, report);
        if (_extraction != null)
        {
            ShowProgress(_extraction.Board);
        }

        _openXr = StartOpenXr();
        if (_openXr == null)
        {
            GD.PushWarning("OpenXR is not initialised; showing the scene on a flat screen.");
            _readout.Begin(RefreshRate.NotInVr(DisplayServer.ScreenGetRefreshRate()));
            return;
        }

        ConnectOpenXr(_openXr);
    }

    /// <summary>
    /// Places the board until the head is tracked, watches extraction while it runs and scrolls the
    /// ground and sky.
    /// </summary>
    public override void _Process(double delta)
    {
        if (_openXr != null && !_boardPlaced)
        {
            PlaceBoard();
        }

        WatchExtraction();
        Scroll(delta);
    }

    /// <summary>
    /// Shows the launch report on the board, dresses the ground and sky in their images or placeholders
    /// and flies the model or the grey cube: at launch, and again when the folder is read after extraction.
    /// </summary>
    private void ShowAssets(ImageTexture? ground, ImageTexture? sky, Node3D? flyer, LaunchReport report)
    {
        _report = report;
        // What the launch check found, readable in the headset; it stays for as long as the app runs.
        // The report is in the log already.
        OnBoard("the launch report", () => Board.Display(report));

        PlacePlanes();
        _groundMaterial = Dress(GetNode<MeshInstance3D>("Ground"), ground ?? PlaceholderTextures.Ground(), _tuning.FloorTile);
        _skyMaterial = Dress(GetNode<MeshInstance3D>("Sky"), sky ?? PlaceholderTextures.Sky(), _tuning.CanopyTile);
        // The new images carry on from where the old ones had scrolled to.
        ApplyScroll();
        var cube = GetNode<FlyingCube>("Cube");
        try
        {
            cube.Carry(flyer);
        }
        catch (Exception e)
        {
            // The grey cube flies instead; the ground and sky keep what they loaded.
            GD.PushWarning($"{AssetFolder.LogTag} {AssetIds.E03Flyer}: the model could not be flown ({DiscExtraction.Reason(e)}); using the placeholder");
            if (flyer != null && IsInstanceValid(flyer))
            {
                flyer.Free();
            }

            cube.Carry(null);
        }
    }

    /// <summary>
    /// The images and model the asset folder supplies, null for each placeholder, and the launch
    /// report with the tuning file's problems, for the caller to log once it is complete. The folder
    /// is found and the tuning read the first time.
    /// Whatever goes wrong in loading, the scene and OpenXR still start: everything is a placeholder
    /// and the report says loading failed.
    /// </summary>
    private (ImageTexture? Ground, ImageTexture? Sky, Node3D? Flyer, LaunchReport Report) LoadAssets()
    {
        AssetFolder? assets = null;
        ImageTexture? ground = null;
        ImageTexture? sky = null;
        Node3D? flyer = null;
        LaunchReport report;
        try
        {
            if (!_located)
            {
                _folder = AssetFolder.Locate();
                _located = true;
            }

            assets = AssetFolder.Open(_folder);
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
            string reason = DiscExtraction.Reason(e);
            GD.PushWarning($"{AssetFolder.LogTag} loading the asset folder failed ({reason}); running on placeholders");
            report = FailedReport(assets, reason);
        }

        // The tuning is read once, from the folder found above (or none); its problems join every report.
        ReadTuning();
        report = report.WithTuningProblems(_tuningProblems);
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

    /// <summary>
    /// Tiles an image over a flat plane, unlit. The coordinates are wrapped here rather than left
    /// to the texture's repeat mode: on the Quest 3 a repeating texture came out clamped to its
    /// edge on these planes, showing one flat colour. The gradients keep the smaller copies of the
    /// image correct across the wrap. The offset, in tiles, scrolls the image along the plane.
    /// </summary>
    private const string TilingShader = """
        shader_type spatial;
        render_mode unshaded;

        uniform sampler2D image : source_color, filter_nearest_mipmap_anisotropic, repeat_enable;
        uniform vec2 tiles = vec2(1.0);
        uniform vec2 offset = vec2(0.0);

        void fragment() {
            vec2 uv = UV * tiles + offset;
            ALBEDO = textureGrad(image, fract(uv), dFdx(uv), dFdy(uv)).rgb;
        }
        """;

    /// <summary>Gives a flat plane a tiling image that needs no light, and returns its material for the scroll.</summary>
    private ShaderMaterial Dress(MeshInstance3D plane, Texture2D image, float tileMetres)
    {
        Vector2 size = ((PlaneMesh)plane.Mesh).Size;
        var material = new ShaderMaterial { Shader = _tiling };
        material.SetShaderParameter("image", image);
        material.SetShaderParameter("tiles", new Vector2(size.X / tileMetres, size.Y / tileMetres));
        plane.MaterialOverride = material;
        return material;
    }
}
