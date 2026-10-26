#nullable enable
using System.Globalization;
using System.Linq;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's refresh-rate job: asks the headset for 90 Hz, reads back the rate it runs at, steps
/// physics at that rate and tells the frame-rate readout.
/// </summary>
public partial class Main
{
    private RefreshRateChoice? _choice;
    private string _lastWarning = "";
    private bool _askedAgain;

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
}
