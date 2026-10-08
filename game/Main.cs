using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// Engine spike, story 1.1: proves that C# runs on the Quest 3.
/// Starts OpenXR, spins a cube every frame and shows the .NET runtime version.
/// </summary>
public partial class Main : Node3D
{
    private const float TurnRadiansPerSecond = 1.0f;

    private Node3D _cube = null!;

    public override void _Ready()
    {
        _cube = GetNode<Node3D>("Cube");

        string xrState = StartOpenXr() ? "OpenXR running" : "OpenXR not running";
        GetNode<Label3D>("RuntimeLabel").Text = $".NET {System.Environment.Version}\n{xrState}";
    }

    public override void _Process(double delta)
    {
        _cube.RotateY(TurnRadiansPerSecond * (float)delta);
    }

    private bool StartOpenXr()
    {
        XRInterface? openXr = XRServer.FindInterface("OpenXR");
        if (openXr == null || !openXr.IsInitialized())
        {
            GD.PushWarning("OpenXR is not initialised; showing the scene on a flat screen.");
            return false;
        }

        // The headset's compositor paces the frames, not the display's vsync.
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        GetViewport().UseXR = true;
        return true;
    }
}
