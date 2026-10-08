#nullable enable
using System;
using System.Runtime.InteropServices;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// A developer aid: a label fixed in front of the camera showing the target and measured frame
/// rate and the slow-frame counts, with the same figures written to the log once a second under
/// the tag PDZ-FPS. Debug builds only; a release build shows and logs nothing.
/// </summary>
public partial class FrameRateReadout : Label3D
{
    /// <summary>The tag of the log lines: <c>adb logcat -s PDZ-FPS</c>.</summary>
    public const string LogTag = "PDZ-FPS";

    private const int AndroidLogInfo = 4;

    private static readonly string RuntimeLine = $".NET {System.Environment.Version}";

    private bool _enabled;
    private bool _androidLog;
    private string _summary = "";
    private FrameStats? _stats;
    private ulong _lastFrameUsec;

    public override void _Ready()
    {
        _enabled = OS.IsDebugBuild();
        _androidLog = OS.HasFeature("android");
        Visible = _enabled;
        SetProcess(_enabled);
    }

    /// <summary>Shows a line while there is nothing to measure against yet.</summary>
    public void ShowWaiting(string summary)
    {
        if (!_enabled)
        {
            return;
        }

        _summary = summary;
        _stats = null;
        Text = $"{summary}\n{RuntimeLine}";
        WriteLog($"status=\"{summary}\"");
    }

    /// <summary>Shows the refresh-rate decision and starts measuring frames against its target.</summary>
    public void Begin(RefreshRateChoice choice)
    {
        if (!_enabled)
        {
            return;
        }

        _summary = choice.Summary;
        // A session that begins again with the same target carries on counting.
        if (_stats == null || _stats.TargetHz != choice.TargetHz)
        {
            _stats = new FrameStats(choice.TargetHz);
            _lastFrameUsec = 0;
        }

        Text = $"{_summary}\nMeasuring...\n{RuntimeLine}";
        WriteLog($"status=\"{_summary}\" target={choice.TargetHz:0.#}");
    }

    public override void _Process(double delta)
    {
        if (_stats == null)
        {
            return;
        }

        // The clock, not the engine's delta, which the engine may clamp or smooth.
        ulong now = Time.GetTicksUsec();
        ulong last = _lastFrameUsec;
        _lastFrameUsec = now;
        if (last == 0 || now < last)
        {
            return;
        }

        if (_stats.AddFrame((now - last) / 1_000_000.0, out FrameReport report))
        {
            Text = $"{_summary}\n{report.ToReadoutText()}\n{RuntimeLine}";
            WriteLog(report.ToLogLine());
        }
    }

    private void WriteLog(string line)
    {
        if (_androidLog)
        {
            try
            {
                // Godot's own print goes out under the tag "godot"; this gives the lines their own.
                __android_log_write(AndroidLogInfo, LogTag, line);
                return;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException)
            {
                _androidLog = false;
                GD.PushWarning($"Android's log library could not be called ({e.Message}); {LogTag} lines go to Godot's log instead.");
            }
        }

        GD.Print($"{LogTag} {line}");
    }

    [DllImport("log")]
    private static extern int __android_log_write(
        int priority,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string tag,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string text);
}
