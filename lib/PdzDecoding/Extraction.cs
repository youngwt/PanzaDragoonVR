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

/// <summary>What extraction did with one <c>.MDB</c>/<c>.CGB</c> pair.</summary>
/// <param name="Name">The pair's name, as on the disc: <c>E03B0N0</c>.</param>
/// <param name="Folder">The folder its PNGs go in, in full; created only when <see cref="Files"/> is not empty.</param>
/// <param name="Files">The PNGs written, in number order.</param>
/// <param name="LeftOut">Textures numbered but not written, by colour mode, being in a mode not decoded yet.</param>
public sealed record DiscFileResult(string Name, string Folder, IReadOnlyList<ExtractedFile> Files, IReadOnlyDictionary<int, int> LeftOut)
{
    /// <summary>Textures numbered but not written, in all modes.</summary>
    public int Skipped => LeftOut.Values.Sum();

    /// <summary>Every texture the file numbers, written or not. 0 when no record fits its <c>.CGB</c>.</summary>
    public int Textures => Files.Count + Skipped;
}

/// <summary>One of the files extraction looks for that the disc lacks.</summary>
/// <param name="Name">The pair's name: <c>E03B0N0</c>.</param>
/// <param name="Absent">The first of its two files the disc lacks: <c>E03B0N0.CGB</c>.</param>
public readonly record struct MissingDiscFile(string Name, string Absent);

/// <summary>What <see cref="Extraction.Run"/> did.</summary>
/// <param name="Source">The disc's Saturn header.</param>
/// <param name="DiscFiles">Each file found on the disc, in the order of <see cref="Extraction.TextureFiles"/>, including those with no textures.</param>
/// <param name="Missing">The files looked for and not found, in the same order.</param>
/// <param name="Folder">The folder the disc files' folders go in, in full: <c>images/e03</c> in the output folder.</param>
public sealed record ExtractionResult(SaturnHeader Source, IReadOnlyList<DiscFileResult> DiscFiles, IReadOnlyList<MissingDiscFile> Missing, string Folder)
{
    /// <summary>Every PNG written, file by file, in number order.</summary>
    public IReadOnlyList<ExtractedFile> Files => DiscFiles.SelectMany(f => f.Files).ToList();

    /// <summary>Textures numbered but not written, in all files and modes.</summary>
    public int Skipped => DiscFiles.Sum(f => f.Skipped);

    /// <summary>Textures numbered but not written, by colour mode, over all files.</summary>
    public IReadOnlyDictionary<int, int> LeftOut
    {
        get
        {
            var modes = new SortedDictionary<int, int>();
            foreach (KeyValuePair<int, int> m in DiscFiles.SelectMany(f => f.LeftOut))
            {
                modes[m.Key] = modes.GetValueOrDefault(m.Key) + m.Value;
            }

            return modes;
        }
    }

    /// <summary>The files found whose <c>.MDB</c> holds no record that fits their <c>.CGB</c>.</summary>
    public IReadOnlyList<string> Empty => DiscFiles.Where(f => f.Textures == 0).Select(f => f.Name).ToList();

    /// <summary>
    /// One line, with no full stop, for the game's log and the PC command: PNGs written per file, the
    /// textures left out by mode, and the files missing or with no textures, for example
    /// <c>E03CMN 8, E03B0N0 28; left out, in colour modes not decoded yet: mode 0 9, mode 4 161; missing: E03END (no E03END.CGB)</c>.
    /// </summary>
    public string Details()
    {
        var parts = new List<string> { string.Join(", ", DiscFiles.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Name} {f.Files.Count}"))) };
        if (LeftOut.Count > 0)
        {
            parts.Add("left out, in colour modes not decoded yet: " +
                string.Join(", ", LeftOut.Select(m => string.Create(CultureInfo.InvariantCulture, $"mode {m.Key} {m.Value}"))));
        }

        if (Missing.Count > 0)
        {
            parts.Add("missing: " + string.Join(", ", Missing.Select(m => $"{m.Name} (no {m.Absent})")));
        }

        if (Empty.Count > 0)
        {
            parts.Add("no textures in: " + string.Join(", ", Empty));
        }

        return string.Join("; ", parts);
    }
}

/// <summary>
/// The one entry point the game and the PC command call: reads the disc image and writes the
/// textures of the nine Episode 3 files that carry their own colours (modes 1 and 5) as PNGs,
/// numbered and placed as the asset contract's "Extracted textures" says. Colour-bank modes, the
/// manifest and staging come later.
/// </summary>
public static class Extraction
{
    /// <summary>The library's version, as <c>generator</c> will give it: <c>pdz-extract 0.1</c>.</summary>
    public const string Version = "0.1";

    /// <summary>The disc files this version extracts, as the asset contract names them, in its order.</summary>
    public static readonly IReadOnlyList<string> TextureFiles = new[]
    {
        "E03CMN", "E03B0N0", "E03B0N1", "E03B0N2", "E03B1N0", "E03B1N1", "E03B1N2", "E03BOSS", "E03END",
    };

    /// <summary>
    /// Extracts from the cue sheet at <paramref name="cuePath"/> into <paramref name="outputFolder"/>.
    /// A file of <see cref="TextureFiles"/> the disc lacks is left out and named in the result; a disc
    /// with none of them is an error. Every texture of every file is decoded before any file is written,
    /// so a decoding error writes nothing; a write that fails part way may leave some PNGs written, until
    /// staging (story 9). A folder is created only for a disc file with a PNG to write.
    /// Throws <see cref="DecodingException"/> for a disc image or disc file it cannot read.
    /// </summary>
    public static ExtractionResult Run(string cuePath, string outputFolder)
    {
        SaturnHeader header;
        var decoded = new List<(DiscFileResult Result, List<byte[]> Pngs)>();
        var missing = new List<MissingDiscFile>();
        using (DiscImage disc = DiscImage.OpenCue(cuePath))
        {
            header = disc.Header();
            foreach (string name in TextureFiles)
            {
                DiscFile? mdb = disc.Find(name + ".MDB");
                DiscFile? cgb = disc.Find(name + ".CGB");
                if (mdb == null || cgb == null)
                {
                    missing.Add(new MissingDiscFile(name, name + (mdb == null ? ".MDB" : ".CGB")));
                    continue;
                }

                DecodedFile file = Decode(disc.ReadFile(mdb.Value), disc.ReadFile(cgb.Value), name);
                string folder = Path.GetFullPath(Path.Combine(outputFolder, FolderFor(name)));
                decoded.Add((new DiscFileResult(name, folder, file.Pngs.Select(p => p.File).ToList(), file.LeftOut),
                    file.Pngs.Select(p => p.Png).ToList()));
            }
        }

        if (decoded.Count == 0)
        {
            throw new DecodingException($"the disc has none of the Episode 3 texture files ({string.Join(", ", TextureFiles)}, each a .MDB and a .CGB)");
        }

        foreach ((DiscFileResult result, List<byte[]> pngs) in decoded)
        {
            if (result.Files.Count > 0)
            {
                Directory.CreateDirectory(result.Folder);
            }

            for (int i = 0; i < pngs.Count; i++)
            {
                File.WriteAllBytes(Path.Combine(outputFolder, result.Files[i].Path), pngs[i]);
            }
        }

        return new ExtractionResult(header, decoded.Select(d => d.Result).ToList(), missing,
            Path.GetFullPath(Path.Combine(outputFolder, "images", "e03")));
    }

    /// <summary>The PNGs of one pair's textures in colour modes 1 and 5, and how many were left out by mode.</summary>
    public static DecodedFile Decode(DiscImage disc, string name)
    {
        byte[] mdb = Read(disc, name + ".MDB");
        byte[] cgb = Read(disc, name + ".CGB");
        return Decode(mdb, cgb, name);
    }

    /// <summary>As <see cref="Decode(DiscImage, string)"/>, on the two files' bytes.</summary>
    public static DecodedFile Decode(byte[] mdb, byte[] cgb, string name)
    {
        List<TextureRecord> records = Textures.FindRecords(mdb);
        int? baseAddress = Textures.InferBase(records, cgb.Length);
        var pngs = new List<(ExtractedFile, byte[])>();
        var leftOut = new SortedDictionary<int, int>();
        if (baseAddress == null)
        {
            return new DecodedFile(pngs, leftOut);
        }

        foreach (NumberedTexture texture in Textures.Number(records, baseAddress.Value, cgb.Length))
        {
            TextureRecord r = texture.Record;
            byte[] rgba;
            switch (r.ColourMode)
            {
                case 1:
                    rgba = Textures.DecodeMode1(cgb, baseAddress.Value, r);
                    break;
                case 5:
                    rgba = Textures.DecodeMode5(cgb, baseAddress.Value, r);
                    break;
                default:
                    leftOut[r.ColourMode] = leftOut.GetValueOrDefault(r.ColourMode) + 1;
                    continue;
            }

            var file = new ExtractedFile(TextureId(name, texture.Number), TexturePath(name, texture.Number), r.Width, r.Height);
            pngs.Add((file, PngWriter.Encode(r.Width, r.Height, rgba)));
        }

        return new DecodedFile(pngs, leftOut);
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

/// <summary>One pair's decoded PNGs, in number order, and the textures left out, by colour mode.</summary>
public sealed record DecodedFile(List<(ExtractedFile File, byte[] Png)> Pngs, SortedDictionary<int, int> LeftOut);
