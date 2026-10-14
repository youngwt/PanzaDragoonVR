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
    private readonly List<AssetLookup> _lookups = new();
    private readonly List<string> _loadProblems = new();

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
        _lookups.Add(lookup);
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
        // The board names the path as the manifest lists it, as for every other problem; the log also gets the full path.
        string listed = System.IO.Path.GetRelativePath(Path!, full);
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LoadProblem(id, $"{id}: {listed}: cannot be read: {e.Message}", full);
            _placeholders.Add(id);
            return null;
        }

        // Decoded as a PNG whatever the file's name, since the image kind is PNG.
        var image = new Godot.Image();
        Error error = image.LoadPngFromBuffer(bytes);
        if (error != Error.Ok || image.IsEmpty())
        {
            LoadProblem(id, $"{id}: {listed}: unreadable image: the PNG could not be decoded ({error})", full);
            _placeholders.Add(id);
            return null;
        }

        // Smaller copies for the distance, as for the placeholders, so far tiles do not shimmer.
        image.GenerateMipmaps();
        Log($"{id}: using {full} ({image.GetWidth()} x {image.GetHeight()})");
        _supplied.Add(id);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// The model the folder supplies for <paramref name="id"/>, built and ready to add to the scene, or
    /// null for the placeholder. The file is checked by the asset contract's "When a model is invalid"
    /// table before the engine sees it, so nothing it names is opened. A model is used whole or not at
    /// all: any image the engine cannot decode, or no mesh in what it builds, rejects all of it. Vertex
    /// colours are switched off, as the contract says the game ignores <c>COLOR_0</c> in format 1.
    /// </summary>
    public Node3D? Model(string id)
    {
        // A failure nothing here foresaw keeps the placeholder for this ID only; the images are unaffected.
        try
        {
            return LoadModel(id);
        }
        catch (Exception e)
        {
            string problem = $"{id}: unreadable model: it could not be loaded ({e.GetType().Name}: {e.Message})";
            Warn($"{problem}; using the placeholder for {id}");
            _loadProblems.Add(problem);
            _supplied.Remove(id);
            if (!_placeholders.Contains(id))
            {
                _placeholders.Add(id);
            }

            return null;
        }
    }

    private Node3D? LoadModel(string id)
    {
        AssetLookup lookup = _read.Find(id, AssetManifest.ModelKind);
        _lookups.Add(lookup);
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
        string listed = System.IO.Path.GetRelativePath(Path!, full);
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            LoadProblem(id, $"{id}: {listed}: cannot be read: {e.Message}", full);
            _placeholders.Add(id);
            return null;
        }

        // Checked again on the very bytes the engine gets, in case the file changed after the lookup.
        GlbCheckResult check = GlbCheck.Check(bytes);
        if (!check.IsUsable)
        {
            LoadProblem(id, check.Line(id, listed)!, full);
            _placeholders.Add(id);
            return null;
        }

        Node3D? model = Build(id, listed, full, bytes, check.ImageCount);
        if (model == null)
        {
            _placeholders.Add(id);
            return null;
        }

        Log($"{id}: using {full}");
        _supplied.Add(id);
        return model;
    }

    /// <summary>Builds a checked model with the engine; null, with the problem recorded, when it cannot be used whole.</summary>
    private Node3D? Build(string id, string listed, string full, byte[] bytes, int imageCount)
    {
        // Images are kept in memory as they are: there is no folder to extract them to.
        var state = new GltfState { HandleBinaryImageMode = GltfState.HandleBinaryImageModeEnum.EmbedAsUncompressed };
        var document = new GltfDocument();
        // No base path: the check above found nothing the file names, so there is nothing to look for.
        Error error = document.AppendFromBuffer(bytes, "", state);
        if (error != Error.Ok)
        {
            LoadProblem(id, $"{id}: {listed}: unreadable model: it could not be loaded ({error})", full);
            return null;
        }

        // An image the engine cannot decode only warns and leaves an empty texture, so look for one.
        Godot.Collections.Array<Texture2D> images = state.GetImages();
        for (int i = 0; i < imageCount; i++)
        {
            if (i >= images.Count || images[i] == null)
            {
                LoadProblem(id, $"{id}: {listed}: unreadable texture in model: image {i} could not be decoded", full);
                return null;
            }
        }

        if (document.GenerateScene(state) is not Node3D model)
        {
            LoadProblem(id, $"{id}: {listed}: unreadable model: no scene could be built from it", full);
            return null;
        }

        try
        {
            var meshes = new List<MeshInstance3D>();
            FindMeshes(model, meshes);
            if (meshes.Count == 0)
            {
                model.Free();
                LoadProblem(id, $"{id}: {listed}: empty model: it has no mesh", full);
                return null;
            }

            foreach (MeshInstance3D instance in meshes)
            {
                IgnoreVertexColours(instance);
            }

            return model;
        }
        catch (Exception)
        {
            // Never half a model: what was built is dropped, and Model reports the failure.
            model.Free();
            throw;
        }
    }

    private static void FindMeshes(Node node, List<MeshInstance3D> found)
    {
        if (node is MeshInstance3D { Mesh: not null } instance && instance.Mesh.GetSurfaceCount() > 0)
        {
            found.Add(instance);
        }

        foreach (Node child in node.GetChildren())
        {
            FindMeshes(child, found);
        }
    }

    /// <summary>
    /// Switches off vertex colour as albedo on every material a mesh is drawn with. The engine turns it
    /// on when a file carries <c>COLOR_0</c>; the contract says the game ignores it in format 1.
    /// </summary>
    private static void IgnoreVertexColours(MeshInstance3D instance)
    {
        Mesh mesh = instance.Mesh;
        for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
        {
            if (mesh.SurfaceGetMaterial(surface) is BaseMaterial3D own)
            {
                own.VertexColorUseAsAlbedo = false;
            }

            if (instance.GetSurfaceOverrideMaterial(surface) is BaseMaterial3D overridden)
            {
                overridden.VertexColorUseAsAlbedo = false;
            }
        }

        if (instance.MaterialOverride is BaseMaterial3D whole)
        {
            whole.VertexColorUseAsAlbedo = false;
        }
    }

    /// <summary>Logs which IDs the folder supplied and which run on their placeholders.</summary>
    public void LogSummary()
    {
        if (_supplied.Count == 0)
        {
            Log($"no asset was supplied: running on placeholders ({string.Join(", ", _placeholders)})");
            return;
        }

        string placeholders = _placeholders.Count == 0 ? "none" : string.Join(", ", _placeholders);
        Log($"supplied: {string.Join(", ", _supplied)}; placeholders: {placeholders}");
    }

    /// <summary>
    /// What the launch check found, for the report in the headset: every lookup made so far and every
    /// image or model that passed the checks but could not be loaded. Every line of it is logged too, in full.
    /// </summary>
    public LaunchReport Report()
    {
        LaunchReport report = LaunchReport.Build(Path, _read, _lookups, _loadProblems);
        LogReport(report);
        return report;
    }

    /// <summary>Logs every line of a launch report, none left out, so what the headset shows is also in the log.</summary>
    public static void LogReport(LaunchReport report)
    {
        foreach (string line in report.AllLines)
        {
            Log($"report: {line}");
        }
    }

    /// <summary>
    /// The report when loading failed part way: the folder that was found, the problems found so far,
    /// then <paramref name="reason"/>. Every line is logged.
    /// </summary>
    public LaunchReport FailedReport(string reason)
    {
        LaunchReport found = LaunchReport.Build(Path, _read, _lookups, _loadProblems);
        LaunchReport report = LaunchReport.Failed(Path, reason, found.Problems);
        LogReport(report);
        return report;
    }

    private void LoadProblem(string id, string problem, string fullPath)
    {
        Warn($"{problem} (file {fullPath}); using the placeholder for {id}");
        _loadProblems.Add(problem);
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
