#nullable enable
using System;
using System.Globalization;
using System.Text;

namespace PanzaDragoonVR.Logic;

/// <summary>One frame's readings from the right controller; a controller that is asleep reads all zero and false.</summary>
public readonly record struct TuningMenuReadings(float StickX, float StickY, bool A, bool B);

/// <summary>What one frame of the list asks of the game.</summary>
/// <param name="TuningChanged">The value in <see cref="TuningMenu.Tuning"/> changed: the scene is to follow it.</param>
/// <param name="SaveAsked">A was pressed with the list shown: the game saves and calls <see cref="TuningMenu.Saved"/>.</param>
public readonly record struct TuningMenuStep(bool TuningChanged, bool SaveAsked);

/// <summary>
/// The development mode's list of tuning settings: B shows or hides it, the stick pushed up or down picks a
/// setting, held left or right changes it, and A asks for a save. While it is hidden only B does anything.
/// Presses and pushes begun while it is hidden do not act when it appears, nor does an A pressed on the frame
/// that shows it. Nothing here uses the engine, so it is tested on the PC; the game feeds it each frame's
/// readings.
/// </summary>
public sealed class TuningMenu
{
    /// <summary>How far the stick must be pushed up or down to move the pick.</summary>
    private const float PickAt = 0.7f;

    /// <summary>How near the middle the stick must come back for the next pick.</summary>
    private const float RearmBelow = 0.3f;

    /// <summary>How far the stick must be pushed left or right to change the value.</summary>
    public const float ChangeAbove = 0.2f;

    private bool _pickHeld;
    private bool _changeHeldSinceHidden;
    private bool _aHeld;
    private bool _bHeld;
    private string _note = "";

    public TuningMenu(Tuning tuning)
    {
        Tuning = tuning;
    }

    /// <summary>The settings as they now stand.</summary>
    public Tuning Tuning { get; private set; }

    /// <summary>Whether the list is on show; it starts hidden.</summary>
    public bool Shown { get; private set; }

    /// <summary>The index in <see cref="Tuning.Settings"/> of the setting picked.</summary>
    public int Picked { get; private set; }

    /// <summary>Takes one frame's readings, <paramref name="delta"/> seconds after the last.</summary>
    public TuningMenuStep Step(TuningMenuReadings readings, double delta)
    {
        bool pressedA = readings.A && !_aHeld;
        if (readings.B && !_bHeld)
        {
            Shown = !Shown;
            // An A pressed with the B that shows the list was pressed while it was hidden: it saves nothing.
            pressedA &= !Shown;
        }

        _bHeld = readings.B;
        _aHeld = readings.A;

        Pick(readings.StickY);
        NoteChangeHeldWhileHidden(readings.StickX);
        if (!Shown)
        {
            return default;
        }

        return new TuningMenuStep(Change(readings.StickX, delta), pressedA);
    }

    /// <summary>Tells the list how a save it asked for went: <paramref name="reason"/> is null when saved.</summary>
    public void Saved(string? reason)
    {
        _note = reason == null ? "saved" : $"not saved: {reason}";
    }

    /// <summary>
    /// The list's text: one line a setting to three decimals, as the built-ins are given, the picked one marked,
    /// then the controls and how the last save went.
    /// </summary>
    public string Text()
    {
        var text = new StringBuilder();
        for (int i = 0; i < Tuning.Settings.Count; i++)
        {
            Tuning.Setting setting = Tuning.Settings[i];
            text.Append(i == Picked ? "> " : "  ")
                .Append(setting.Name).Append("  ")
                .Append(setting.Get(Tuning).ToString("0.000", CultureInfo.InvariantCulture))
                .Append('\n');
        }

        text.Append("stick pick/change, A save, B hide");
        if (_note.Length > 0)
        {
            text.Append('\n').Append(_note);
        }

        return text.ToString();
    }

    /// <summary>Moves the pick once a push, wrapping; the hold is followed while hidden too.</summary>
    private void Pick(float stickY)
    {
        if (Math.Abs(stickY) > PickAt)
        {
            if (!_pickHeld && Shown)
            {
                int count = Tuning.Settings.Count;
                Picked = (Picked + (stickY > 0 ? count - 1 : 1)) % count;
            }

            _pickHeld = true;
        }
        else if (Math.Abs(stickY) < RearmBelow)
        {
            _pickHeld = false;
        }
    }

    /// <summary>Notes a sideways hold begun while hidden; it changes nothing until the stick comes back within <see cref="ChangeAbove"/>.</summary>
    private void NoteChangeHeldWhileHidden(float stickX)
    {
        if (Math.Abs(stickX) <= ChangeAbove)
        {
            _changeHeldSinceHidden = false;
        }
        else if (!Shown)
        {
            _changeHeldSinceHidden = true;
        }
    }

    /// <summary>Moves the picked value at its rate, stopping at its range's ends; never while a pick is held. True if it moved.</summary>
    private bool Change(float stickX, double delta)
    {
        if (_pickHeld || _changeHeldSinceHidden || Math.Abs(stickX) <= ChangeAbove)
        {
            return false;
        }

        Tuning.Setting setting = Tuning.Settings[Picked];
        float before = setting.Get(Tuning);
        var after = (float)Math.Clamp(before + (stickX * setting.Rate * delta), setting.Min, setting.Max);
        if (after == before)
        {
            return false;
        }

        Tuning = setting.Set(Tuning, after);
        _note = "";
        return true;
    }
}
