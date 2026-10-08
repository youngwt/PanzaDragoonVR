#nullable enable
using System;
using System.Globalization;

namespace PanzaDragoonVR.Logic;

/// <summary>The frame-rate figures for one finished window of about a second.</summary>
/// <param name="TargetHz">The rate frames are measured against.</param>
/// <param name="AverageHz">Frames in the window divided by the window's length.</param>
/// <param name="SlowFrames">Slow frames in the window.</param>
/// <param name="TotalSlowFrames">Slow frames since counting started.</param>
/// <param name="TotalFrames">Frames since counting started.</param>
public readonly record struct FrameReport(
    double TargetHz,
    double AverageHz,
    int SlowFrames,
    long TotalSlowFrames,
    long TotalFrames)
{
    /// <summary>Slow frames since the start as a percentage of all frames.</summary>
    public double SlowPercent => TotalFrames == 0 ? 0.0 : 100.0 * TotalSlowFrames / TotalFrames;

    /// <summary>The line written to the device log once a second, as key=value pairs.</summary>
    public string ToLogLine() => string.Format(
        CultureInfo.InvariantCulture,
        "target={0:0.#} avg={1:0.0} slow={2} slow_total={3} frames_total={4} slow_pct={5:0.00}",
        TargetHz, AverageHz, SlowFrames, TotalSlowFrames, TotalFrames, SlowPercent);

    /// <summary>The two lines the readout shows.</summary>
    public string ToReadoutText() => string.Format(
        CultureInfo.InvariantCulture,
        "Measured {0:0.0} Hz\nSlow frames: {1} last second, {2} since start",
        AverageHz, SlowFrames, TotalSlowFrames);
}

/// <summary>
/// Counts frames and slow frames and averages the rate over windows of about a second.
/// No engine types, so it is unit-tested off the headset.
/// </summary>
public sealed class FrameStats
{
    /// <summary>A frame is slow when it lasts longer than this many target intervals.</summary>
    public const double SlowFrameFactor = 1.5;

    private readonly double _slowSeconds;
    private readonly double _windowSeconds;

    private double _windowElapsed;
    private int _windowFrames;
    private int _windowSlowFrames;

    /// <param name="targetHz">The rate frames are measured against; must be above zero.</param>
    /// <param name="windowSeconds">How long a window lasts before it is reported.</param>
    public FrameStats(double targetHz, double windowSeconds = 1.0)
    {
        if (!double.IsFinite(targetHz) || targetHz <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHz), targetHz, "The target rate must be above zero.");
        }

        if (!double.IsFinite(windowSeconds) || windowSeconds <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "The window must be longer than zero.");
        }

        TargetHz = targetHz;
        _slowSeconds = SlowFrameFactor / targetHz;
        _windowSeconds = windowSeconds;
    }

    /// <summary>The rate frames are measured against.</summary>
    public double TargetHz { get; }

    /// <summary>Frames since counting started.</summary>
    public long TotalFrames { get; private set; }

    /// <summary>Slow frames since counting started.</summary>
    public long TotalSlowFrames { get; private set; }

    /// <summary>Records one frame. Returns true when it completed a window, with that window's report.</summary>
    /// <param name="frameSeconds">How long the frame lasted. A value that is not a real, non-negative time is ignored.</param>
    /// <param name="report">The finished window's figures when the result is true.</param>
    public bool AddFrame(double frameSeconds, out FrameReport report)
    {
        report = default;
        if (!double.IsFinite(frameSeconds) || frameSeconds < 0.0)
        {
            return false;
        }

        TotalFrames++;
        _windowFrames++;
        _windowElapsed += frameSeconds;
        if (frameSeconds > _slowSeconds)
        {
            TotalSlowFrames++;
            _windowSlowFrames++;
        }

        if (_windowElapsed < _windowSeconds)
        {
            return false;
        }

        report = new FrameReport(
            TargetHz,
            _windowFrames / _windowElapsed,
            _windowSlowFrames,
            TotalSlowFrames,
            TotalFrames);
        _windowElapsed = 0.0;
        _windowFrames = 0;
        _windowSlowFrames = 0;
        return true;
    }
}
