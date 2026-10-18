using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PdzDecoding;

/// <summary>What <see cref="DiscFolder.Find"/> found in an asset folder's <c>disc/</c>.</summary>
public enum DiscFolderState
{
    /// <summary><c>disc/</c> is absent or holds no disc image: no <c>.cue</c> sheet and no <c>.iso</c>.</summary>
    None,

    /// <summary>One disc image, a <c>.cue</c> sheet or an <c>.iso</c>, in <see cref="DiscSearch.ImagePath"/>.</summary>
    One,

    /// <summary>More than one, named in <see cref="DiscSearch.Names"/>: nothing is extracted.</summary>
    Several,
}

/// <summary>The disc image found in <c>disc/</c>, if any: the <c>.cue</c> sheet or the <c>.iso</c>.</summary>
public readonly record struct DiscSearch(DiscFolderState State, string DiscFolder, string? ImagePath, IReadOnlyList<string> Names);

/// <summary>The <c>disc/</c> folder inside the asset folder, where the asset contract puts the player's disc image.</summary>
public static class DiscFolder
{
    /// <summary>The folder's name inside the asset folder, fixed by the asset contract.</summary>
    public const string Name = "disc";

    /// <summary>The extensions of a disc image: a cue sheet, which names its <c>.bin</c> files, or a plain ISO.</summary>
    public static readonly IReadOnlyList<string> ImageExtensions = new[] { ".cue", ".iso" };

    /// <summary>
    /// Looks for a disc image in <c>disc/</c> inside <paramref name="assetFolder"/>: a <c>.cue</c> sheet
    /// (its <c>.bin</c> files are part of it, not images of their own) or an <c>.iso</c>, matched without
    /// regard to case.
    /// </summary>
    public static DiscSearch Find(string assetFolder)
    {
        string folder = Path.Combine(assetFolder, Name);
        if (!Directory.Exists(folder))
        {
            return new DiscSearch(DiscFolderState.None, folder, null, Array.Empty<string>());
        }

        string[] images = Directory.EnumerateFiles(folder)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        string[] names = images.Select(f => Path.GetFileName(f)).ToArray();
        return images.Length switch
        {
            0 => new DiscSearch(DiscFolderState.None, folder, null, names),
            1 => new DiscSearch(DiscFolderState.One, folder, images[0], names),
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
/// <param name="LeftOut">Textures numbered but not written, by colour mode: in a mode not decoded yet, or a colour-bank mode (0 or 4) with no colour table.</param>
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
/// <param name="Colours">The colour RAM modes 0 and 4 were coloured from, and the table it came from; null when there is none.</param>
/// <param name="ColourNotice">Why there is no colour RAM, so modes 0 and 4 are left out: one line, no full stop. Null when there is.</param>
public sealed record ExtractionResult(SaturnHeader Source, IReadOnlyList<DiscFileResult> DiscFiles, IReadOnlyList<MissingDiscFile> Missing, string Folder,
    ColourRam? Colours, string? ColourNotice)
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
    /// colour table modes 0 and 4 were coloured from or why there is none, the textures left out by
    /// mode, and the files missing or with no textures, for example
    /// <c>E03CMN 125, E03B0N0 28; colour table at 0xE23C of EPISODE3.PRG (table 2 of 2), 5 blocks; missing: E03END (no E03END.CGB)</c>
    /// or <c>E03CMN 8; no colours for modes 0 and 4: the disc has no EPISODE3.PRG; left out: mode 0 1, mode 4 116</c>.
    /// </summary>
    public string Details()
    {
        var parts = new List<string> { string.Join(", ", DiscFiles.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Name} {f.Files.Count}"))) };
        parts.Add(ColourLine());
        if (LeftOut.Count > 0)
        {
            parts.Add("left out: " +
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

    /// <summary>One line, with no full stop: the colour table used (<see cref="ColourRam.Describe"/>), or <c>no colours for modes 0 and 4: &lt;notice&gt;</c>.</summary>
    public string ColourLine() => Colours?.Describe() ?? $"no colours for modes 0 and 4: {ColourNotice}";
}

/// <summary>
/// The one entry point the game and the PC command call: reads the disc image and writes the
/// textures of the nine Episode 3 files as PNGs, numbered and placed as the asset contract's
/// "Extracted textures" says: modes 1 and 5 from their own colours, modes 4 and 0 from the colour RAM
/// <c>EPISODE3.PRG</c>'s upload table fills (see <see cref="ColourBanks"/>). The manifest and staging come later.
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
    /// Extracts from the disc image at <paramref name="imagePath"/>, a <c>.cue</c> sheet or an <c>.iso</c>
    /// (see <see cref="DiscImage.OpenImage"/>), into <paramref name="outputFolder"/>.
    /// A file of <see cref="TextureFiles"/> the disc lacks is left out and named in the result; a disc
    /// with none of them is an error. Colour RAM for modes 4 and 0 is built once, from <c>EPISODE3.PRG</c>;
    /// a disc without it, or without an upload table in it, leaves those modes out and says why in the
    /// result. Every texture of every file is decoded before any file is written,
    /// so a decoding error writes nothing; a write that fails part way may leave some PNGs written, until
    /// staging (story 9). A folder is created only for a disc file with a PNG to write.
    /// Throws <see cref="DecodingException"/> for a disc image or disc file it cannot read.
    /// </summary>
    public static ExtractionResult Run(string imagePath, string outputFolder)
    {
        SaturnHeader header;
        var decoded = new List<(DiscFileResult Result, List<byte[]> Pngs)>();
        var missing = new List<MissingDiscFile>();
        ColourRam? colours;
        string? notice;
        using (DiscImage disc = DiscImage.OpenImage(imagePath))
        {
            header = disc.Header();
            (colours, notice) = ColourBanks.FromDisc(disc);
            foreach (string name in TextureFiles)
            {
                DiscFile? mdb = disc.Find(name + ".MDB");
                DiscFile? cgb = disc.Find(name + ".CGB");
                if (mdb == null || cgb == null)
                {
                    missing.Add(new MissingDiscFile(name, name + (mdb == null ? ".MDB" : ".CGB")));
                    continue;
                }

                DecodedFile file = Decode(disc.ReadFile(mdb.Value), disc.ReadFile(cgb.Value), name, colours);
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
            Path.GetFullPath(Path.Combine(outputFolder, "images", "e03")), colours, notice);
    }

    /// <summary>
    /// The PNGs of one pair's textures in colour modes 0, 1, 4 and 5, and how many were left out by
    /// mode, with modes 0 and 4 coloured from the disc's <c>EPISODE3.PRG</c> as <see cref="Run"/> does.
    /// </summary>
    public static DecodedFile Decode(DiscImage disc, string name)
    {
        byte[] mdb = Read(disc, name + ".MDB");
        byte[] cgb = Read(disc, name + ".CGB");
        return Decode(mdb, cgb, name, ColourBanks.FromDisc(disc).Colours);
    }

    /// <summary>
    /// As <see cref="Decode(DiscImage, string)"/>, on the two files' bytes, with modes 0 and 4 coloured
    /// from <paramref name="colours"/>, or left out when it is null.
    /// </summary>
    public static DecodedFile Decode(byte[] mdb, byte[] cgb, string name, ColourRam? colours = null)
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
                case 4 when colours != null:
                    rgba = Textures.DecodeMode4(cgb, baseAddress.Value, r, colours);
                    break;
                case 0 when colours != null:
                    rgba = Textures.DecodeMode0(cgb, baseAddress.Value, r, colours);
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
