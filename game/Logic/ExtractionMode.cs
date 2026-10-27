#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PanzaDragoonVR.Logic;

/// <summary>The asset contract's extraction modes, from "Development and release modes".</summary>
public enum ExtractionMode
{
    /// <summary>The <c>development</c> marker is in the asset folder: extract on every launch and keep the disc image.</summary>
    Development,

    /// <summary>
    /// No marker, whatever the build: extract unless already extracted from the same disc by the same extractor,
    /// then delete the disc image, unless the extraction failed or wrote nothing.
    /// </summary>
    Release,
}

/// <summary>A disc's product and version, as its Saturn header and a manifest's <c>source</c> give them.</summary>
public readonly record struct DiscSource(string Product, string Version);

/// <summary>
/// Who wrote the asset folder's current <c>manifest.json</c> and from which disc: its <c>generator</c>,
/// and its <c>source</c> when that is an object with a string <c>product</c> and <c>version</c>.
/// </summary>
public readonly record struct ManifestOrigin(string Generator, DiscSource? Source);

/// <summary>What <see cref="ExtractionModes.Decide"/> chose.</summary>
/// <param name="Mode">The mode the folder is in.</param>
/// <param name="Extract">Whether to extract; false only when release mode skips it.</param>
/// <param name="DeleteImage">
/// Whether the disc image is deleted: after a successful extraction when <paramref name="Extract"/> is true,
/// at once when it is false. True in release mode, false in development mode.
/// </param>
public readonly record struct ExtractionDecision(ExtractionMode Mode, bool Extract, bool DeleteImage);

/// <summary>What deleting a disc image's files came to, as log lines after the tag.</summary>
/// <param name="Line">The files deleted, or null when none was.</param>
/// <param name="Warning">The one line for every file that could not be deleted, or null when all were.</param>
public readonly record struct DeletionOutcome(string? Line, string? Warning);

/// <summary>
/// The mode and skip decision of the asset contract's "Development and release modes", and its log
/// lines: development mode, with an empty <c>development</c> file at the top of the asset folder,
/// extracts every launch and keeps the disc image; release mode extracts unless the folder's
/// manifest was written by the current extractor (the whole <c>generator</c>, version and all) from a
/// disc of the same product and version, and deletes the disc image after a successful extraction
/// that wrote something, or after such a skip. Nothing here uses the engine or the decoding library,
/// so it is tested on the PC: the current extractor's generator is passed in by the game.
/// </summary>
public static class ExtractionModes
{
    /// <summary>The marker file's name at the top of the asset folder, fixed by the asset contract.</summary>
    public const string MarkerName = "development";

    /// <summary>The asset folder's mode: development when the marker file is there, release otherwise.</summary>
    public static ExtractionMode ModeOf(string assetFolder) =>
        File.Exists(Path.Combine(assetFolder, MarkerName)) ? ExtractionMode.Development : ExtractionMode.Release;

    /// <summary>
    /// Reads the origin of the folder's <c>manifest.json</c>; null when there is none, it cannot be read,
    /// or it has no string <c>generator</c>. Nothing else in it is checked: that is the launch check's job.
    /// </summary>
    public static ManifestOrigin? LoadOrigin(string assetFolder)
    {
        byte[] raw;
        try
        {
            raw = File.ReadAllBytes(Path.Combine(assetFolder, AssetManifest.FileName));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return ParseOrigin(raw);
    }

    /// <summary>The origin in a manifest's bytes; null when it is not a JSON object with a string <c>generator</c>.</summary>
    public static ManifestOrigin? ParseOrigin(byte[] raw)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(raw);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("generator", out JsonElement generator) || generator.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            DiscSource? source = null;
            if (root.TryGetProperty("source", out JsonElement s) && s.ValueKind == JsonValueKind.Object &&
                s.TryGetProperty("product", out JsonElement product) && product.ValueKind == JsonValueKind.String &&
                s.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.String)
            {
                source = new DiscSource(product.GetString()!, version.GetString()!);
            }

            return new ManifestOrigin(generator.GetString()!, source);
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether <paramref name="manifest"/> was written by the current extractor (its whole <c>generator</c>
    /// is <paramref name="generator"/>, the decoding library's) from a disc of <paramref name="disc"/>'s
    /// product and version. A manifest from an older or newer extractor is not.
    /// </summary>
    public static bool ExtractedFrom(ManifestOrigin? manifest, DiscSource? disc, string generator) =>
        manifest is ManifestOrigin m && disc is DiscSource d &&
        string.Equals(m.Generator, generator, StringComparison.Ordinal) &&
        m.Source is DiscSource s &&
        string.Equals(s.Product, d.Product, StringComparison.Ordinal) &&
        string.Equals(s.Version, d.Version, StringComparison.Ordinal);

    /// <summary>
    /// What to do with the one disc image in <c>disc/</c>: in development mode, extract and keep it; in
    /// release mode, skip extraction when the folder's manifest was extracted from the same disc by the
    /// extractor whose generator is <paramref name="generator"/>, else extract, and delete the image
    /// (see <see cref="DeleteAfterExtraction"/>). <paramref name="disc"/> is the image's header, null
    /// when it could not be read, which is never a skip: extraction then reports why.
    /// </summary>
    public static ExtractionDecision Decide(ExtractionMode mode, ManifestOrigin? manifest, DiscSource? disc, string generator) => mode switch
    {
        ExtractionMode.Development => new ExtractionDecision(mode, Extract: true, DeleteImage: false),
        _ => new ExtractionDecision(mode, Extract: !ExtractedFrom(manifest, disc, generator), DeleteImage: true),
    };

    /// <summary>
    /// Whether the disc image is deleted once extraction has ended: in release mode, after a successful
    /// extraction that wrote something (<paramref name="wroteAnything"/>: a texture or a plane); never
    /// after a failure, which keeps the image as it keeps the previous contents, nor after an extraction
    /// that wrote nothing (see <see cref="NothingWrittenLine"/>).
    /// </summary>
    public static bool DeleteAfterExtraction(ExtractionDecision decision, bool succeeded, bool wroteAnything) =>
        decision.DeleteImage && decision.Extract && succeeded && wroteAnything;

    /// <summary>
    /// The log line, after the tag, when release mode keeps the disc image because a successful extraction
    /// wrote nothing; null in every other case, when there is nothing to say about it.
    /// </summary>
    public static string? NothingWrittenLine(ExtractionDecision decision, bool succeeded, bool wroteAnything, string image) =>
        decision.DeleteImage && decision.Extract && succeeded && !wroteAnything
            ? $"{Name(decision.Mode)}: the extraction wrote nothing, so the disc image {image} is kept"
            : null;

    /// <summary>
    /// Deletes the disc image's own <paramref name="files"/>, the image itself first (the <c>.cue</c> or the
    /// <c>.iso</c>, as the library lists them). When the image itself cannot be deleted nothing else is,
    /// so the image stays whole; a later file that cannot be deleted is skipped and the rest still go.
    /// Every failure is gathered into one warning line. <paramref name="deleteFile"/> is
    /// <see cref="File.Delete"/> unless a test gives another.
    /// </summary>
    public static DeletionOutcome DeleteImage(string image, IReadOnlyList<string> files, Action<string>? deleteFile = null)
    {
        deleteFile ??= File.Delete;
        var deleted = new List<string>();
        var failed = new List<string>();
        foreach (string file in files)
        {
            try
            {
                deleteFile(file);
                deleted.Add(Path.GetFileName(file));
            }
            catch (Exception e)
            {
                failed.Add($"{Path.GetFileName(file)}: {e.GetType().Name}: {OneLine.Of(e.Message)}");
                if (deleted.Count == 0)
                {
                    // The image itself is still there, so its other files are kept with it.
                    break;
                }
            }
        }

        return new DeletionOutcome(
            deleted.Count > 0 ? DeletedLine(image, deleted) : null,
            failed.Count > 0 ? NotDeletedLine(image, string.Join("; ", failed)) : null);
    }

    /// <summary>The mode as the log names it: <c>development mode</c> or <c>release mode</c>.</summary>
    public static string Name(ExtractionMode mode) => mode == ExtractionMode.Development ? "development mode" : "release mode";

    /// <summary>
    /// The decision's log line, after the tag, with no full stop. <paramref name="image"/> is the disc
    /// image's path; <paramref name="disc"/> its header, used to name the disc a skip found already extracted.
    /// </summary>
    public static string DecisionLine(ExtractionDecision decision, string image, string assetFolder, DiscSource? disc)
    {
        string mode = Name(decision.Mode);
        if (decision.Mode == ExtractionMode.Development)
        {
            return $"{mode} ({MarkerName} is in {assetFolder}): extracting from {image} in the background into {assetFolder}; the disc image is kept";
        }

        if (decision.Extract)
        {
            return $"{mode}: extracting from {image} in the background into {assetFolder}; the disc image is deleted after a successful extraction that writes something";
        }

        string from = disc is DiscSource d ? $"{d.Product} {d.Version}" : "this disc";
        return $"{mode}: {assetFolder} is already extracted from {from}: not extracting again; deleting the disc image {image}";
    }

    /// <summary>The log line, after the tag, when <c>disc/</c> holds no disc image: nothing extracted, nothing deleted.</summary>
    public static string NoImageLine(ExtractionMode mode, string discFolder) =>
        $"{Name(mode)}: no disc image in {discFolder}: nothing to extract";

    /// <summary>The log line after the disc image's files were deleted, naming each.</summary>
    public static string DeletedLine(string image, IEnumerable<string> files) =>
        $"{Name(ExtractionMode.Release)}: deleted the disc image {image} ({string.Join(", ", files)})";

    /// <summary>A warning line when a disc image file could not be deleted; the game carries on.</summary>
    public static string NotDeletedLine(string image, string reason) =>
        $"{Name(ExtractionMode.Release)}: the disc image {image} could not be deleted ({reason}); carrying on";
}
