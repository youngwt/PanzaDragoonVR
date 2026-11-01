#nullable enable
using System;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The development mode's tuning list: only when the asset folder holds the <c>development</c> marker, adds both
/// controllers and a label above the left one, feeds the right one's readings to <see cref="TuningMenu"/>, moves
/// the scene as the values change and saves <c>tuning.json</c> on the press of A. In release mode none of it exists.
/// </summary>
public partial class Main
{
    private TuningMenu? _tuningMenu;
    private XRController3D _tuningHand = null!;
    private Label3D _tuningList = null!;

    /// <summary>Sets the list up, hidden, once the folder is found and the tuning read; does nothing unless in development mode.</summary>
    private void StartTuningMenu()
    {
        if (_folder == null || ExtractionModes.ModeOf(_folder) != ExtractionMode.Development)
        {
            return;
        }

        _tuningMenu = new TuningMenu(_tuning);
        var origin = GetNode<XROrigin3D>("XROrigin3D");
        var left = new XRController3D { Tracker = "left_hand" };
        origin.AddChild(left);
        _tuningHand = new XRController3D { Tracker = "right_hand" };
        origin.AddChild(_tuningHand);
        _tuningList = new Label3D
        {
            Position = new Vector3(0, 0.12f, -0.05f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            RenderPriority = 10,
            OutlineRenderPriority = 9,
            PixelSize = 0.0004f,
            FontSize = 32,
            OutlineSize = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Width = 800,
            Visible = false,
        };
        left.AddChild(_tuningList);
        GD.Print($"{AssetFolder.LogTag} development mode: B on the right controller shows the tuning list");
    }

    /// <summary>One frame of the list. The runtime gives the app no input unless it has the focus, so nothing moves then.</summary>
    private void DriveTuningMenu(double delta)
    {
        if (_tuningMenu == null || _openXr?.GetSessionState() != OpenXRInterface.SessionState.Focused)
        {
            return;
        }

        Vector2 stick = _tuningHand.GetVector2("primary");
        TuningMenuStep step = _tuningMenu.Step(
            new TuningMenuReadings(stick.X, stick.Y, _tuningHand.IsButtonPressed("ax_button"), _tuningHand.IsButtonPressed("by_button")),
            delta);
        if (step.TuningChanged)
        {
            _tuning = _tuningMenu.Tuning;
            PlacePlanesAndSeat();
            SetTiles(_floorMaterial, Ground, _tuning.FloorTile);
            SetTiles(_canopyMaterial, Sky, _tuning.CanopyTile);
        }

        if (step.SaveAsked)
        {
            SaveTuning();
        }

        _tuningList.Visible = _tuningMenu.Shown;
        if (_tuningMenu.Shown)
        {
            _tuningList.Text = _tuningMenu.Text();
        }
    }

    /// <summary>Saves the values in use, tells the list and logs how it went.</summary>
    private void SaveTuning()
    {
        string? reason = _tuning.Save(_folder!);
        _tuningMenu!.Saved(reason);
        if (reason == null)
        {
            // The file now holds only good values, so a re-dress after extraction must not bring back the launch's problems.
            _tuningProblems = Array.Empty<string>();
            GD.Print($"{AssetFolder.LogTag} {Tuning.FileName}: saved from the tuning list: {_tuning.Describe()}");
        }
        else
        {
            GD.PushWarning($"{AssetFolder.LogTag} {Tuning.FileName}: not saved from the tuning list ({reason}); the old file stays");
        }
    }
}
