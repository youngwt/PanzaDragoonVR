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
/// nine Episode 3 files, mode 5, a file missing, none present and a file with no textures.
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
        Assert.Contains("E03B0N0 1; left out, in colour modes not decoded yet: mode 0 1, mode 4 2", result.Details());
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
        Assert.Equal(cue, search.CuePath);
    }

    [Fact]
    public void DiscFolder_TwoCues_AreNamedAndNotPicked()
    {
        File.WriteAllText(Path.Combine(_disc, "b.cue"), "");
        File.WriteAllText(Path.Combine(_disc, "a.CUE"), "");

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.Several, search.State);
        Assert.Null(search.CuePath);
        Assert.Equal(new[] { "a.CUE", "b.cue" }, search.Names);
    }

    [Fact]
    public void TextureId_AndPath_FollowTheAssetContract()
    {
        Assert.Equal("e03.tex.e03b0n0.0007", Extraction.TextureId("E03B0N0", 7));
        Assert.Equal("images/e03/e03b0n0/0007.png", Extraction.TexturePath("E03B0N0", 7));
        Assert.Equal("e03.tex.e03cmn.1234", Extraction.TextureId("E03CMN", 1234));
    }
}
