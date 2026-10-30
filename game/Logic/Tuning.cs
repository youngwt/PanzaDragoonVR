#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace PanzaDragoonVR.Logic;

/// <summary>
/// The scene's tuning: how deep the floor is, how high the canopy, how fast they scroll, how much of
/// each plane one copy of its image covers, and the player's seat on the dragon and its size. Each setting has a built-in value; an optional
/// <c>tuning.json</c> at the top of the asset folder replaces any of them, as the asset contract's
/// "Tuning" section defines. The planes' heights are in metres from the play area's floor, the seat's offsets in metres from the seat, speeds in metres a second.
/// Nothing here uses the engine, so it is tested on the PC; the game reads the file once at launch.
/// </summary>
public sealed record Tuning(
    float FloorDepth,
    float CanopyHeight,
    float FlightSpeed,
    float CanopySpeedScale,
    float FloorTile,
    float CanopyTile,
    float SeatForward,
    float SeatHeight,
    float DragonScale)
{
    public const string FileName = "tuning.json";

    /// <summary>Metres the eye is assumed to be above the play area's floor; it enters only the canopy's speed.</summary>
    public const float EyeHeight = 1.6f;

    /// <summary>
    /// Every setting at its built-in value, the planes' set by the author in the headset on 2026-10-26 to
    /// look like Episode 3 with the extracted planes. The seat's leave the player at the model's origin.
    /// </summary>
    public static Tuning BuiltIn { get; } = new(6f, 10f, 6f, 1f, 24f, 48f, 0f, 0f, 1f);

    /// <summary>
    /// Each setting the file can give: its name in the file, its allowed range (inclusive), how it reads
    /// the value from a tuning and how it replaces it; the built-in values are in <see cref="BuiltIn"/>.
    /// </summary>
    private static readonly Setting[] Settings =
    {
        new("floor_depth", 3, 50, t => t.FloorDepth, (t, v) => t with { FloorDepth = v }),
        new("canopy_height", 4, 100, t => t.CanopyHeight, (t, v) => t with { CanopyHeight = v }),
        new("flight_speed", 0, 30, t => t.FlightSpeed, (t, v) => t with { FlightSpeed = v }),
        new("canopy_speed_scale", 0, 2, t => t.CanopySpeedScale, (t, v) => t with { CanopySpeedScale = v }),
        new("floor_tile", 4, 500, t => t.FloorTile, (t, v) => t with { FloorTile = v }),
        new("canopy_tile", 8, 500, t => t.CanopyTile, (t, v) => t with { CanopyTile = v }),
        new("seat_forward", -20, 20, t => t.SeatForward, (t, v) => t with { SeatForward = v }),
        new("seat_height", -3, 3, t => t.SeatHeight, (t, v) => t with { SeatHeight = v }),
        new("dragon_scale", 0.1, 5, t => t.DragonScale, (t, v) => t with { DragonScale = v }),
    };

    /// <summary>
    /// Metres a second the canopy's image moves: the speed at which it crosses the same angle a second
    /// overhead as the floor does below, seen from <see cref="EyeHeight"/>, times <see cref="CanopySpeedScale"/>.
    /// </summary>
    public float CanopySpeed =>
        PlaneScroll.CanopySpeed(FlightSpeed, FloorDepth + EyeHeight, CanopyHeight - EyeHeight) * CanopySpeedScale;

    /// <summary>The settings as the file names them, for the log: <c>floor_depth 6, canopy_height 10, ...</c>.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        foreach (Setting setting in Settings)
        {
            parts.Add($"{setting.Name} {Shown(setting.Get(this))}");
        }

        return string.Join(", ", parts);
    }

    /// <summary>Reads <c>tuning.json</c> from <paramref name="folder"/>; no folder or no file gives the built-ins and no problems.</summary>
    public static TuningRead Load(string? folder)
    {
        if (folder == null)
        {
            return new TuningRead(BuiltIn, Array.Empty<string>(), Found: false);
        }

        string path = Path.Combine(folder, FileName);
        if (!File.Exists(path))
        {
            return new TuningRead(BuiltIn, Array.Empty<string>(), Found: false);
        }

        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return CannotBeRead(OneLine.Of(e.Message));
        }

        return Parse(text);
    }

    /// <summary>
    /// Reads a tuning file's text. Each setting it gives in range replaces the built-in; each one that
    /// is out of range or not a number, and each name that is not a setting, is a problem, one line each,
    /// and the built-in is kept for it. A file that is not one JSON object gives the built-ins and one problem.
    /// </summary>
    public static TuningRead Parse(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            return CannotBeRead($"is not valid JSON: {OneLine.Of(e.Message)}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return CannotBeRead("must be one JSON object");
            }

            Tuning tuning = BuiltIn;
            var problems = new List<string>();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                Setting? setting = Array.Find(Settings, s => s.Name == property.Name);
                if (setting == null)
                {
                    problems.Add($"{FileName}: '{OneLine.Of(property.Name)}' is not a setting; it is ignored");
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Number
                    && property.Value.TryGetDouble(out double value)
                    && double.IsFinite(value)
                    && value >= setting.Min
                    && value <= setting.Max)
                {
                    tuning = setting.Set(tuning, (float)value);
                    continue;
                }

                problems.Add($"{FileName}: {setting.Name} must be a number from {Shown(setting.Min)} to {Shown(setting.Max)}; " +
                             $"using the built-in {Shown(setting.Get(BuiltIn))}");
            }

            return new TuningRead(tuning, problems, Found: true);
        }
    }

    private static TuningRead CannotBeRead(string reason) =>
        new(BuiltIn, new[] { $"{FileName}: cannot be read: {reason}; using the built-in values" }, Found: true);

    private static string Shown(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private sealed record Setting(string Name, double Min, double Max, Func<Tuning, float> Get, Func<Tuning, float, Tuning> Set);
}

/// <summary>What came of reading the tuning file: the settings to use and its problems, one line each.</summary>
/// <param name="Found">The file was there, whether or not it could be read.</param>
public sealed record TuningRead(Tuning Tuning, IReadOnlyList<string> Problems, bool Found);
