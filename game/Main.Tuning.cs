#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's tuning job: reads <c>tuning.json</c> from the asset folder once, at launch, and places the
/// floor and canopy planes and the dragon's seat from it. Its problems join the launch report each time it is built, so they
/// survive the board's rebuild after extraction. A change to the file is seen when the app is started again.
/// </summary>
public partial class Main
{
    private Tuning _tuning = Tuning.BuiltIn;
    private IReadOnlyList<string> _tuningProblems = Array.Empty<string>();
    private bool _tuningRead;

    /// <summary>
    /// Reads the tuning file from <see cref="_folder"/> the first time it is called and logs what it gives;
    /// later calls do nothing. Nothing in it stops the game: a problem keeps that setting's built-in value.
    /// </summary>
    private void ReadTuning()
    {
        if (_tuningRead)
        {
            return;
        }

        _tuningRead = true;
        TuningRead read;
        try
        {
            read = Tuning.Load(_folder);
        }
        catch (Exception e)
        {
            read = new TuningRead(Tuning.BuiltIn,
                new[] { $"{Tuning.FileName}: cannot be read: {DiscExtraction.Reason(e)}; using the built-in values" }, Found: true);
        }

        _tuning = read.Tuning;
        _tuningProblems = read.Problems;
        foreach (string problem in read.Problems)
        {
            GD.PushWarning($"{AssetFolder.LogTag} {problem}");
        }

        string source = read.Found ? $"from {Tuning.FileName} and the built-ins" : $"built-in, no {Tuning.FileName}";
        GD.Print($"{AssetFolder.LogTag} tuning ({source}): {_tuning.Describe()}");
    }

    /// <summary>Puts the floor plane <see cref="Tuning.FloorDepth"/> below the play area's floor and the canopy plane <see cref="Tuning.CanopyHeight"/> above it, and adjusts the dragon's seat.</summary>
    private void PlacePlanes()
    {
        GetNode<Node3D>("Ground").Position = new Vector3(0, -_tuning.FloorDepth, 0);
        GetNode<Node3D>("Sky").Position = new Vector3(0, _tuning.CanopyHeight, 0);
        GetNode<RiddenDragon>("Dragon").Adjust(_tuning.SeatForward, _tuning.SeatHeight, _tuning.DragonScale);
    }
}
