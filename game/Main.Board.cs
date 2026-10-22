#nullable enable
using System;
using System.Globalization;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's board job: the launch report board, what goes on it, and standing it in front of the head.
/// </summary>
public partial class Main
{
    private bool _boardPlaced;
    private LaunchReport _report = LaunchReport.Nothing;

    private LaunchReportPanel Board => GetNode<LaunchReportPanel>("LaunchReport");

    /// <summary>
    /// Runs <paramref name="show"/>, which puts <paramref name="what"/> on the board. A board that fails
    /// to show must not stop the scene or OpenXR: the failure is one warning and the scene carries on.
    /// </summary>
    private static void OnBoard(string what, Action show)
    {
        try
        {
            show();
        }
        catch (Exception e)
        {
            GD.PushWarning($"{AssetFolder.LogTag} {what} could not be shown ({DiscExtraction.Reason(e)})");
        }
    }

    private void OnPoseRecentered() => _boardPlaced = false;

    /// <summary>
    /// Stands the launch report board in front of where the head faces, once the headset tracks it:
    /// the head pose is not tracked on the first frames, so this is tried each frame until it is.
    /// </summary>
    private void PlaceBoard()
    {
        if (XRServer.GetTracker("head") is not XRPositionalTracker head || head.GetPose("default") is not XRPose pose || !pose.HasTrackingData)
        {
            return;
        }

        Transform3D camera = GetNode<XRCamera3D>("XROrigin3D/XRCamera3D").GlobalTransform;
        Vector3 forward = -camera.Basis.Z;
        if (BoardPlacement.InFrontOf(camera.Origin.X, camera.Origin.Y, camera.Origin.Z, forward.X, forward.Y, forward.Z) is not BoardPose place)
        {
            return;
        }

        GetNode<Node3D>("LaunchReport").GlobalTransform = new Transform3D(new Basis(Vector3.Up, place.Yaw), new Vector3(place.X, place.Y, place.Z));
        _boardPlaced = true;
        GD.Print(string.Create(CultureInfo.InvariantCulture, $"{AssetFolder.LogTag} board placed in front of the head at ({place.X:0.00}, {place.Y:0.00}, {place.Z:0.00})"));
    }
}
