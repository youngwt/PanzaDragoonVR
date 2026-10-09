#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PanzaDragoonVR.Logic;

/// <summary>What came of asking the headset for the wanted refresh rate.</summary>
public enum RefreshRateOutcome
{
    /// <summary>The wanted rate was asked for and the headset is running at it.</summary>
    Set,

    /// <summary>The headset offers the wanted rate, so it is asked for; whether it took effect is not known yet.</summary>
    Requested,

    /// <summary>The wanted rate was asked for, and the headset is not running at it.</summary>
    RequestNotApplied,

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
/// <param name="OfferedCount">How many rates the headset offered.</param>
public readonly record struct RefreshRateChoice(
    RefreshRateOutcome Outcome,
    double? RateToRequest,
    double TargetHz,
    string Summary,
    int OfferedCount = 0)
{
    /// <summary>True when something went other than planned and a warning should be logged.</summary>
    public bool IsWarning => Outcome is not (RefreshRateOutcome.Set or RefreshRateOutcome.Requested);
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

        // The count goes on the readout, so an empty list can be told from a short one in the headset.
        int count = offeredHz?.Count ?? 0;
        string offeredCount = OfferedText(count);

        if (offeredHz == null || count == 0)
        {
            return new RefreshRateChoice(
                RefreshRateOutcome.NoRateList,
                null,
                fallbackTarget,
                $"{inUse}; {Format(wantedHz)} Hz could not be set {offeredCount}");
        }

        foreach (double offered in offeredHz)
        {
            if (IsUsable(offered) && Math.Abs(offered - wantedHz) <= ToleranceHz)
            {
                // Request the headset's own figure, which is the one it will accept.
                // Asking is not having: Confirm says what the headset then runs at.
                return new RefreshRateChoice(
                    RefreshRateOutcome.Requested,
                    offered,
                    wantedHz,
                    $"Asking for {Format(wantedHz)} Hz {offeredCount}",
                    count);
            }
        }

        return new RefreshRateChoice(
            RefreshRateOutcome.NotOffered,
            null,
            fallbackTarget,
            $"{inUse}; {Format(wantedHz)} Hz is not offered {offeredCount}",
            count);
    }

    /// <summary>
    /// Says what came of a request, given the rate the headset is running at afterwards. A headset
    /// can accept a request and carry on at its old rate, so frames are measured against the rate
    /// in use, not the rate asked for. A choice that asked for nothing is returned unchanged.
    /// </summary>
    /// <param name="choice">A choice from <see cref="Choose"/>, or an earlier result of this method.</param>
    /// <param name="inUseHz">The rate in use after the request; zero or less when it is not known.</param>
    public static RefreshRateChoice Confirm(RefreshRateChoice choice, double inUseHz)
    {
        if (choice.RateToRequest is not double asked)
        {
            return choice;
        }

        string offeredCount = OfferedText(choice.OfferedCount);
        if (!IsUsable(inUseHz))
        {
            return choice with
            {
                Outcome = RefreshRateOutcome.RequestNotApplied,
                TargetHz = asked,
                Summary = $"Asked for {Format(asked)} Hz; running at an unknown rate {offeredCount}",
            };
        }

        if (Math.Abs(inUseHz - asked) <= ToleranceHz)
        {
            return choice with
            {
                Outcome = RefreshRateOutcome.Set,
                TargetHz = inUseHz,
                Summary = $"Target {Format(asked)} Hz {offeredCount}",
            };
        }

        return choice with
        {
            Outcome = RefreshRateOutcome.RequestNotApplied,
            TargetHz = inUseHz,
            Summary = $"Asked for {Format(asked)} Hz; running at {Format(inUseHz)} Hz {offeredCount}",
        };
    }

    /// <summary>The choice when OpenXR is not running and the scene is on a flat screen.</summary>
    /// <param name="screenHz">The screen's refresh rate; zero or less when it is not known.</param>
    public static RefreshRateChoice NotInVr(double screenHz)
    {
        double target = IsUsable(screenHz) ? screenHz : FallbackScreenHz;
        return new RefreshRateChoice(RefreshRateOutcome.NotInVr, null, target, "Not in VR");
    }

    private static string OfferedText(int count) => count == 1 ? "(1 rate offered)" : $"({count} rates offered)";

    private static bool IsUsable(double hz) => double.IsFinite(hz) && hz > 0.0;

    private static string Format(double hz) => hz.ToString("0.#", CultureInfo.InvariantCulture);
}
