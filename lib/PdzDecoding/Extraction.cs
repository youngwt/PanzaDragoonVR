using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PdzDecoding;

/// <summary>What <see cref="DiscFolder.Find"/> found in an asset folder's <c>disc/</c>.</summary>
public enum DiscFolderState
{
    /// <summary><c>disc/</c> is absent or holds no <c>.cue</c> sheet.</summary>
    None,

    /// <summary>One <c>.cue</c> sheet, in <see cref="DiscSearch.CuePath"/>.</summary>
    One,

    /// <summary>More than one, named in <see cref="DiscSearch.Names"/>: nothing is extracted.</summary>
    Several,
}

/// <summary>The disc image found in <c>disc/</c>, if any.</summary>
public readonly record struct DiscSearch(DiscFolderState State, string DiscFolder, string? CuePath, IReadOnlyList<string> Names);

/// <summary>The <c>disc/</c> folder inside the asset folder, where the asset contract puts the player's disc image.</summary>
public static class DiscFolder
{
    /// <summary>The folder's name inside the asset folder, fixed by the asset contract.</summary>
    public const string Name = "disc";

    /// <summary>Looks for a <c>.cue</c> sheet in <c>disc/</c> inside <paramref name="assetFolder"/>. Other layouts come with story 4.</summary>
    public static DiscSearch Find(string assetFolder)
    {
        string folder = Path.Combine(assetFolder, Name);
        if (!Directory.Exists(folder))
        {
            return new DiscSearch(DiscFolderState.None, folder, null, Array.Empty<string>());
        }

        string[] cues = Directory.EnumerateFiles(folder)
            .Where(f => string.Equals(Path.GetExtension(f), ".cue", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        string[] names = cues.Select(f => Path.GetFileName(f)).ToArray();
        return cues.Length switch
        {
            0 => new DiscSearch(DiscFolderState.None, folder, null, names),
            1 => new DiscSearch(DiscFolderState.One, folder, cues[0], names),
            _ => new DiscSearch(DiscFolderState.Several, folder, null, names),
        };
    }
}

/// <summary>One PNG extraction wrote: its asset ID and its path relative to the output folder.</summary>
public readonly record struct ExtractedFile(string Id, string Path, int Width, int Height);

/// <summary>What <see cref="Extraction.Run"/> did.</summary>
/// <param name="Source">The disc's Saturn header.</param>
/// <param name="DiscFileName">The disc file the textures came from, as on the disc.</param>
/// <param name="Folder">The folder the PNGs were written to, in full.</param>
/// <param name="Files">The PNGs written, in number order.</param>
/// <param name="Skipped">Textures numbered but not written, being in a colour mode not decoded yet.</param>
public sealed record ExtractionResult(SaturnHeader Source, string DiscFileName, string Folder, IReadOnlyList<ExtractedFile> Files, int Skipped);

/// <summary>
/// The one entry point the game and the PC command call: reads the disc image and writes the
/// mode 1 textures of <c>E03B0N0</c> as PNGs, numbered and placed as the asset contract's
/// "Extracted textures" says. The manifest, staging and the other files and modes come later.
/// </summary>
public static class Extraction
{
    /// <summary>The library's version, as <c>generator</c> will give it: <c>pdz-extract 0.1</c>.</summary>
    public const string Version = "0.1";

    /// <summary>The one disc file this version extracts.</summary>
    public const string TextureFile = "E03B0N0";

    /// <summary>
    /// Extracts from the cue sheet at <paramref name="cuePath"/> into <paramref name="outputFolder"/>.
    /// Every texture is decoded before any file is written, so a decoding error writes nothing; a write
    /// that fails part way may leave some PNGs written, until staging (story 9). With no PNG to write,
    /// not even the folder is created.
    /// Throws <see cref="DecodingException"/> for a disc image or disc file it cannot read.
    /// </summary>
    public static ExtractionResult Run(string cuePath, string outputFolder)
    {
        SaturnHeader header;
        List<(ExtractedFile File, byte[] Png)> pngs;
        int skipped;
        using (DiscImage disc = DiscImage.OpenCue(cuePath))
        {
            header = disc.Header();
            (pngs, skipped) = Decode(disc, TextureFile);
        }

        string folder = Path.GetFullPath(Path.Combine(outputFolder, FolderFor(TextureFile)));
        if (pngs.Count > 0)
        {
            Directory.CreateDirectory(folder);
        }

        foreach ((ExtractedFile file, byte[] png) in pngs)
        {
            File.WriteAllBytes(Path.Combine(outputFolder, file.Path), png);
        }

        return new ExtractionResult(header, TextureFile, folder, pngs.Select(p => p.File).ToList(), skipped);
    }

    /// <summary>The PNGs of one <c>.MDB</c>/<c>.CGB</c> pair's mode 1 textures, and how many textures were left out.</summary>
    public static (List<(ExtractedFile File, byte[] Png)> Pngs, int Skipped) Decode(DiscImage disc, string name)
    {
        byte[] mdb = Read(disc, name + ".MDB");
        byte[] cgb = Read(disc, name + ".CGB");
        return Decode(mdb, cgb, name);
    }

    /// <summary>As <see cref="Decode(DiscImage, string)"/>, on the two files' bytes.</summary>
    public static (List<(ExtractedFile File, byte[] Png)> Pngs, int Skipped) Decode(byte[] mdb, byte[] cgb, string name)
    {
        List<TextureRecord> records = Textures.FindRecords(mdb);
        int? baseAddress = Textures.InferBase(records, cgb.Length);
        var pngs = new List<(ExtractedFile, byte[])>();
        if (baseAddress == null)
        {
            return (pngs, 0);
        }

        int skipped = 0;
        foreach (NumberedTexture texture in Textures.Number(records, baseAddress.Value, cgb.Length))
        {
            TextureRecord r = texture.Record;
            if (r.ColourMode != 1)
            {
                skipped++;
                continue;
            }

            byte[] rgba = Textures.DecodeMode1(cgb, baseAddress.Value, r);
            var file = new ExtractedFile(TextureId(name, texture.Number), TexturePath(name, texture.Number), r.Width, r.Height);
            pngs.Add((file, PngWriter.Encode(r.Width, r.Height, rgba)));
        }

        return (pngs, skipped);
    }

    /// <summary>The asset ID of texture <paramref name="number"/> of a disc file: <c>e03.tex.e03b0n0.0007</c>.</summary>
    public static string TextureId(string discFile, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"e03.tex.{discFile.ToLowerInvariant()}.{number:D4}");

    /// <summary>Where texture <paramref name="number"/> of a disc file goes in the asset folder: <c>images/e03/e03b0n0/0007.png</c>.</summary>
    public static string TexturePath(string discFile, int number) =>
        string.Create(CultureInfo.InvariantCulture, $"{FolderFor(discFile)}/{number:D4}.png");

    private static string FolderFor(string discFile) => $"images/e03/{discFile.ToLowerInvariant()}";

    private static byte[] Read(DiscImage disc, string path) =>
        disc.Find(path) is DiscFile file
            ? disc.ReadFile(file)
            : throw new DecodingException($"the disc has no {path}");
}
