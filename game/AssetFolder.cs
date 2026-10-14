#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The asset folder outside the APK, read through its <c>manifest.json</c> as the asset contract says.
/// On the headset it is the app's external files folder,
/// <c>/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/</c>; on a desktop it is the folder
/// the <c>PDZ_ASSETS</c> environment variable names. Every fallback to a placeholder is logged with
/// the ID and the reason, each line starting with <see cref="LogTag"/>.
/// </summary>
public sealed class AssetFolder
{
    /// <summary>Starts every log line, so <c>adb logcat -d | grep PDZ-ASSETS</c> finds them.</summary>
    public const string LogTag = "PDZ-ASSETS";

    /// <summary>On a desktop, the environment variable that names the asset folder.</summary>
    public const string EnvironmentVariable = "PDZ_ASSETS";

    /// <summary>The app's package, as in <c>export_presets.cfg</c>.</summary>
    private const string Package = "io.github.youngwt.panzadragoonvr";

    /// <summary>
    /// The app's external files folder as a fixed path, used when Android's own answer cannot be had.
    /// <c>/sdcard</c> is a link to <c>/storage/emulated/0</c>.
    /// </summary>
    private const string FixedAndroidPath = "/storage/emulated/0/Android/data/" + Package + "/files";

    private readonly ManifestRead _read;
    private readonly List<string> _supplied = new();
    private readonly List<string> _placeholders = new();

    private AssetFolder(string? path, ManifestRead read)
    {
        Path = path;
        _read = read;
    }

    /// <summary>The asset folder, or null on a desktop with <c>PDZ_ASSETS</c> unset.</summary>
    public string? Path { get; }

    /// <summary>Finds the folder and reads its manifest, logging what came of it.</summary>
    public static AssetFolder Open()
    {
        string? path = Locate();
        if (path == null)
        {
            Log($"{EnvironmentVariable} is not set, so there is no asset folder: running on placeholders");
            return new AssetFolder(null, new ManifestRead(ManifestStatus.Absent, null, null));
        }

        ManifestRead read = AssetManifest.Load(path);
        switch (read.Status)
        {
            case ManifestStatus.Absent:
                Log($"no {AssetManifest.FileName} in {path}: running on placeholders");
                break;
            case ManifestStatus.Unreadable:
                Warn($"the manifest cannot be read: {read.Problem}; running on placeholders (folder {path})");
                break;
            case ManifestStatus.UnsupportedFormat:
                Warn($"{read.Problem}; running on placeholders (folder {path})");
                break;
            default:
                Log($"reading {AssetManifest.FileName} in {path}");
                break;
        }

        return new AssetFolder(path, read);
    }

    /// <summary>
    /// The image the folder supplies for <paramref name="id"/>, ready to tile, or null for the
    /// placeholder. A fallback for any reason other than the ID not being listed is logged.
    /// </summary>
    public ImageTexture? Image(string id)
    {
        AssetLookup lookup = _read.Find(id, AssetManifest.ImageKind);
        switch (lookup.Status)
        {
            case AssetLookupStatus.NotListed:
                _placeholders.Add(id);
                return null;
            case AssetLookupStatus.Problem:
                Warn($"{lookup.Problem}; using the placeholder for {id}");
                _placeholders.Add(id);
                return null;
        }

        string full = lookup.FullPath!;
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Warn($"{id}: {full}: cannot be read: {e.Message}; using the placeholder for {id}");
            _placeholders.Add(id);
            return null;
        }

        // Decoded as a PNG whatever the file's name, since the image kind is PNG.
        var image = new Godot.Image();
        Error error = image.LoadPngFromBuffer(bytes);
        if (error != Error.Ok || image.IsEmpty())
        {
            Warn($"{id}: {full}: unreadable image: the PNG could not be decoded ({error}); using the placeholder for {id}");
            _placeholders.Add(id);
            return null;
        }

        // Smaller copies for the distance, as for the placeholders, so far tiles do not shimmer.
        image.GenerateMipmaps();
        Log($"{id}: using {full} ({image.GetWidth()} x {image.GetHeight()})");
        _supplied.Add(id);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>Logs which IDs the folder supplied and which run on their placeholders.</summary>
    public void LogSummary()
    {
        if (_supplied.Count == 0)
        {
            Log($"no image was supplied: running on placeholders ({string.Join(", ", _placeholders)})");
            return;
        }

        string placeholders = _placeholders.Count == 0 ? "none" : string.Join(", ", _placeholders);
        Log($"supplied: {string.Join(", ", _supplied)}; placeholders: {placeholders}");
    }

    /// <summary>
    /// Where the asset folder is: on Android the app's external files folder, asked of Android
    /// through Godot's Android runtime bridge, else the fixed path; elsewhere <c>PDZ_ASSETS</c>.
    /// </summary>
    private static string? Locate()
    {
        if (!OS.HasFeature("android"))
        {
            string named = OS.GetEnvironment(EnvironmentVariable);
            return string.IsNullOrEmpty(named) ? null : named;
        }

        try
        {
            string? asked = AskAndroid();
            if (!string.IsNullOrEmpty(asked))
            {
                return asked;
            }

            Log($"Android gave no external files folder; using {FixedAndroidPath}");
        }
        catch (Exception e)
        {
            Log($"Android could not be asked for the external files folder ({e.Message}); using {FixedAndroidPath}");
        }

        return FixedAndroidPath;
    }

    /// <summary>
    /// <c>getApplicationContext().getExternalFilesDir(null).getAbsolutePath()</c>, which also creates
    /// the folder when it is absent. Null when the bridge is not there or gives nothing.
    /// </summary>
    private static string? AskAndroid()
    {
        if (!Engine.HasSingleton("AndroidRuntime"))
        {
            return null;
        }

        GodotObject runtime = Engine.GetSingleton("AndroidRuntime");
        if (runtime.Call("getApplicationContext").AsGodotObject() is not GodotObject context)
        {
            return null;
        }

        if (context.Call("getExternalFilesDir", new Variant()).AsGodotObject() is not GodotObject folder)
        {
            return null;
        }

        string path = folder.Call("getAbsolutePath").AsString();
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private static void Log(string line) => GD.Print($"{LogTag} {line}");

    private static void Warn(string line) => GD.PushWarning($"{LogTag} {line}");
}
