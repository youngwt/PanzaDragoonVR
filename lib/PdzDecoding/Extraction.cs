using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PdzDecoding;

/// <summary>One PNG extraction wrote: its asset ID and its path relative to the output folder.</summary>
public readonly record struct ExtractedFile(string Id, string Path, int Width, int Height);

/// <summary>What extraction did with one <c>.MDB</c>/<c>.CGB</c> pair.</summary>
/// <param name="Name">The pair's name, as on the disc: <c>E03B0N0</c>.</param>
/// <param name="Folder">The folder its PNGs go in, in full; created only when <see cref="Files"/> is not empty.</param>
/// <param name="Files">The PNGs written, in number order.</param>
/// <param name="LeftOut">Textures numbered but not written, by colour mode: in a mode extraction does not decode (any but 0, 1, 4 and 5), or in a colour-bank mode (0 or 4) with no colour table, or a table none of whose pairs could be applied.</param>
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
/// <param name="ManifestPath">The <c>manifest.json</c> written at the top of the output folder, in full; null when no PNG or model was written, and so no manifest.</param>
/// <param name="PlaneFiles">The floor and canopy PNGs written, <c>e03.floor</c> then <c>e03.canopy</c>; empty when there are none.</param>
/// <param name="PlaneSources">Where in <c>EPISODE3.PRG</c> the floor and canopy came from; null when there are none.</param>
/// <param name="PlaneNotice">Why there is no floor and canopy: one line, no full stop. Null when there are.</param>
/// <param name="ModelFiles">The dragon forms' <c>.glb</c> files written, in form order; empty when there are none.</param>
/// <param name="DragonSources">Where the dragons' seats came from; null when there are no dragons.</param>
/// <param name="DragonNotice">Why there are no dragons: one line, no full stop. Null when there are.</param>
public sealed record ExtractionResult(SaturnHeader Source, IReadOnlyList<DiscFileResult> DiscFiles, IReadOnlyList<MissingDiscFile> Missing, string Folder,
    ColourRam? Colours, string? ColourNotice, string? ManifestPath,
    IReadOnlyList<ExtractedFile> PlaneFiles, PlaneSources? PlaneSources, string? PlaneNotice,
    IReadOnlyList<ExtractedModel> ModelFiles, DragonSources? DragonSources, string? DragonNotice)
{
    /// <summary>Every texture PNG written, file by file, in number order; the floor and canopy are in <see cref="PlaneFiles"/>.</summary>
    public IReadOnlyList<ExtractedFile> Files => DiscFiles.SelectMany(f => f.Files).ToList();

    /// <summary>How many disc files had at least one PNG written.</summary>
    public int DiscFilesWritten => DiscFiles.Count(f => f.Files.Count > 0);

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
    /// mode, the floor and canopy's sources or why there are none, the dragons or why there are none,
    /// and the files missing or with no textures, for example
    /// <c>E03CMN 125, E03B0N0 28; colour table at 0xE23C of EPISODE3.PRG (table 2 of 2), 5 blocks; floor and canopy from EPISODE3.PRG: cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244; 10 dragon forms, seats from 0x54840 of 1ST_READ.PRG; missing: E03END (no E03END.CGB)</c>
    /// or <c>E03CMN 8; no colours for modes 0 and 4: the disc has no EPISODE3.PRG; left out: mode 0 1, mode 4 116; no floor and canopy: the disc has no EPISODE3.PRG; no dragons: the disc has no DRA01A.MDB</c>.
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

        parts.Add(PlaneLine());
        parts.Add(DragonLine());
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

    /// <summary>One line, with no full stop: the floor and canopy's sources (<see cref="PdzDecoding.PlaneSources.Describe"/>), or <c>no floor and canopy: &lt;notice&gt;</c>.</summary>
    public string PlaneLine() => PlaneSources?.Describe() ?? $"no floor and canopy: {PlaneNotice}";

    /// <summary>One line, with no full stop: the dragons written (<see cref="PdzDecoding.DragonSources.Describe"/>), or <c>no dragons: &lt;notice&gt;</c>.</summary>
    public string DragonLine() => DragonSources?.Describe() ?? $"no dragons: {DragonNotice}";
}

/// <summary>What <see cref="Extraction.Run"/> is doing when it reports progress.</summary>
public enum ExtractionStage
{
    /// <summary>Reading and decoding one disc file's <c>.MDB</c> and <c>.CGB</c>; nothing is written yet.</summary>
    Reading,

    /// <summary>Writing one disc file's PNGs into the staging folder.</summary>
    Writing,

    /// <summary>Every file is written in the staging folder: moving them into place, <c>manifest.json</c> last.</summary>
    MovingIntoPlace,
}

/// <summary>One step of <see cref="Extraction.Run"/>, reported as it starts.</summary>
/// <param name="Stage">What is being done.</param>
/// <param name="DiscFile">The disc file it is done to, as <see cref="Extraction.TextureFiles"/> names it; null for <see cref="ExtractionStage.MovingIntoPlace"/>.</param>
/// <param name="Number">The disc file's place in <see cref="Extraction.TextureFiles"/>, from 1; <paramref name="Count"/> for <see cref="ExtractionStage.MovingIntoPlace"/>.</param>
/// <param name="Count">How many disc files there are: <see cref="Extraction.TextureFiles"/>' length.</param>
public readonly record struct ExtractionProgress(ExtractionStage Stage, string? DiscFile, int Number, int Count);

/// <summary>
/// The one entry point the game and the PC command call: reads the disc image and writes the
/// textures of the nine Episode 3 files as PNGs, numbered and placed as the asset contract's
/// "Extracted textures" says: modes 1 and 5 from their own colours, modes 4 and 0 from the colour RAM
/// <c>EPISODE3.PRG</c>'s upload table fills (see <see cref="ColourBanks"/>), then <c>manifest.json</c>
/// listing them (see <see cref="Manifest"/>), through the staging folder of the contract's
/// "What extraction writes" (see <see cref="StagingFolder"/>). The ten dragon forms go the same way, as
/// <c>.glb</c> files (see <see cref="Dragons"/>).
/// </summary>
public static class Extraction
{
    /// <summary>The library's version, as the manifest's <c>generator</c> gives it: <c>pdz-extract 0.3</c>.</summary>
    public const string Version = "0.3";

    /// <summary>
    /// The staging folder inside the output folder, fixed by the asset contract: extraction writes
    /// every file here first and moves them into place only when all are written.
    /// </summary>
    public const string StagingFolder = "extracting";

    /// <summary>The folder inside the output folder that the disc files' folders go in.</summary>
    private const string ImageFolder = "images/e03";

    /// <summary>The folder inside the output folder that the dragon forms go in.</summary>
    private const string ModelFolder = "models";

    /// <summary>Where the floor (<see cref="Planes.FloorId"/>) goes in the asset folder.</summary>
    public const string FloorPath = ImageFolder + "/floor.png";

    /// <summary>Where the canopy (<see cref="Planes.CanopyId"/>) goes in the asset folder.</summary>
    public const string CanopyPath = ImageFolder + "/canopy.png";

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
    /// a disc without it, without an upload table in it, or whose chosen table has no pair that can be
    /// applied, leaves those modes out and says why in the result. The floor and canopy (see <see cref="Planes"/>)
    /// come from the same <c>EPISODE3.PRG</c>, read once, and are written as <see cref="FloorPath"/> and
    /// <see cref="CanopyPath"/> with the textures; a disc whose planes cannot be found or unpacked still has
    /// its textures written, and the result says why. The dragon forms (see <see cref="Dragons"/>) are written
    /// as <see cref="Dragons.FormPath"/> with them, on the same terms: all ten or, with a notice, none. Every texture of every file is decoded before any file is written, so a decoding error
    /// writes nothing. The PNGs and models, then <c>manifest.json</c> listing them, are written into
    /// <see cref="StagingFolder"/> inside <paramref name="outputFolder"/>, which is emptied first (so a
    /// leftover from an interrupted run is cleared); only when every one is written are they moved into
    /// place, the manifest last, replacing any manifest there, and the staging folder removed. A failure
    /// before that moves nothing, leaves the previous files and manifest as they were, and removes the
    /// staging folder when it can. A failure while moving into place (reported as
    /// <see cref="ExtractionStage.MovingIntoPlace"/>) may leave some new files in place, but never the new
    /// manifest, which moves last, so the previous manifest is kept. A folder is created only for a disc file with a PNG to write. Files
    /// extraction did not write stay, unlisted. With no PNG or model written, no manifest is written and any
    /// already there is left alone.
    /// <paramref name="progress"/>, when given, is called on the calling thread as each step starts: each
    /// disc file read, each disc file written, then the move into place. An exception it throws stops
    /// extraction as any other failure does.
    /// Throws <see cref="DecodingException"/> for a disc image or disc file it cannot read, with
    /// <see cref="DecodingException.DiscFile"/> naming the disc file when the error is one file's.
    /// </summary>
    public static ExtractionResult Run(string imagePath, string outputFolder, Action<ExtractionProgress>? progress = null)
    {
        SaturnHeader header;
        var decoded = new List<(DiscFileResult Result, List<byte[]> Pngs, int Number)>();
        var missing = new List<MissingDiscFile>();
        ColourRam? colours;
        string? notice;
        PlaneImages? planes;
        string? planeNotice;
        DragonModels? dragons = null;
        string? dragonNotice = null;
        int count = TextureFiles.Count;
        using (DiscImage disc = DiscImage.OpenImage(imagePath))
        {
            header = disc.Header();
            (byte[]? prg, string? readNotice) = ColourBanks.ReadProgram(disc);
            (colours, notice) = prg == null ? (null, readNotice) : ColourBanks.FromProgram(prg);
            (planes, planeNotice) = prg == null ? (null, readNotice) : Planes.FromProgram(prg);
            for (int i = 0; i < count; i++)
            {
                string name = TextureFiles[i];
                progress?.Invoke(new ExtractionProgress(ExtractionStage.Reading, name, i + 1, count));
                DiscFile? mdb = disc.Find(name + ".MDB");
                DiscFile? cgb = disc.Find(name + ".CGB");
                if (mdb == null || cgb == null)
                {
                    missing.Add(new MissingDiscFile(name, name + (mdb == null ? ".MDB" : ".CGB")));
                    continue;
                }

                DecodedFile file;
                try
                {
                    file = Decode(disc.ReadFile(mdb.Value), disc.ReadFile(cgb.Value), name, colours);
                }
                catch (DecodingException e) when (e.DiscFile == null)
                {
                    throw new DecodingException(e.Message, e) { DiscFile = name };
                }

                string folder = Path.GetFullPath(Path.Combine(outputFolder, FolderFor(name)));
                decoded.Add((new DiscFileResult(name, folder, file.Pngs.Select(p => p.File).ToList(), file.LeftOut),
                    file.Pngs.Select(p => p.Png).ToList(), i + 1));
            }

            // In the last disc file's reading stage, as the planes are in its writing stage: no stage of their own.
            if (decoded.Count > 0)
            {
                (dragons, dragonNotice) = Dragons.FromDisc(disc);
            }
        }

        if (decoded.Count == 0)
        {
            throw new DecodingException($"the disc has none of the Episode 3 texture files ({string.Join(", ", TextureFiles)}, each a .MDB and a .CGB)");
        }

        // The planes are encoded with the textures, before anything is written.
        var planeFiles = new List<(ExtractedFile File, byte[] Png)>();
        if (planes != null)
        {
            planeFiles.Add((new ExtractedFile(Planes.FloorId, FloorPath, Planes.Size, Planes.Size), PngWriter.Encode(Planes.Size, Planes.Size, planes.Floor)));
            planeFiles.Add((new ExtractedFile(Planes.CanopyId, CanopyPath, Planes.Size, Planes.Size), PngWriter.Encode(Planes.Size, Planes.Size, planes.Canopy)));
        }

        string staging = Path.Combine(outputFolder, StagingFolder);
        var entries = new List<ManifestEntry>();
        string? manifestPath = null;
        try
        {
            Empty(staging);
            foreach ((DiscFileResult result, List<byte[]> pngs, int number) in decoded)
            {
                if (result.Files.Count == 0)
                {
                    continue;
                }

                progress?.Invoke(new ExtractionProgress(ExtractionStage.Writing, result.Name, number, count));
                Directory.CreateDirectory(Path.Combine(staging, FolderFor(result.Name)));
                for (int i = 0; i < pngs.Count; i++)
                {
                    File.WriteAllBytes(Path.Combine(staging, result.Files[i].Path), pngs[i]);
                    entries.Add(new ManifestEntry(result.Files[i].Id, result.Files[i].Path, pngs[i]));
                }
            }

            // The planes go with the textures, in the last disc file's writing stage: no stage of their own.
            if (planeFiles.Count > 0)
            {
                Directory.CreateDirectory(Path.Combine(staging, ImageFolder));
                foreach ((ExtractedFile file, byte[] png) in planeFiles)
                {
                    File.WriteAllBytes(Path.Combine(staging, file.Path), png);
                    entries.Add(new ManifestEntry(file.Id, file.Path, png));
                }
            }

            if (dragons != null)
            {
                Directory.CreateDirectory(Path.Combine(staging, ModelFolder));
                foreach ((ExtractedModel file, byte[] glb) in dragons.Files)
                {
                    File.WriteAllBytes(Path.Combine(staging, file.Path), glb);
                    entries.Add(new ManifestEntry(file.Id, file.Path, glb, Manifest.ModelKind));
                }
            }

            if (entries.Count > 0)
            {
                File.WriteAllBytes(Path.Combine(staging, Manifest.FileName), Manifest.Write(header, entries));
            }

            // Every file is written: only now does anything in the output folder change.
            progress?.Invoke(new ExtractionProgress(ExtractionStage.MovingIntoPlace, null, count, count));
            foreach (DiscFileResult result in decoded.Select(d => d.Result).Where(r => r.Files.Count > 0))
            {
                Directory.CreateDirectory(result.Folder);
                foreach (ExtractedFile file in result.Files)
                {
                    File.Move(Path.Combine(staging, file.Path), Path.Combine(outputFolder, file.Path), overwrite: true);
                }
            }

            if (planeFiles.Count > 0)
            {
                Directory.CreateDirectory(Path.Combine(outputFolder, ImageFolder));
                foreach ((ExtractedFile file, _) in planeFiles)
                {
                    File.Move(Path.Combine(staging, file.Path), Path.Combine(outputFolder, file.Path), overwrite: true);
                }
            }

            if (dragons != null)
            {
                Directory.CreateDirectory(Path.Combine(outputFolder, ModelFolder));
                foreach ((ExtractedModel file, _) in dragons.Files)
                {
                    File.Move(Path.Combine(staging, file.Path), Path.Combine(outputFolder, file.Path), overwrite: true);
                }
            }

            if (entries.Count > 0)
            {
                manifestPath = Path.GetFullPath(Path.Combine(outputFolder, Manifest.FileName));
                File.Move(Path.Combine(staging, Manifest.FileName), manifestPath, overwrite: true);
            }
        }
        catch
        {
            TryRemove(staging);
            throw;
        }

        // Everything is in place; a staging folder that cannot be removed now is emptied by the next run.
        TryRemove(staging);
        return new ExtractionResult(header, decoded.Select(d => d.Result).ToList(), missing,
            Path.GetFullPath(Path.Combine(outputFolder, ImageFolder)), colours, notice, manifestPath,
            planeFiles.Select(p => p.File).ToList(), planes?.Sources, planeNotice,
            dragons?.Files.Select(f => f.File).ToList() ?? new List<ExtractedModel>(), dragons?.Sources, dragonNotice);
    }

    /// <summary>Makes <paramref name="folder"/> an empty folder, removing whatever was there.</summary>
    private static void Empty(string folder)
    {
        if (File.Exists(folder))
        {
            File.Delete(folder);
        }
        else if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }

        Directory.CreateDirectory(folder);
    }

    private static void TryRemove(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left for the next extraction to empty, as the asset contract allows.
        }
    }

    /// <summary>
    /// The PNGs of one pair's textures in colour modes 0, 1, 4 and 5, from the two files' bytes, and how
    /// many were left out by mode, with modes 0 and 4 coloured from <paramref name="colours"/>, or left
    /// out when it is null.
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

    private static string FolderFor(string discFile) => $"{ImageFolder}/{discFile.ToLowerInvariant()}";
}

/// <summary>One pair's decoded PNGs, in number order, and the textures left out, by colour mode.</summary>
public sealed record DecodedFile(List<(ExtractedFile File, byte[] Png)> Pngs, SortedDictionary<int, int> LeftOut);
