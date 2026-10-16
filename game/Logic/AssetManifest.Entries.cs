#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PanzaDragoonVR.Logic;

// Reading the manifest's entries: each one's fields, duplicate IDs, and why an entry cannot be used.

public sealed partial class AssetManifest
{
    /// <summary>One usable entry. <paramref name="Bytes"/> is null for a size too large for 64 bits, which no file has.</summary>
    private sealed record Entry(string Id, string Kind, string Path, long? Bytes, string BytesText);

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
}
