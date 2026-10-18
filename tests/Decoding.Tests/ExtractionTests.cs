using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;
using static PdzDecoding.Tests.SyntheticTextures;

namespace PdzDecoding.Tests;

/// <summary>
/// The extraction entry point on whole disc images built here, one or more tests per row of the
/// stories' matrices: disc present, no disc, bad image, a repeated record and transparency; then the
/// nine Episode 3 files, mode 5, a file missing, none present and a file with no textures; then the
/// colour-bank modes from <c>EPISODE3.PRG</c>'s tables: two, one, none, a bad pair, and modes 1 and 5
/// unchanged; then each disc image layout: per-track and single-file cue sheets, MODE1/2048 tracks and plain ISOs.
/// </summary>
public sealed class ExtractionTests : IDisposable
{
    private readonly string _assets;
    private readonly string _disc;

    public ExtractionTests()
    {
        _assets = Path.Combine(Path.GetTempPath(), "pdz-extract-" + Guid.NewGuid().ToString("N"));
        _disc = Path.Combine(_assets, DiscFolder.Name);
        Directory.CreateDirectory(_disc);
    }

    public void Dispose() => Directory.Delete(_assets, recursive: true);

    private string TextureFolder => Path.Combine(_assets, "images", "e03", "e03b0n0");

    /// <summary>A disc with <c>E03B0N0</c> made of <paramref name="mdb"/> and <paramref name="cgb"/>, and an <c>E03CMN.MDB</c> without its <c>.CGB</c>, so that file is missing; or the files of <paramref name="others"/> instead of that.</summary>
    private string DiscWith(byte[] mdb, byte[] cgb, SyntheticDisc? others = null) =>
        (others ?? new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.With("E03CMN.MDB", new byte[8]))
            .With("E03B0N0.MDB", mdb)
            .With("E03B0N0.CGB", cgb)
            .WriteCue(_disc);

    [Fact]
    public void Run_DiscPresent_WritesTheMode1TexturesNumbered()
    {
        string cue = DiscWith(Mdb(Record(), Record(pixels: 16), Record(pixels: 32, pmod: Mode1 | 0x40)), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(new[] { "e03.tex.e03b0n0.0000", "e03.tex.e03b0n0.0001", "e03.tex.e03b0n0.0002" }, result.Files.Select(f => f.Id));
        Assert.Equal(new[] { "images/e03/e03b0n0/0000.png", "images/e03/e03b0n0/0001.png", "images/e03/e03b0n0/0002.png" }, result.Files.Select(f => f.Path));
        Assert.Equal(new[] { "0000.png", "0001.png", "0002.png" }, Directory.GetFiles(TextureFolder).Select(Path.GetFileName).OrderBy(n => n));
        Assert.Equal("GS-9049", result.Source.Product);
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Run_DiscPresent_WritesEachPngAsTheWriterEncodesIt()
    {
        string cue = DiscWith(Mdb(Record()), Cgb());

        Extraction.Run(cue, _assets);

        TextureRecord r = Textures.FindRecords(Mdb(Record())).Single();
        Assert.Equal(PngWriter.Encode(8, 2, Textures.DecodeMode1(Cgb(), Base, r)), File.ReadAllBytes(Path.Combine(TextureFolder, "0000.png")));
    }

    [Fact]
    public void Run_TwoRecordsWithTheSameFourWords_WritesOneTexture()
    {
        string cue = DiscWith(Mdb(Record(), Record(), Record(pixels: 16)), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(new[] { "e03.tex.e03b0n0.0000", "e03.tex.e03b0n0.0001" }, result.Files.Select(f => f.Id));
    }

    [Fact]
    public void Run_TransparencyBit_IsTheOnlyDifferenceBetweenTwoTextures()
    {
        string cue = DiscWith(Mdb(Record(), Record(pmod: Mode1 | 0x40)), Cgb());

        Extraction.Run(cue, _assets);

        // The same pixels and table: only index 0's alpha differs, so the PNGs differ.
        Assert.NotEqual(File.ReadAllBytes(Path.Combine(TextureFolder, "0000.png")), File.ReadAllBytes(Path.Combine(TextureFolder, "0001.png")));
    }

    [Fact]
    public void Run_OtherColourModes_KeepTheirNumbersButAreNotWritten()
    {
        string cue = DiscWith(Mdb(Record(pmod: 0x0420), Record()), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal("e03.tex.e03b0n0.0001", Assert.Single(result.Files).Id);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(new[] { "0001.png" }, Directory.GetFiles(TextureFolder).Select(Path.GetFileName));
    }

    [Fact]
    public void Run_NoMode1Texture_CreatesNoFolder()
    {
        string cue = DiscWith(Mdb(Record(pmod: 0x0420)), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Empty(result.Files);
        Assert.Equal(1, result.Skipped);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
    }

    [Fact]
    public void OneLine_JoinsLinesAndDropsTheClosingFullStop()
    {
        Assert.Equal("Access to the path '/a.bin' is denied", DecodingException.OneLine("Access to the path '/a.bin' is denied."));
        Assert.Equal("first line second line", DecodingException.OneLine("first line\r\nsecond line.\n"));
    }

    [Fact]
    public void Run_CueNamesAMissingBin_WritesNothing()
    {
        string cue = DiscWith(Mdb(Record()), Cgb());
        File.Delete(Path.Combine(_disc, "Game (Track 1).bin"));

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(cue, _assets));

        Assert.Contains("which is missing", e.Message);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
    }

    [Fact]
    public void Run_NoIsoVolume_WritesNothing()
    {
        string cue = Path.Combine(_disc, "Game.cue");
        File.WriteAllBytes(Path.Combine(_disc, "Game (Track 1).bin"), new byte[40 * DiscImage.RawSectorSize]);
        File.WriteAllText(cue, SyntheticDisc.Cue("Game (Track 1).bin"));

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(cue, _assets));

        Assert.Contains("no ISO9660 volume at sector 16", e.Message);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
    }

    [Fact]
    public void Run_DiscWithNoneOfTheNineFiles_IsAnErrorNamingThemAndWritesNothing()
    {
        // E03B0N0 lacks its .CGB, so no pair of the nine is whole.
        string cue = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).With("OTHER.CGB", Cgb()).WriteCue(_disc);

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(cue, _assets));

        Assert.Equal("the disc has none of the Episode 3 texture files (E03CMN, E03B0N0, E03B0N1, E03B0N2, E03B1N0, " +
            "E03B1N1, E03B1N2, E03BOSS, E03END, each a .MDB and a .CGB)", e.Message);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
    }

    [Fact]
    public void Run_AllNineFiles_WritesEachUnderItsOwnFolder()
    {
        var disc = new SyntheticDisc();
        foreach (string name in Extraction.TextureFiles)
        {
            disc.With(name + ".MDB", Mdb(Record(), Record(pixels: 16))).With(name + ".CGB", Cgb());
        }

        ExtractionResult result = Extraction.Run(disc.WriteCue(_disc), _assets);

        Assert.Equal(Extraction.TextureFiles, result.DiscFiles.Select(f => f.Name));
        Assert.Empty(result.Missing);
        Assert.Equal(18, result.Files.Count);
        Assert.Equal(new[] { "e03b0n0", "e03b0n1", "e03b0n2", "e03b1n0", "e03b1n1", "e03b1n2", "e03boss", "e03cmn", "e03end" },
            Directory.GetDirectories(Path.Combine(_assets, "images", "e03")).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal("e03.tex.e03cmn.0000", result.Files[0].Id);
        Assert.Equal("images/e03/e03end/0001.png", result.Files[^1].Path);
        Assert.Equal(Path.GetFullPath(Path.Combine(_assets, "images", "e03")), result.Folder);
    }

    [Fact]
    public void Run_Mode5Record_IsWrittenAsTheWriterEncodesIt()
    {
        string cue = DiscWith(Mdb(Record(pmod: Mode5), Record(pixels: 32)), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(new[] { "0000.png", "0001.png" }, Directory.GetFiles(TextureFolder).Select(Path.GetFileName).OrderBy(n => n));
        TextureRecord r = Textures.FindRecords(Mdb(Record(pmod: Mode5))).Single();
        Assert.Equal(PngWriter.Encode(8, 2, Textures.DecodeMode5(Cgb(), Base, r)), File.ReadAllBytes(Path.Combine(TextureFolder, "0000.png")));
        Assert.Equal(0, result.Skipped);
    }

    [Fact]
    public void Run_AFileMissing_ExtractsTheRestAndNamesIt()
    {
        string cue = new SyntheticDisc()
            .With("E03CMN.MDB", Mdb(Record()))
            .With("E03CMN.CGB", Cgb())
            .With("E03B0N0.MDB", Mdb(Record()))
            .With("E03BOSS.CGB", Cgb())
            .WriteCue(_disc);

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal("E03CMN", Assert.Single(result.DiscFiles).Name);
        Assert.Equal(new[] { "e03.tex.e03cmn.0000" }, result.Files.Select(f => f.Id));
        Assert.Contains(new MissingDiscFile("E03B0N0", "E03B0N0.CGB"), result.Missing);
        Assert.Contains(new MissingDiscFile("E03BOSS", "E03BOSS.MDB"), result.Missing);
        Assert.Equal(8, result.Missing.Count);
        Assert.Contains("missing: E03B0N0 (no E03B0N0.CGB), E03B0N1 (no E03B0N1.MDB)", result.Details());
        Assert.False(Directory.Exists(TextureFolder));
    }

    [Fact]
    public void Run_FileWithNoRecordsThatFit_HasNoFolderAndIsNoted()
    {
        string cue = DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc()
            .With("E03END.MDB", Mdb(Record(pixels: 128)))
            .With("E03END.CGB", Cgb()));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(new[] { "E03END" }, result.Empty);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images", "e03", "e03end")));
        Assert.Single(result.Files);
        Assert.EndsWith("no textures in: E03END", result.Details());
    }

    [Fact]
    public void Run_LeftOutTextures_AreCountedByMode()
    {
        string cue = DiscWith(Mdb(Record(pmod: 0x0420), Record(pmod: 0x0400), Record(pmod: 0x0420, pixels: 16), Record()), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(new[] { (0, 1), (4, 2) }, result.LeftOut.Select(m => (m.Key, m.Value)));
        Assert.Equal(3, result.Skipped);
        Assert.Equal("e03.tex.e03b0n0.0003", Assert.Single(result.Files).Id);
        Assert.Contains("E03B0N0 1; no colours for modes 0 and 4: the disc has no EPISODE3.PRG; left out: mode 0 1, mode 4 2", result.Details());
    }

    [Fact]
    public void Run_FixedDisc_WritesE03B0N0AsTheBaselineBuildDid()
    {
        // Paths and SHA-256s recorded from the build at 364670722c, before the nine files: a change
        // to finding, numbering, IDs, paths or PNG bytes fails here. Number 0002 is mode 4, left out.
        string cue = DiscWith(Mdb(Record(), Record(lut: 64), Record(pmod: 0x0420), Record(pixels: 32, pmod: Mode1 | 0x40), Record()), Cgb());

        Extraction.Run(cue, _assets);

        string images = Path.Combine(_assets, "images");
        var written = Directory.GetFiles(images, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(_assets, f).Replace('\\', '/'), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f)))));
        Assert.Equal(new[]
        {
            ("images/e03/e03b0n0/0000.png", "d9c8f7e3a4503fcf3966943ab1bdc9dfcf796362131dff5f8fe99709554a4b8e"),
            ("images/e03/e03b0n0/0001.png", "1d48d8b557d91a51b7711fac2639e02e1c9d666791cc44405c2a4a848bc839f5"),
            ("images/e03/e03b0n0/0003.png", "bdcf009ba090d9060e6f784288f1b2d947a0082bd15612f658f43d9ea67ee5c4"),
        }, written);
    }

    [Fact]
    public void Run_OtherFilesOnTheDisc_LeaveE03B0N0sOutputUnchanged()
    {
        byte[] mdb = Mdb(Record(), Record(pixels: 16), Record(pixels: 32, pmod: Mode1 | 0x40));
        string alone = Path.Combine(_assets, "alone");
        Directory.CreateDirectory(alone);
        Extraction.Run(new SyntheticDisc().With("E03B0N0.MDB", mdb).With("E03B0N0.CGB", Cgb()).WriteCue(alone), alone);

        Extraction.Run(DiscWith(mdb, Cgb(), new SyntheticDisc()
            .With("E03CMN.MDB", Mdb(Record(pixels: 48), Record(pmod: Mode5)))
            .With("E03CMN.CGB", Cgb())), _assets);

        string aloneFolder = Path.Combine(alone, "images", "e03", "e03b0n0");
        string[] names = Directory.GetFiles(aloneFolder).Select(Path.GetFileName).OrderBy(n => n).ToArray()!;
        Assert.Equal(names, Directory.GetFiles(TextureFolder).Select(Path.GetFileName).OrderBy(n => n));
        Assert.All(names, n => Assert.Equal(File.ReadAllBytes(Path.Combine(aloneFolder, n)), File.ReadAllBytes(Path.Combine(TextureFolder, n))));
    }

    /// <summary>An <c>E03B0N0</c> of a mode 4, a mode 0 and a mode 1 texture, each in its own pixels of <see cref="CountingCgb"/>.</summary>
    private static byte[] ColourBankMdb() =>
        Mdb(Record(pmod: Mode4, colr: 0x1200), Record(pixels: 16, pmod: Mode0 | 0x40, colr: 0x0010), Record(pixels: 32));

    [Fact]
    public void Run_TwoTables_WritesModes4And0FromTheSecondUnderTheirNumbers()
    {
        string cue = DiscWith(ColourBankMdb(), CountingCgb(), new SyntheticDisc().With("EPISODE3.PRG", TwoTableProgram()));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(new[] { "0000.png", "0001.png", "0002.png" }, Directory.GetFiles(TextureFolder).Select(Path.GetFileName).OrderBy(n => n));
        Assert.Equal(0, result.Skipped);
        Assert.Equal(0x102, result.Colours!.Table.Offset);
        Assert.Null(result.ColourNotice);
        ColourRam second = ColourBanks.FromProgram(TwoTableProgram()).Colours!;
        var records = Textures.FindRecords(ColourBankMdb());
        Assert.Equal(PngWriter.Encode(8, 2, Textures.DecodeMode4(CountingCgb(), Base, records[0], second)), File.ReadAllBytes(Path.Combine(TextureFolder, "0000.png")));
        Assert.Equal(PngWriter.Encode(8, 2, Textures.DecodeMode0(CountingCgb(), Base, records[1], second)), File.ReadAllBytes(Path.Combine(TextureFolder, "0001.png")));
        Assert.Contains("E03B0N0 3; colour table at 0x102 of EPISODE3.PRG (table 2 of 2), 2 blocks", result.Details());
        Assert.DoesNotContain("left out", result.Details());
    }

    [Fact]
    public void Run_OneTable_ColoursFromIt()
    {
        byte[] prg = Program(0x1000, new[] { (0x400, 7) }, (0x40, new[] { (0x25F00000u, 0x06080400u), (0x25F00400u, 0x06080400u) }));
        string cue = DiscWith(ColourBankMdb(), CountingCgb(), new SyntheticDisc().With("EPISODE3.PRG", prg));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(0x40, result.Colours!.Table.Offset);
        Assert.Equal(1, result.Colours.TablesFound);
        TextureRecord r = Textures.FindRecords(ColourBankMdb())[0];
        Assert.Equal(PngWriter.Encode(8, 2, Textures.DecodeMode4(CountingCgb(), Base, r, ColourBanks.FromProgram(prg).Colours!)),
            File.ReadAllBytes(Path.Combine(TextureFolder, "0000.png")));
    }

    [Theory]
    [InlineData(false, "the disc has no EPISODE3.PRG")]
    [InlineData(true, "EPISODE3.PRG holds no colour upload table")]
    public void Run_NoTable_LeavesModes4And0OutWithANoticeAndWritesTheRest(bool withProgram, string notice)
    {
        var others = new SyntheticDisc().With("E03CMN.MDB", Mdb(Record(pmod: Mode5))).With("E03CMN.CGB", Cgb());
        if (withProgram)
        {
            others.With("EPISODE3.PRG", new byte[0x1000]);
        }

        ExtractionResult result = Extraction.Run(DiscWith(ColourBankMdb(), CountingCgb(), others), _assets);

        Assert.Null(result.Colours);
        Assert.Equal(notice, result.ColourNotice);
        Assert.Equal(new[] { "e03.tex.e03cmn.0000", "e03.tex.e03b0n0.0002" }, result.Files.Select(f => f.Id));
        Assert.Equal(new[] { (0, 1), (4, 1) }, result.LeftOut.Select(m => (m.Key, m.Value)));
        Assert.Contains($"no colours for modes 0 and 4: {notice}; left out: mode 0 1, mode 4 1", result.Details());
    }

    [Fact]
    public void Run_ProgramPastTheImagesEnd_IsNoticedAndModes1And5StillWritten()
    {
        // EPISODE3.PRG is the last file, so cutting the image short leaves its sectors past the end.
        byte[] prg = TwoTableProgram();
        var disc = new SyntheticDisc()
            .With("E03CMN.MDB", Mdb(Record(pmod: Mode5))).With("E03CMN.CGB", Cgb())
            .With("E03B0N0.MDB", ColourBankMdb()).With("E03B0N0.CGB", CountingCgb())
            .With("EPISODE3.PRG", prg);
        byte[] track = disc.Build(DiscImage.SectorSize);
        string iso = Path.Combine(_disc, "Game.iso");
        File.WriteAllBytes(iso, track.AsSpan(0, track.Length - prg.Length / 2).ToArray());

        ExtractionResult result = Extraction.Run(iso, _assets);

        Assert.Null(result.Colours);
        Assert.StartsWith("EPISODE3.PRG cannot be read (Game.iso: the image ends inside sector ", result.ColourNotice);
        Assert.Equal(new[] { "e03.tex.e03cmn.0000", "e03.tex.e03b0n0.0002" }, result.Files.Select(f => f.Id));
        Assert.Equal(new[] { (0, 1), (4, 1) }, result.LeftOut.Select(m => (m.Key, m.Value)));
    }

    [Fact]
    public void Run_BadPair_IsSkippedTheRestAppliedAndNotedInTheResult()
    {
        byte[] prg = Program(0x1000, new[] { (0x400, 1), (0x600, 2) }, (0x10, new[]
        {
            (0x25F00000u, 0x06080400u), (0x25F00200u, 0x06090000u), (0x25F00400u, 0x06080600u),
        }));
        string cue = DiscWith(ColourBankMdb(), CountingCgb(), new SyntheticDisc().With("EPISODE3.PRG", prg));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(0x18, Assert.Single(result.Colours!.Skipped).Offset);
        Assert.Equal(3, result.Files.Count);
        Assert.Equal(BlockColour(2, 1), result.Colours.Colour(0x201));
        Assert.Contains("2 blocks; skipped: the pair at 0x18 (destination 0x25F00200, source 0x06090000)", result.Details());
    }

    [Fact]
    public void Run_WithColourTables_LeavesModes1And5AsTheBaselineBuildWroteThem()
    {
        // The same disc as Run_FixedDisc_WritesE03B0N0AsTheBaselineBuildDid, plus an EPISODE3.PRG:
        // the mode 1 PNGs keep their bytes, and number 0002, mode 4, is now written as well.
        string cue = DiscWith(Mdb(Record(), Record(lut: 64), Record(pmod: 0x0420), Record(pixels: 32, pmod: Mode1 | 0x40), Record()), Cgb(),
            new SyntheticDisc().With("EPISODE3.PRG", TwoTableProgram()));

        Extraction.Run(cue, _assets);

        var written = Directory.GetFiles(TextureFolder).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))))).ToArray();
        Assert.Equal(new[] { "0000.png", "0001.png", "0002.png", "0003.png" }, written.Select(w => w.Item1));
        Assert.Equal(new[]
        {
            ("0000.png", "d9c8f7e3a4503fcf3966943ab1bdc9dfcf796362131dff5f8fe99709554a4b8e"),
            ("0001.png", "1d48d8b557d91a51b7711fac2639e02e1c9d666791cc44405c2a4a848bc839f5"),
            ("0003.png", "bdcf009ba090d9060e6f784288f1b2d947a0082bd15612f658f43d9ea67ee5c4"),
        }, written.Where(w => w.Item1 != "0002.png"));
    }

    [Fact]
    public void Run_Mode5WithColourTables_IsWrittenAsWithout()
    {
        byte[] mdb = Mdb(Record(pmod: Mode5), Record(pixels: 32));
        string alone = Path.Combine(_assets, "alone");
        Directory.CreateDirectory(alone);
        Extraction.Run(new SyntheticDisc().With("E03B0N0.MDB", mdb).With("E03B0N0.CGB", Cgb()).WriteCue(alone), alone);

        Extraction.Run(DiscWith(mdb, Cgb(), new SyntheticDisc().With("EPISODE3.PRG", TwoTableProgram())), _assets);

        string aloneFolder = Path.Combine(alone, "images", "e03", "e03b0n0");
        Assert.All(new[] { "0000.png", "0001.png" }, n =>
            Assert.Equal(File.ReadAllBytes(Path.Combine(aloneFolder, n)), File.ReadAllBytes(Path.Combine(TextureFolder, n))));
    }

    [Fact]
    public void DiscFolder_NoDisc_FindsNothing()
    {
        Assert.Equal(DiscFolderState.None, DiscFolder.Find(_assets).State);

        Directory.Delete(_disc);
        Assert.Equal(DiscFolderState.None, DiscFolder.Find(_assets).State);
    }

    [Fact]
    public void DiscFolder_OneCue_IsFound()
    {
        string cue = DiscWith(Mdb(Record()), Cgb());

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.One, search.State);
        Assert.Equal(cue, search.ImagePath);
    }

    [Fact]
    public void DiscFolder_TwoCues_AreNamedAndNotPicked()
    {
        File.WriteAllText(Path.Combine(_disc, "b.cue"), "");
        File.WriteAllText(Path.Combine(_disc, "a.CUE"), "");

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.Several, search.State);
        Assert.Null(search.ImagePath);
        Assert.Equal(new[] { "a.CUE", "b.cue" }, search.Names);
    }

    /// <summary>The disc every layout test writes in its own way: two of the nine files, one with a mode 5 texture.</summary>
    private static SyntheticDisc LayoutDisc() =>
        new SyntheticDisc { Product = "MK-81022", Version = "V1.000" }
            .With("E03B0N0.MDB", Mdb(Record(), Record(pixels: 16), Record(pixels: 32, pmod: Mode1 | 0x40)))
            .With("E03B0N0.CGB", Cgb())
            .With("E03CMN.MDB", Mdb(Record(pixels: 48), Record(pmod: Mode5)))
            .With("E03CMN.CGB", Cgb());

    /// <summary>Every file under <paramref name="folder"/>, by path relative to it, with its bytes.</summary>
    private static (string Path, byte[] Bytes)[] Written(string folder) =>
        Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetRelativePath(folder, f).Replace('\\', '/'), File.ReadAllBytes(f)))
            .ToArray();

    /// <summary>Runs extraction on the image <paramref name="write"/> makes in a folder of its own; returns the result and what it wrote.</summary>
    private (ExtractionResult Result, (string Path, byte[] Bytes)[] Files) RunOn(string name, Func<string, string> write)
    {
        string folder = Path.Combine(_assets, name);
        string output = Path.Combine(folder, "out");
        Directory.CreateDirectory(folder);
        ExtractionResult result = Extraction.Run(write(folder), output);
        return (result, Written(output));
    }

    [Fact]
    public void Run_SingleFileCue_WritesTheSameFilesAndHeaderAsPerTrack()
    {
        var perTrack = RunOn("per-track", f => LayoutDisc().WriteCue(f));
        var single = RunOn("single", f => LayoutDisc().WriteSingleFileCue(f));

        Assert.Equal(perTrack.Result.Source, single.Result.Source);
        Assert.Equal(perTrack.Files.Select(f => f.Path), single.Files.Select(f => f.Path));
        Assert.Equal(perTrack.Files.Select(f => f.Bytes), single.Files.Select(f => f.Bytes));
        Assert.NotEmpty(single.Files);
    }

    [Fact]
    public void Run_Mode1_2048Cue_WritesTheSameFilesAndHeaderAsPerTrack()
    {
        var perTrack = RunOn("per-track", f => LayoutDisc().WriteCue(f));
        var cooked = RunOn("cooked", f => LayoutDisc().WriteCue(f, "Game.bin", mode: CueSheet.CookedMode));

        Assert.Equal(perTrack.Result.Source, cooked.Result.Source);
        Assert.Equal(perTrack.Files.Select(f => f.Path), cooked.Files.Select(f => f.Path));
        Assert.Equal(perTrack.Files.Select(f => f.Bytes), cooked.Files.Select(f => f.Bytes));
    }

    [Fact]
    public void Run_PlainIso_WritesTheSameFilesAndHeaderAsPerTrack()
    {
        var perTrack = RunOn("per-track", f => LayoutDisc().WriteCue(f));
        var iso = RunOn("iso", f => LayoutDisc().WriteIso(f));

        Assert.Equal("MK-81022", iso.Result.Source.Product);
        Assert.Equal("V1.000", iso.Result.Source.Version);
        Assert.Equal(perTrack.Result.Source, iso.Result.Source);
        Assert.Equal(perTrack.Files.Select(f => f.Path), iso.Files.Select(f => f.Path));
        Assert.Equal(perTrack.Files.Select(f => f.Bytes), iso.Files.Select(f => f.Bytes));
        // A disc lacking files is attempted, and the missing ones are named.
        Assert.Contains(iso.Result.Missing, m => m.Name == "E03END");
    }

    [Fact]
    public void Run_IsoWithNoIsoVolume_FailsNamingItAndWritesNothing()
    {
        string iso = Path.Combine(_disc, "Game.iso");
        File.WriteAllBytes(iso, new byte[40 * DiscImage.SectorSize]);

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(iso, _assets));

        Assert.Equal("Game.iso: no ISO9660 volume at sector 16", e.Message);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
    }

    [Fact]
    public void Run_CueWithAudioTrack1_FailsNamingTheModeAndWritesNothing()
    {
        File.WriteAllBytes(Path.Combine(_disc, "Game (Track 1).bin"), LayoutDisc().Build());
        string cue = Path.Combine(_disc, "Game.cue");
        File.WriteAllText(cue, SyntheticDisc.Cue("Game (Track 1).bin", "AUDIO"));

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(cue, _assets));

        Assert.Contains("track 1 is AUDIO", e.Message);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
    }

    [Fact]
    public void DiscFolder_OneIso_IsFound()
    {
        string iso = LayoutDisc().WriteIso(_disc, "Game.ISO");

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.One, search.State);
        Assert.Equal(iso, search.ImagePath);
    }

    [Fact]
    public void DiscFolder_CueWithItsBins_IsOneImage()
    {
        string cue = LayoutDisc().WriteSingleFileCue(_disc);
        File.WriteAllBytes(Path.Combine(_disc, "Game (Track 2).bin"), new byte[16]);

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.One, search.State);
        Assert.Equal(cue, search.ImagePath);
    }

    [Fact]
    public void DiscFolder_IsoAndCue_AreNamedAndNotPicked()
    {
        LayoutDisc().WriteCue(_disc);
        LayoutDisc().WriteIso(_disc);

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.Several, search.State);
        Assert.Null(search.ImagePath);
        Assert.Equal(new[] { "Game.cue", "Game.iso" }, search.Names);
    }

    [Fact]
    public void TextureId_AndPath_FollowTheAssetContract()
    {
        Assert.Equal("e03.tex.e03b0n0.0007", Extraction.TextureId("E03B0N0", 7));
        Assert.Equal("images/e03/e03b0n0/0007.png", Extraction.TexturePath("E03B0N0", 7));
        Assert.Equal("e03.tex.e03cmn.1234", Extraction.TextureId("E03CMN", 1234));
    }
}
