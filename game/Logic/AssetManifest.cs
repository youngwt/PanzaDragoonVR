#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PanzaDragoonVR.Logic;

/// <summary>What came of reading an asset folder's <c>manifest.json</c>.</summary>
public enum ManifestStatus
{
    /// <summary>The folder has no <c>manifest.json</c>: the game runs on placeholders. Not an error.</summary>
    Absent,

    /// <summary>The manifest is not valid JSON, or a required top-level field is absent or of the wrong type.</summary>
    Unreadable,

    /// <summary>The manifest's <c>format</c> is not one this build supports.</summary>
    UnsupportedFormat,

    /// <summary>The manifest was read; its entries can be looked up.</summary>
    Read,
}

/// <summary>What came of looking up one asset ID.</summary>
public enum AssetLookupStatus
{
    /// <summary>The entry and its file passed every check; <see cref="AssetLookup.FullPath"/> is the file.</summary>
    Found,

    /// <summary>The manifest does not list the ID, or there is no manifest. The normal state; nothing to report.</summary>
    NotListed,

    /// <summary>The ID is listed but cannot be used; <see cref="AssetLookup.Problem"/> says why.</summary>
    Problem,
}

/// <summary>The answer for one asset ID: the file to load, or why the placeholder is used.</summary>
public readonly record struct AssetLookup(AssetLookupStatus Status, string? FullPath, string? Problem)
{
    public static AssetLookup NotListed() => new(AssetLookupStatus.NotListed, null, null);

    public static AssetLookup Fail(string problem) => new(AssetLookupStatus.Problem, null, problem);
}

/// <summary>
/// An asset folder's manifest, read and checked to the asset contract
/// (<c>spec-panzer-dragoon-zwei-vr/asset-contract.md</c>). The same rules are in
/// <c>tools/manifest.py</c> for the PC side; the two must agree.
/// Nothing here uses the engine, so it is tested on the PC.
/// </summary>
public sealed class AssetManifest
{
    public const string FileName = "manifest.json";

    /// <summary>The <c>format</c> numbers this build understands.</summary>
    public const int SupportedFormat = 1;

    public const string ImageKind = "image";
    public const string ModelKind = "model";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Regex IdRule = new(@"\A[a-z0-9_]+(\.[a-z0-9_]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex PathChars = new(@"\A[a-z0-9_.\-/]*\z", RegexOptions.CultureInvariant);
    private static readonly byte[] PngSignature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

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

    /// <summary>One usable entry. <paramref name="Bytes"/> is null for a size too large for 64 bits, which no file has.</summary>
    private sealed record Entry(string Id, string Kind, string Path, long? Bytes, string BytesText);

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

            return new ManifestRead(ManifestStatus.Read, ReadEntries(root.GetProperty("assets"), folder), null);
        }

        static ManifestRead Unreadable(string reason) => new(ManifestStatus.Unreadable, null, reason);
    }

    /// <summary>
    /// Looks up <paramref name="id"/> for the game, which expects an asset of <paramref name="expectedKind"/>,
    /// and checks its file: present, a file, inside the folder, of the stated size and, for an image,
    /// a PNG of 8 bits per channel, RGB or RGBA; for a model, a self-contained <c>.glb</c> of glTF 2.0
    /// with a mesh and no required extensions (<see cref="GlbCheck"/>).
    /// </summary>
    public AssetLookup Find(string id, string expectedKind)
    {
        if (_rejected.TryGetValue(id, out string? reason))
        {
            return AssetLookup.Fail(reason);
        }

        if (!_entries.TryGetValue(id, out Entry? entry))
        {
            return AssetLookup.NotListed();
        }

        string where = $"{id}: {entry.Path}";
        if (entry.Kind != expectedKind)
        {
            return AssetLookup.Fail($"{id}: wrong kind: the manifest says '{entry.Kind}', the game needs '{expectedKind}'");
        }

        string full = System.IO.Path.Combine(Folder, entry.Path);
        try
        {
            if (!LeadsInside(Folder, entry.Path))
            {
                return AssetLookup.Fail($"{id}: invalid entry: its path leaves the asset folder");
            }

            if (Directory.Exists(full))
            {
                return AssetLookup.Fail($"{where}: missing: the path is not a file");
            }

            var file = new FileInfo(full);
            if (!file.Exists)
            {
                return AssetLookup.Fail($"{where}: missing");
            }

            if (file.Length != entry.Bytes)
            {
                return AssetLookup.Fail($"{where}: incomplete or changed: the file is {file.Length} bytes, the manifest says {entry.BytesText}");
            }

            if (expectedKind == ImageKind)
            {
                byte[] head = new byte[26];
                int read;
                using (FileStream stream = file.OpenRead())
                {
                    read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
                }

                string? notPng = PngProblem(head.AsSpan(0, read));
                if (notPng != null)
                {
                    return AssetLookup.Fail($"{where}: unreadable image: {notPng}");
                }
            }
            else if (expectedKind == ModelKind)
            {
                // Its header and JSON chunk, by the contract's "When a model is invalid" table.
                // Nothing the model names is opened.
                GlbCheckResult model = GlbCheck.Check(File.ReadAllBytes(full));
                if (!model.IsUsable)
                {
                    return AssetLookup.Fail(model.Line(id, entry.Path)!);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return AssetLookup.Fail($"{where}: cannot be read: {e.Message}");
        }

        return new AssetLookup(AssetLookupStatus.Found, full, null);
    }

    /// <summary>Why <paramref name="id"/> is not a valid asset ID, or null.</summary>
    public static string? IdProblem(string id) =>
        IdRule.IsMatch(id) ? null : "an ID is lowercase letters, digits and underscores in levels separated by single dots";

    /// <summary>Why <paramref name="path"/> breaks the contract's path rules, or null. The same rules as <c>tools/manifest.py</c>.</summary>
    public static string? PathProblem(string path)
    {
        if (path.Length == 0)
        {
            return "a path must not be empty";
        }

        if (path.Contains('\\'))
        {
            return "a path must use forward slashes, not backslashes";
        }

        if (path.StartsWith('/'))
        {
            return "a path must be relative to the asset folder, with no leading slash";
        }

        string[] parts = path.Split('/');
        if (Array.IndexOf(parts, ".") >= 0 || Array.IndexOf(parts, "..") >= 0)
        {
            return "a path must not contain '.' or '..' parts";
        }

        if (Array.IndexOf(parts, "") >= 0)
        {
            return "a path must not contain empty parts";
        }

        if (!PathChars.IsMatch(path))
        {
            return "a path may hold only lowercase letters, digits, underscores, hyphens and dots";
        }

        return null;
    }

    /// <summary>Why the first bytes of a file are not an 8-bit RGB or RGBA PNG, or null.</summary>
    public static string? PngProblem(ReadOnlySpan<byte> head)
    {
        if (head.Length < 26 || !head[..8].SequenceEqual(PngSignature) || !head[12..16].SequenceEqual("IHDR"u8))
        {
            return "not a PNG file";
        }

        byte depth = head[24];
        byte colour = head[25];
        if (depth != 8 || (colour != 2 && colour != 6))
        {
            string name = colour switch
            {
                0 => "greyscale",
                2 => "RGB",
                3 => "palette",
                4 => "greyscale with alpha",
                6 => "RGBA",
                _ => $"colour type {colour}",
            };
            return $"a {depth}-bit {name} PNG; it must be 8-bit RGB or RGBA";
        }

        return null;
    }

    private static AssetManifest ReadEntries(JsonElement assets, string folder)
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        var rejected = new Dictionary<string, string>(StringComparer.Ordinal);
        var firstPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var problems = new List<string>();

        int index = 0;
        foreach (JsonElement item in assets.EnumerateArray())
        {
            index++;
            string label = $"entry {index}";
            if (item.ValueKind != JsonValueKind.Object)
            {
                problems.Add($"{label}: must be an object");
                continue;
            }

            string? id = item.TryGetProperty("id", out JsonElement idElement) ? Text(idElement) : null;
            string? path = item.TryGetProperty("path", out JsonElement pathElement) ? Text(pathElement) : null;

            if (id != null)
            {
                // Named by its path, or by its place before it is relabelled with its ID.
                string shown = path ?? label;
                label = id;
                if (firstPath.TryGetValue(id, out string? earlier))
                {
                    // Neither entry is used: the game cannot know which one was meant.
                    string duplicate = $"{id}: duplicate ID, listed for both {earlier} and {shown}";
                    problems.Add(duplicate);
                    entries.Remove(id);
                    rejected[id] = duplicate;
                    continue;
                }

                firstPath[id] = shown;
            }

            string? invalid = EntryProblem(item, id, path);
            if (invalid != null)
            {
                string line = $"{label}: invalid entry: {invalid}";
                problems.Add(line);
                if (id != null)
                {
                    rejected[id] = line;
                }

                continue;
            }

            JsonElement bytes = item.GetProperty("bytes");
            entries[id!] = new Entry(id!, KindText(item.GetProperty("kind")), path!,
                bytes.TryGetInt64(out long size) ? size : null, bytes.GetRawText());
        }

        return new AssetManifest(folder, entries, rejected, problems);
    }

    /// <summary>Why an entry cannot be used whatever the game wants it for, or null.</summary>
    private static string? EntryProblem(JsonElement item, string? id, string? path)
    {
        var reasons = new List<string>();
        RequireField(item, "id", e => e.ValueKind == JsonValueKind.String, "a string", reasons);
        RequireField(item, "kind", e => e.ValueKind == JsonValueKind.String, "a string", reasons);
        RequireField(item, "path", e => e.ValueKind == JsonValueKind.String, "a string", reasons);
        RequireField(item, "bytes", e => IsInteger(e), "an integer", reasons);
        // An unreadable kind is not checked here: like any kind the game does not know, it is only a
        // problem when that ID is asked for, as the contract and tools/manifest.py have it.
        foreach (string field in new[] { "id", "path" })
        {
            if (item.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.String && Text(value) == null)
            {
                reasons.Add($"field '{field}' is not readable text: it holds a lone surrogate escape");
            }
        }

        if (reasons.Count > 0)
        {
            return string.Join("; ", reasons);
        }

        if (IdProblem(id!) is string badId)
        {
            return $"not a valid ID: {badId}";
        }

        JsonElement bytes = item.GetProperty("bytes");
        if (bytes.TryGetInt64(out long size) ? size < 0 : bytes.GetRawText().StartsWith('-'))
        {
            return "'bytes' is negative";
        }

        if (PathProblem(path!) is string badPath)
        {
            return $"invalid path: {badPath}";
        }

        return null;
    }

    /// <summary>
    /// A JSON string's text, or null when it is not a string or cannot be read: a lone surrogate
    /// escape such as <c>"\ud800"</c> parses as JSON but throws when read.
    /// </summary>
    private static string? Text(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return value.GetString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A kind's text, or its JSON as written when it cannot be read, so it reads as a kind the game does not know.</summary>
    private static string KindText(JsonElement kind) => Text(kind) ?? kind.GetRawText();

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

    private static void RequireField(JsonElement item, string field, Func<JsonElement, bool> good, string wanted, List<string> reasons)
    {
        if (!item.TryGetProperty(field, out JsonElement value))
        {
            reasons.Add($"required field '{field}' is absent");
        }
        else if (!good(value))
        {
            reasons.Add($"field '{field}' must be {wanted}");
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

    /// <summary>
    /// Whether <paramref name="path"/>, which already keeps the path rules, still ends inside
    /// <paramref name="folder"/> once every symbolic link along it is followed, as the
    /// <c>os.path.realpath</c> comparison in <c>tools/manifest.py</c> does.
    /// </summary>
    private static bool LeadsInside(string folder, string path)
    {
        string? root = RealPath(System.IO.Path.GetFullPath(folder));
        string? real = root == null ? null : RealPath(root + "/" + path);
        if (root == null || real == null)
        {
            return false;
        }

        string withSlash = root.EndsWith('/') ? root : root + "/";
        return real.StartsWith(withSlash, StringComparison.Ordinal);
    }

    /// <summary>
    /// The absolute path <paramref name="path"/> leads to, with every symbolic link in every part
    /// followed, and each <c>..</c> taken after the link before it is followed, as the file system does.
    /// Parts that do not exist are kept as written. Null after too many links, as for a loop.
    /// Paths are POSIX paths, as on Android and the PC.
    /// </summary>
    private static string? RealPath(string path)
    {
        const int maxLinks = 40;
        var resolved = new List<string>();
        var pending = new LinkedList<string>(path.Split('/'));
        int links = 0;
        while (pending.First != null)
        {
            string part = pending.First.Value;
            pending.RemoveFirst();
            if (part is "" or ".")
            {
                continue;
            }

            if (part == "..")
            {
                if (resolved.Count > 0)
                {
                    resolved.RemoveAt(resolved.Count - 1);
                }

                continue;
            }

            string here = "/" + string.Join('/', resolved.Append(part));
            string? target = new FileInfo(here).LinkTarget;
            if (target == null)
            {
                resolved.Add(part);
                continue;
            }

            if (++links > maxLinks)
            {
                return null;
            }

            // The link's target replaces it; a relative one is taken from the link's own folder.
            if (target.StartsWith('/'))
            {
                resolved.Clear();
            }

            string[] targetParts = target.Split('/');
            for (int i = targetParts.Length - 1; i >= 0; i--)
            {
                pending.AddFirst(targetParts[i]);
            }
        }

        return "/" + string.Join('/', resolved);
    }
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
