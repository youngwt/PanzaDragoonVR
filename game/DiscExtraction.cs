#nullable enable
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Godot;
using PdzDecoding;

namespace PanzaDragoonVR;

/// <summary>
/// Extraction at launch: when the asset folder's <c>disc/</c> holds a disc image, a <c>.cue</c> sheet or an <c>.iso</c>, the decoding
/// library writes the textures it extracts into the folder, and the <c>manifest.json</c> listing them, before the folder is read. Every outcome
/// is one <c>PDZ-ASSETS</c> line; nothing here stops the game, which carries on with what the folder holds.
/// </summary>
public static class DiscExtraction
{
    /// <summary>Runs extraction on <paramref name="assetFolder"/>; does nothing when there is no folder.</summary>
    public static void Run(string? assetFolder)
    {
        if (assetFolder == null)
        {
            return;
        }

        try
        {
            DiscSearch search = DiscFolder.Find(assetFolder);
            switch (search.State)
            {
                case DiscFolderState.None:
                    GD.Print($"{AssetFolder.LogTag} no disc image in {search.DiscFolder}: nothing to extract");
                    return;
                case DiscFolderState.Several:
                    Warn($"more than one disc image in {search.DiscFolder} ({string.Join(", ", search.Names)}): nothing extracted");
                    return;
            }

            var clock = Stopwatch.StartNew();
            ExtractionResult result = Extraction.Run(search.ImagePath!, assetFolder);
            string seconds = clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            if (result.Files.Count == 0)
            {
                GD.Print($"{AssetFolder.LogTag} found no textures to write on " +
                    $"{result.Source.Product} {result.Source.Version}: nothing written ({result.Details()})");
                return;
            }

            GD.Print($"{AssetFolder.LogTag} extracted {result.Files.Count} textures from {result.DiscFiles.Count(f => f.Files.Count > 0)} disc files " +
                $"of {result.Source.Product} {result.Source.Version} into {result.Folder}, listed in {result.ManifestPath}, " +
                $"in {seconds} s ({result.Details()})");
        }
        catch (DecodingException e)
        {
            Warn($"nothing extracted from the disc image: {e.Message}");
        }
        catch (Exception e)
        {
            Warn($"extraction failed ({e.GetType().Name}: {DecodingException.OneLine(e.Message)}); carrying on with what the folder holds");
        }
    }

    private static void Warn(string line) => GD.PushWarning($"{AssetFolder.LogTag} {line}");
}
