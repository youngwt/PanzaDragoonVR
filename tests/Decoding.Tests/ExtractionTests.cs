using System;
using System.IO;
using System.Linq;
using Xunit;
using static PdzDecoding.Tests.SyntheticTextures;

namespace PdzDecoding.Tests;

/// <summary>
/// The extraction entry point on whole disc images built here, one or more tests per row of the
/// story's matrix: disc present, no disc, bad image, a repeated record and transparency.
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

    private string DiscWith(byte[] mdb, byte[] cgb) =>
        new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }
            .With("E03B0N0.MDB", mdb)
            .With("E03B0N0.CGB", cgb)
            .With("E03CMN.MDB", new byte[8])
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
    public void Run_DiscWithoutTheTextureFile_WritesNothing()
    {
        string cue = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).WriteCue(_disc);

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(cue, _assets));

        Assert.Equal("the disc has no E03B0N0.CGB", e.Message);
        Assert.False(Directory.Exists(Path.Combine(_assets, "images")));
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
