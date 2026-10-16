#nullable enable
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text.Json;

namespace PanzaDragoonVR.Logic;

/// <summary>Which row of the asset contract's "When a model is invalid" table a model falls under.</summary>
public enum GlbProblemKind
{
    /// <summary>Nothing found that the contract rejects: the file may be handed to the engine.</summary>
    None,

    /// <summary>Not a readable <c>.glb</c> of glTF version 2.0.</summary>
    Unreadable,

    /// <summary>It refers to something outside itself: an external buffer, an external image or a <c>data:</c> URI.</summary>
    NotSelfContained,

    /// <summary>It lists an extension in <c>extensionsRequired</c>.</summary>
    UnsupportedModel,

    /// <summary>It contains no mesh.</summary>
    Empty,
}

/// <summary>What <see cref="GlbCheck.Check"/> found in a <c>.glb</c>.</summary>
/// <param name="Kind">Which problem, or <see cref="GlbProblemKind.None"/>.</param>
/// <param name="Detail">What exactly is wrong, for the report; null when there is no problem.</param>
/// <param name="ImageCount">How many images the file lists; 0 when it could not be read.</param>
public readonly record struct GlbCheckResult(GlbProblemKind Kind, string? Detail, int ImageCount)
{
    public bool IsUsable => Kind == GlbProblemKind.None;

    /// <summary>
    /// The launch report's line for this problem, worded as the asset contract's table words it and
    /// starting with the ID and the path as the manifest lists it; null when there is no problem.
    /// </summary>
    public string? Line(string id, string path) => Kind switch
    {
        GlbProblemKind.None => null,
        GlbProblemKind.Unreadable => $"{id}: {path}: unreadable model: {Detail}",
        GlbProblemKind.NotSelfContained => $"{id}: {path}: not self-contained: {Detail}",
        GlbProblemKind.UnsupportedModel => $"{id}: {path}: unsupported model: {Detail}",
        GlbProblemKind.Empty => $"{id}: {path}: empty model: {Detail}",
        _ => $"{id}: {path}: {Detail}",
    };
}

/// <summary>
/// Reads a binary glTF file's header and JSON chunk and finds the problems the asset contract's
/// "When a model is invalid" table rejects (<c>spec-panzer-dragoon-zwei-vr/asset-contract.md</c>),
/// before the engine sees the file. So a model that names another file is rejected without that file
/// ever being opened. Nothing else is policed: the contract's conventions are the tool's obligations.
/// Images are not decoded here; whether the engine can decode them is checked when it loads the file.
/// Nothing here uses the engine, so it is tested on the PC.
/// </summary>
public static class GlbCheck
{
    private const uint Magic = 0x46546C67;     // "glTF", little-endian
    private const uint JsonChunk = 0x4E4F534A; // "JSON"
    private const uint BinChunk = 0x004E4942;  // "BIN\0"
    private const int HeaderBytes = 12;
    private const int ChunkHeaderBytes = 8;

    /// <summary>Checks a whole <c>.glb</c> file's bytes.</summary>
    public static GlbCheckResult Check(ReadOnlySpan<byte> file)
    {
        if (file.Length < HeaderBytes || BinaryPrimitives.ReadUInt32LittleEndian(file) != Magic)
        {
            return Unreadable("not a .glb file: no glTF header");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(file[4..]);
        if (version != 2)
        {
            return Unreadable($"a .glb of glTF version {version}; it must be version 2");
        }

        uint declared = BinaryPrimitives.ReadUInt32LittleEndian(file[8..]);
        if (declared != (uint)file.Length)
        {
            return Unreadable($"the header gives a length of {declared} bytes but the file is {file.Length} bytes");
        }

        if (file.Length < HeaderBytes + ChunkHeaderBytes)
        {
            return Unreadable("the file has no JSON chunk");
        }

        uint jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(file[HeaderBytes..]);
        uint jsonType = BinaryPrimitives.ReadUInt32LittleEndian(file[(HeaderBytes + 4)..]);
        if (jsonType != JsonChunk)
        {
            return Unreadable("the first chunk is not JSON");
        }

        long jsonEnd = (long)HeaderBytes + ChunkHeaderBytes + jsonLength;
        if (jsonEnd > file.Length)
        {
            return Unreadable("the JSON chunk runs past the end of the file");
        }

        long? binLength = null;
        if (file.Length - jsonEnd >= ChunkHeaderBytes)
        {
            ReadOnlySpan<byte> next = file[(int)jsonEnd..];
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(next);
            uint type = BinaryPrimitives.ReadUInt32LittleEndian(next[4..]);
            if (jsonEnd + ChunkHeaderBytes + length > file.Length)
            {
                return Unreadable("the chunk after the JSON runs past the end of the file");
            }

            if (type == BinChunk)
            {
                binLength = length;
            }
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(file.Slice(HeaderBytes + ChunkHeaderBytes, (int)jsonLength).ToArray());
        }
        catch (JsonException)
        {
            return Unreadable("the JSON chunk is not valid JSON");
        }

        using (document)
        {
            try
            {
                return CheckJson(document.RootElement, binLength);
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException or JsonException)
            {
                // Such as a string holding a lone surrogate escape, which parses but cannot be read.
                return Unreadable("the JSON chunk could not be read");
            }
        }
    }

    private static GlbCheckResult CheckJson(JsonElement root, long? binLength)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return Unreadable("the JSON chunk is not one JSON object");
        }

        if (!root.TryGetProperty("asset", out JsonElement asset) || asset.ValueKind != JsonValueKind.Object ||
            !asset.TryGetProperty("version", out JsonElement versionElement) || versionElement.ValueKind != JsonValueKind.String)
        {
            return Unreadable("it has no 'asset' object with a 'version'");
        }

        string version = versionElement.GetString()!;
        if (version.Split('.')[0] != "2")
        {
            return Unreadable($"glTF version '{version}'; it must be 2.0");
        }

        // A reference outside the file is found before anything else, so nothing the file names is opened.
        JsonElement[] buffers = ArrayOf(root, "buffers");
        JsonElement[] images = ArrayOf(root, "images");
        for (int i = 0; i < buffers.Length; i++)
        {
            if (Outside(buffers[i], $"buffer {i}") is string outside)
            {
                return new GlbCheckResult(GlbProblemKind.NotSelfContained, outside, 0);
            }
        }

        for (int i = 0; i < images.Length; i++)
        {
            if (Outside(images[i], $"image {i}") is string outside)
            {
                return new GlbCheckResult(GlbProblemKind.NotSelfContained, outside, 0);
            }
        }

        // With no uri, a buffer can only be the file's own binary chunk, and only the first buffer can be.
        for (int i = 0; i < buffers.Length; i++)
        {
            if (i > 0)
            {
                return Unreadable($"buffer {i} has no uri, and only buffer 0 can be the file's binary chunk");
            }

            if (binLength == null)
            {
                return Unreadable("buffer 0 is the file's binary chunk, but the file has none");
            }

            if (!buffers[i].TryGetProperty("byteLength", out JsonElement byteLength) || !byteLength.TryGetInt64(out long length) || length < 0)
            {
                return Unreadable("buffer 0 has no valid 'byteLength'");
            }

            if (length > binLength)
            {
                return Unreadable($"buffer 0 is {length} bytes but the binary chunk holds {binLength}");
            }
        }

        if (root.TryGetProperty("extensionsRequired", out JsonElement required) && required.ValueKind == JsonValueKind.Array &&
            required.GetArrayLength() > 0)
        {
            string names = string.Join(", ", required.EnumerateArray().Select(name =>
                name.ValueKind == JsonValueKind.String ? name.GetString() : name.GetRawText()));
            string noun = required.GetArrayLength() == 1 ? "extension" : "extensions";
            return new GlbCheckResult(GlbProblemKind.UnsupportedModel, $"it requires the {noun} {names}", 0);
        }

        if (ArrayOf(root, "meshes").Length == 0)
        {
            return new GlbCheckResult(GlbProblemKind.Empty, "it has no mesh", 0);
        }

        bool meshInScene = ArrayOf(root, "nodes").Any(node => node.ValueKind == JsonValueKind.Object && node.TryGetProperty("mesh", out _));
        if (!meshInScene)
        {
            return new GlbCheckResult(GlbProblemKind.Empty, "no node uses any of its meshes", 0);
        }

        return new GlbCheckResult(GlbProblemKind.None, null, images.Length);
    }

    /// <summary>What a buffer or image refers to outside the file, or null when it has no <c>uri</c>.</summary>
    private static string? Outside(JsonElement item, string what)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("uri", out JsonElement uri))
        {
            return null;
        }

        if (uri.ValueKind != JsonValueKind.String)
        {
            return $"{what} has a uri; nothing it names was opened";
        }

        string text = uri.GetString()!;
        if (text.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return $"{what} has a data: URI";
        }

        // A long or many-lined name is shortened, as it is shown on the board.
        string shown = text.Length > 60 ? text[..57] + "..." : text;
        shown = string.Concat(shown.Select(c => char.IsControl(c) ? ' ' : c));
        return $"{what} names the file '{shown}', which was not opened";
    }

    private static JsonElement[] ArrayOf(JsonElement root, string field) =>
        root.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();

    private static GlbCheckResult Unreadable(string detail) => new(GlbProblemKind.Unreadable, detail, 0);
}
