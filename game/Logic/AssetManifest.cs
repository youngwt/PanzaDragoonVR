#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PanzaDragoonVR.Logic;

// The manifest as a whole: loading and parsing manifest.json, its top-level fields and the JSON
// helpers the other parts share. The class is split by job: entry reading is in
// AssetManifest.Entries.cs, the ID, path and PNG rules in AssetManifest.Rules.cs, and Find with
// its file and folder checks in AssetManifest.Files.cs.

/// <summary>What came of reading an asset folder's <c>manifest.json</c>.</summary>
public enum ManifestStatus
{
    /// <summary>The folder has no <c>manifest.json</c>: the game runs on placeholders. Not an error.</summary>
    Absent,

    /// <summary>The manifest is not valid JSON, a required top-level field is absent or of the wrong type, or it lists no assets.</summary>
    Unreadable,

    /// <summary>The manifest's <c>format</c> is not one this build supports.</summary>
    UnsupportedFormat,

    /// <summary>The manifest was read; its entries can be looked up.</summary>
    Read,
}

/// <summary>
/// An asset folder's manifest, read and checked to the asset contract
/// (<c>spec-panzer-dragoon-zwei-vr/asset-contract.md</c>). The same rules are in
/// <c>tools/manifest.py</c> for the PC side; the two must agree.
/// Nothing here uses the engine, so it is tested on the PC.
/// </summary>
public sealed partial class AssetManifest
{
    public const string FileName = "manifest.json";

    /// <summary>The <c>format</c> numbers this build understands.</summary>
    public const int SupportedFormat = 1;

    /// <summary>The problem line for a manifest whose <c>assets</c> list is empty, as <c>tools/manifest.py</c> gives it.</summary>
    public const string NoAssets = FileName + ": lists no assets";

    public const string ImageKind = "image";
    public const string ModelKind = "model";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly Dictionary<string, Entry> _entries;
    private readonly Dictionary<string, string> _rejected;

    private AssetManifest(string folder, Dictionary<string, Entry> entries, Dictionary<string, string> rejected, List<string> problems)
    {
        Folder = folder;
        _entries = entries;
        _rejected = rejected;
        Problems = problems;
    }

    /// <summary>The asset folder the manifest's paths are relative to.</summary>
    public string Folder { get; }

    /// <summary>
    /// Every problem found in the entries when the manifest was read, one line each: duplicate IDs
    /// and invalid entries, whether or not the game uses those IDs. File checks happen in <see cref="Find"/>.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>The IDs of the entries that were read without a problem, in manifest order.</summary>
    public IEnumerable<string> Ids => _entries.Keys;

    /// <summary>Reads <c>manifest.json</c> from <paramref name="folder"/>.</summary>
    public static ManifestRead Load(string folder)
    {
        string manifestPath = System.IO.Path.Combine(folder, FileName);
        if (!File.Exists(manifestPath))
        {
            return new ManifestRead(ManifestStatus.Absent, null, $"no {FileName} in {folder}");
        }

        byte[] raw;
        try
        {
            raw = File.ReadAllBytes(manifestPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new ManifestRead(ManifestStatus.Unreadable, null, $"{FileName}: cannot be read: {e.Message}");
        }

        return Parse(raw, folder);
    }

    /// <summary>Reads a manifest from its bytes. <paramref name="folder"/> is what its paths are relative to.</summary>
    public static ManifestRead Parse(byte[] raw, string folder)
    {
        if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
        {
            return Unreadable($"{FileName}: starts with a byte-order mark; it must be UTF-8 without one");
        }

        try
        {
            // Strings the JSON reader accepts can still hold bytes that are not UTF-8, and reading
            // such a string later throws, so the whole file is checked first, as tools/manifest.py does.
            StrictUtf8.GetCharCount(raw);
        }
        catch (DecoderFallbackException)
        {
            return Unreadable($"{FileName}: is not UTF-8");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException e)
        {
            return Unreadable($"{FileName}: is not valid JSON: {OneLine(e.Message)}");
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Unreadable($"{FileName}: must be one JSON object");
            }

            var problems = new List<string>();
            CheckTopField(root, "format", e => IsInteger(e), "an integer", problems);
            CheckTopField(root, "generator", e => e.ValueKind == JsonValueKind.String, "a string", problems);
            CheckTopField(root, "source", e => e.ValueKind is JsonValueKind.Object or JsonValueKind.Null, "an object or null", problems);
            CheckTopField(root, "assets", e => e.ValueKind == JsonValueKind.Array, "an array", problems);
            if (problems.Count > 0)
            {
                return Unreadable(string.Join("; ", problems));
            }

            if (!root.GetProperty("format").TryGetInt64(out long format) || format != SupportedFormat)
            {
                string shown = root.GetProperty("format").GetRawText();
                return new ManifestRead(ManifestStatus.UnsupportedFormat, null,
                    $"{FileName}: unsupported format {shown}; this build understands format {SupportedFormat}. " +
                    "The assets were made by a newer or older tool than this build; run the matching tool again");
            }

            // After the format check, so a newer format with no assets is reported as the newer format.
            if (root.GetProperty("assets").GetArrayLength() == 0)
            {
                return Unreadable(NoAssets);
            }

            return new ManifestRead(ManifestStatus.Read, ReadEntries(root.GetProperty("assets"), folder), null);
        }

        static ManifestRead Unreadable(string reason) => new(ManifestStatus.Unreadable, null, reason);
    }

    private static void CheckTopField(JsonElement root, string field, Func<JsonElement, bool> good, string wanted, List<string> problems)
    {
        if (!root.TryGetProperty(field, out JsonElement value))
        {
            problems.Add($"{FileName}: required field '{field}' is absent");
        }
        else if (!good(value))
        {
            problems.Add($"{FileName}: field '{field}' must be {wanted}");
        }
    }

    /// <summary>A message on one line with no closing full stop, as it may quote the file's own line breaks.</summary>
    private static string OneLine(string message) =>
        Regex.Replace(message, @"\s+", " ").Trim().TrimEnd('.');

    /// <summary>
    /// A JSON number written with no fraction or exponent, of any size, as Python's <c>json</c> reads an
    /// integer in <c>tools/manifest.py</c>. <c>1.0</c> and <c>1e3</c> are not integers.
    /// </summary>
    private static bool IsInteger(JsonElement element) =>
        element.ValueKind == JsonValueKind.Number && element.GetRawText().IndexOfAny(new[] { '.', 'e', 'E' }) < 0;
}

/// <summary>The result of reading a manifest: the manifest, or why there is none to use.</summary>
/// <param name="Status">Which row of the contract's launch table applies to the manifest as a whole.</param>
/// <param name="Manifest">The manifest when <paramref name="Status"/> is <see cref="ManifestStatus.Read"/>.</param>
/// <param name="Problem">Why there is no manifest to use, otherwise null.</param>
public readonly record struct ManifestRead(ManifestStatus Status, AssetManifest? Manifest, string? Problem)
{
    /// <summary>Looks up an ID; with no manifest every ID is unlisted.</summary>
    public AssetLookup Find(string id, string expectedKind) =>
        Manifest?.Find(id, expectedKind) ?? AssetLookup.NotListed();
}
