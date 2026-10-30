#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's OpenXR job: starts the session, listens to it and pauses the frame-rate readout while the
/// headset is not showing the app.
/// </summary>
public partial class Main
{
    private OpenXRInterface? _openXr;

    /// <summary>Listens to the session that has started, and asks for the refresh rate once it has begun.</summary>
    private void ConnectOpenXr(OpenXRInterface openXr)
    {
        // The headset's rates can be read and set once its session has begun.
        openXr.SessionBegun += RequestRefreshRate;
        openXr.SessionFocussed += RequestRefreshRateAgain;
        openXr.RefreshRateChanged += OnRefreshRateChanged;
        // Only frames the headset paces are measured: those while it is showing the app.
        openXr.SessionVisible += ResumeMeasuring;
        openXr.SessionFocussed += ResumeMeasuring;
        openXr.SessionSynchronized += PauseMeasuring;
        openXr.SessionStopping += PauseMeasuring;
        openXr.SessionLossPending += PauseMeasuring;
        // The board goes in front of the head and the dragon's seat below it once it is tracked, and again when the view is recentred.
        openXr.PoseRecentered += OnPoseRecentered;
        if (SessionHasBegun(openXr))
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
            _openXr.SessionFocussed -= RequestRefreshRateAgain;
            _openXr.RefreshRateChanged -= OnRefreshRateChanged;
            _openXr.SessionVisible -= ResumeMeasuring;
            _openXr.SessionFocussed -= ResumeMeasuring;
            _openXr.SessionSynchronized -= PauseMeasuring;
            _openXr.SessionStopping -= PauseMeasuring;
            _openXr.SessionLossPending -= PauseMeasuring;
            _openXr.PoseRecentered -= OnPoseRecentered;
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
}
