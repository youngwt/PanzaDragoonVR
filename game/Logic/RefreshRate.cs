#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PanzaDragoonVR.Logic;

/// <summary>What came of asking the headset for the wanted refresh rate.</summary>
public enum RefreshRateOutcome
{
    /// <summary>The headset offers the wanted rate, so it is requested.</summary>
    Set,

    /// <summary>The headset lists its rates and the wanted one is not among them.</summary>
    NotOffered,

    /// <summary>The runtime gave no list of rates, so none can be requested.</summary>
    NoRateList,

    /// <summary>OpenXR is not running; the scene is on a flat screen.</summary>
    NotInVr,
}

/// <summary>The decision about the refresh rate, and the words the readout shows for it.</summary>
/// <param name="Outcome">Which row of the story's matrix applies.</param>
/// <param name="RateToRequest">The rate to ask the headset for, or null to leave the rate as it is.</param>
/// <param name="TargetHz">The rate frames are measured against.</param>
/// <param name="Summary">One line for the readout and the log.</param>
public readonly record struct RefreshRateChoice(
    RefreshRateOutcome Outcome,
    double? RateToRequest,
    double TargetHz,
    string Summary)
{
    /// <summary>True when something went other than planned and a warning should be logged.</summary>
    public bool IsWarning => Outcome != RefreshRateOutcome.Set;
}

/// <summary>Chooses the display refresh rate. No engine types, so it is unit-tested off the headset.</summary>
public static class RefreshRate
{
    /// <summary>The rate the game is built to hold.</summary>
    public const double WantedHz = 90.0;

    /// <summary>The rate assumed for a flat screen that does not report its own.</summary>
    public const double FallbackScreenHz = 60.0;

    // Runtimes report rates as floats, so 90 may arrive as 89.99 or 90.0001.
    private const double ToleranceHz = 0.5;

    /// <summary>Decides what to do given the rates the headset offers and the rate in use.</summary>
    /// <param name="offeredHz">The headset's list of refresh rates; null or empty when it gave none.</param>
    /// <param name="currentHz">The rate in use; zero or less when it is not known.</param>
    /// <param name="wantedHz">The rate to ask for.</param>
    public static RefreshRateChoice Choose(
        IReadOnlyList<double>? offeredHz,
        double currentHz,
        double wantedHz = WantedHz)
    {
        bool currentKnown = IsUsable(currentHz);
        // With the rate left alone, frames are measured against the rate in use.
        double fallbackTarget = currentKnown ? currentHz : wantedHz;
        string inUse = currentKnown ? $"Running at {Format(currentHz)} Hz" : "Running at an unknown rate";

        if (offeredHz == null || offeredHz.Count == 0)
        {
            return new RefreshRateChoice(
                RefreshRateOutcome.NoRateList,
                null,
                fallbackTarget,
                $"{inUse}; {Format(wantedHz)} Hz could not be set");
        }

        foreach (double offered in offeredHz)
        {
            if (IsUsable(offered) && Math.Abs(offered - wantedHz) <= ToleranceHz)
            {
                // Request the headset's own figure, which is the one it will accept.
                return new RefreshRateChoice(
                    RefreshRateOutcome.Set,
                    offered,
                    wantedHz,
                    $"Target {Format(wantedHz)} Hz");
            }
        }

        return new RefreshRateChoice(
            RefreshRateOutcome.NotOffered,
            null,
            fallbackTarget,
            $"{inUse}; {Format(wantedHz)} Hz is not offered");
    }

    /// <summary>The choice when OpenXR is not running and the scene is on a flat screen.</summary>
    /// <param name="screenHz">The screen's refresh rate; zero or less when it is not known.</param>
    public static RefreshRateChoice NotInVr(double screenHz)
    {
        double target = IsUsable(screenHz) ? screenHz : FallbackScreenHz;
        return new RefreshRateChoice(RefreshRateOutcome.NotInVr, null, target, "Not in VR");
    }

    private static bool IsUsable(double hz) => double.IsFinite(hz) && hz > 0.0;

    private static string Format(double hz) => hz.ToString("0.#", CultureInfo.InvariantCulture);
}
