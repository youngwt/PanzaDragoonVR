using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;
using static PdzDecoding.Tests.SyntheticTextures;
using static PdzDecoding.Tests.SyntheticPlanes;

namespace PdzDecoding.Tests;

/// <summary>
/// The extraction entry point on whole disc images built here, one or more tests per row of the
/// stories' matrices: disc present, no disc, bad image, a repeated record and transparency; then the
/// nine Episode 3 files, mode 5, a file missing, none present and a file with no textures; then the
/// colour-bank modes from <c>EPISODE3.PRG</c>'s tables: two, one, none, a bad pair, and modes 1 and 5
/// unchanged; then the manifest: recorded bytes, every PNG listed, an old manifest replaced and none
/// when nothing is written; then each disc image layout: per-track and single-file cue sheets, MODE1/2048 tracks and plain ISOs;
/// then staging and progress: no staging folder left, progress by disc file, the previous extraction kept while writing and
/// replaced after, a failure part way, a leftover staging folder, and the disc file named in a disc file's error.
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
        Assert.False(File.Exists(Path.Combine(_assets, Manifest.FileName)));
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

    /// <summary>The disc of <see cref="Run_FixedDisc_WritesE03B0N0AsTheBaselineBuildDid"/>: three mode 1 textures and number 0002 in mode 4, left out.</summary>
    private string FixedDisc() =>
        DiscWith(Mdb(Record(), Record(lut: 64), Record(pmod: 0x0420), Record(pixels: 32, pmod: Mode1 | 0x40), Record()), Cgb());

    private string ManifestFile => Path.Combine(_assets, Manifest.FileName);

    [Fact]
    public void Run_FixedDisc_WritesTheManifestAsRecorded()
    {
        // expected/fixed-disc-manifest.json was written by Python's json.dumps(indent=2) from the
        // baseline PNGs' sizes and SHA-256s; tests/tools/test_manifest.py checks it is tools/manifest.py's text.
        ExtractionResult result = Extraction.Run(FixedDisc(), _assets);

        Assert.Equal(Path.GetFullPath(ManifestFile), result.ManifestPath);
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "fixed-disc-manifest.json")), File.ReadAllBytes(ManifestFile));
    }

    [Fact]
    public void Run_Manifest_ListsEveryPngWrittenSortedByIdWithItsSizeAndChecksum()
    {
        var disc = new SyntheticDisc();
        foreach (string name in Extraction.TextureFiles)
        {
            disc.With(name + ".MDB", Mdb(Record(), Record(pixels: 16))).With(name + ".CGB", Cgb());
        }

        ExtractionResult result = Extraction.Run(disc.WriteCue(_disc), _assets);

        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(ManifestFile));
        var root = manifest.RootElement;
        Assert.Equal(1, root.GetProperty("format").GetInt32());
        Assert.Equal("pdz-extract " + Extraction.Version, root.GetProperty("generator").GetString());
        Assert.Equal("T-00000", root.GetProperty("source").GetProperty("product").GetString());
        Assert.Equal("V9.999", root.GetProperty("source").GetProperty("version").GetString());
        var assets = root.GetProperty("assets").EnumerateArray().ToArray();
        Assert.Equal(result.Files.Select(f => f.Id).OrderBy(id => id, StringComparer.Ordinal), assets.Select(a => a.GetProperty("id").GetString()));
        Assert.Equal("e03.tex.e03b0n0.0000", assets[0].GetProperty("id").GetString());
        Assert.All(assets, a =>
        {
            Assert.Equal(new[] { "id", "kind", "path", "bytes", "sha256" }, a.EnumerateObject().Select(p => p.Name));
            Assert.Equal("image", a.GetProperty("kind").GetString());
            byte[] png = File.ReadAllBytes(Path.Combine(_assets, a.GetProperty("path").GetString()!));
            Assert.Equal(png.Length, a.GetProperty("bytes").GetInt32());
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(png)), a.GetProperty("sha256").GetString());
        });
    }

    [Fact]
    public void Run_OldManifest_IsReplaced_AndFilesNotWrittenStayUnlisted()
    {
        File.WriteAllText(ManifestFile, "{ \"format\": 1, \"assets\": [ \"an old manifest\" ] }\n");
        string old = Path.Combine(_assets, "images", "e03", "e03b0n0", "0099.png");
        Directory.CreateDirectory(Path.GetDirectoryName(old)!);
        File.WriteAllBytes(old, new byte[] { 1, 2, 3 });

        Extraction.Run(FixedDisc(), _assets);

        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "fixed-disc-manifest.json")), File.ReadAllBytes(ManifestFile));
        Assert.True(File.Exists(old));
        Assert.DoesNotContain("0099", File.ReadAllText(ManifestFile));
    }

    [Fact]
    public void Run_NothingWritten_WritesNoManifest_AndLeavesAnOldOneAlone()
    {
        string cue = DiscWith(Mdb(Record(pmod: 0x0420)), Cgb());

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Null(result.ManifestPath);
        Assert.False(File.Exists(ManifestFile));

        File.WriteAllText(ManifestFile, "old\n");
        Extraction.Run(cue, _assets);
        Assert.Equal("old\n", File.ReadAllText(ManifestFile));
    }

    [Fact]
    public void Run_Manifest_IsWrittenAfterEveryPng()
    {
        Extraction.Run(FixedDisc(), _assets);

        DateTime manifest = File.GetLastWriteTimeUtc(ManifestFile);
        Assert.All(Directory.GetFiles(TextureFolder), png => Assert.True(File.GetLastWriteTimeUtc(png) <= manifest, png));
    }

    [Fact]
    public void Manifest_Write_IsJsonDumpsText_ForAHeaderWithCharactersToEscape()
    {
        // From Python: json.dumps({... "source": {"product": 'A"B\\C', "version": "\x01\tV\u00e9\x7f"}, ...}, indent=2, ensure_ascii=False) + "\n"
        var header = new SaturnHeader("SEGA SEGASATURN", "SEGA", "A\"B\\C", "\u0001\tV\u00e9\u007f", "", "");
        byte[] png = { 1, 2, 3 };

        byte[] written = Manifest.Write(header, new[] { new ManifestEntry("e03.b", "images/b.png", png), new ManifestEntry("e03.a", "images/a.png", png) });

        string sha = "039058c6f2c0cb492c533b0a4d14ef77cc0f78abccced5287d84a1a2011cfb81";
        string expected = "{\n  \"format\": 1,\n  \"generator\": \"pdz-extract 0.3\",\n  \"source\": {\n" +
            "    \"product\": \"A\\\"B\\\\C\",\n    \"version\": \"\\u0001\\tV\u00e9\u007f\"\n  },\n  \"assets\": [\n" +
            "    {\n      \"id\": \"e03.a\",\n      \"kind\": \"image\",\n      \"path\": \"images/a.png\",\n      \"bytes\": 3,\n      \"sha256\": \"" + sha + "\"\n    },\n" +
            "    {\n      \"id\": \"e03.b\",\n      \"kind\": \"image\",\n      \"path\": \"images/b.png\",\n      \"bytes\": 3,\n      \"sha256\": \"" + sha + "\"\n    }\n" +
            "  ]\n}\n";
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(expected), written);
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
    public void Run_TableWithEveryPairSkipped_LeavesModes4And0OutAsWithNoTable()
    {
        byte[] prg = Program(0x1000, new[] { (0x400, 1) }, (0x10, new[] { (0x25F00000u, 0x06090000u) }));
        var others = new SyntheticDisc().With("E03CMN.MDB", Mdb(Record(pmod: Mode5))).With("E03CMN.CGB", Cgb())
            .With("EPISODE3.PRG", prg);

        ExtractionResult result = Extraction.Run(DiscWith(ColourBankMdb(), CountingCgb(), others), _assets);

        Assert.Null(result.Colours);
        Assert.Equal("EPISODE3.PRG's colour table at 0x10 has no block inside the file and colour RAM", result.ColourNotice);
        Assert.Equal(new[] { "e03.tex.e03cmn.0000", "e03.tex.e03b0n0.0002" }, result.Files.Select(f => f.Id));
        Assert.Equal(new[] { (0, 1), (4, 1) }, result.LeftOut.Select(m => (m.Key, m.Value)));
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

    [Fact]
    public void ImageFiles_PerTrackCue_IsTheCueAndEveryBinItNames()
    {
        File.WriteAllBytes(Path.Combine(_disc, "Game (Track 1).bin"), new byte[16]);
        File.WriteAllBytes(Path.Combine(_disc, "Game (Track 2).bin"), new byte[16]);
        File.WriteAllBytes(Path.Combine(_disc, "other.bin"), new byte[16]);
        string cue = Path.Combine(_disc, "Game.cue");
        File.WriteAllText(cue, SyntheticDisc.Cue("Game (Track 1).bin"));

        Assert.Equal(new[] { cue, Path.Combine(_disc, "Game (Track 1).bin"), Path.Combine(_disc, "Game (Track 2).bin") },
            DiscFolder.ImageFiles(cue));
    }

    [Fact]
    public void ImageFiles_SingleFileCue_NamesItsBinOnce()
    {
        string cue = new SyntheticDisc().WriteSingleFileCue(_disc);

        Assert.Equal(new[] { cue, Path.Combine(_disc, "Game.bin") }, DiscFolder.ImageFiles(cue));
    }

    [Fact]
    public void ImageFiles_Iso_IsTheIsoAlone()
    {
        string iso = new SyntheticDisc().WriteIso(_disc);
        File.WriteAllText(Path.Combine(_disc, "readme.txt"), "");

        Assert.Equal(new[] { iso }, DiscFolder.ImageFiles(iso));
    }

    [Fact]
    public void ImageFiles_CueNamingFilesOutsideItsFolderOrMissing_LeavesThemOut()
    {
        File.WriteAllText(Path.Combine(_assets, "manifest.json"), "{}");
        string cue = Path.Combine(_disc, "Game.cue");
        File.WriteAllText(cue, "FILE \"../manifest.json\" BINARY\nFILE \"gone.bin\" BINARY\nFILE \"Game.cue\" BINARY\n");

        Assert.Equal(new[] { cue }, DiscFolder.ImageFiles(cue));
    }

    [Fact]
    public void ImageFiles_CueNamingFilesInASubfolder_KeepsThem()
    {
        Directory.CreateDirectory(Path.Combine(_disc, "tracks"));
        File.WriteAllBytes(Path.Combine(_disc, "tracks", "x.bin"), new byte[16]);
        File.WriteAllBytes(Path.Combine(_disc, "..x.bin"), new byte[16]);
        string cue = Path.Combine(_disc, "Game.cue");
        File.WriteAllText(cue, "FILE \"tracks/x.bin\" BINARY\nFILE \"..x.bin\" BINARY\n");

        Assert.Equal(new[] { cue, Path.Combine(_disc, "tracks", "x.bin"), Path.Combine(_disc, "..x.bin") }, DiscFolder.ImageFiles(cue));
    }

    [Fact]
    public void ReadHeader_ReadsSectorZeroWithoutTheFileSystem()
    {
        var header = new byte[20 * DiscImage.SectorSize];
        System.Text.Encoding.ASCII.GetBytes("SEGA SEGASATURN SEGA TP T-000   GS-9049   V1.001").CopyTo(header, 0);
        string iso = Path.Combine(_disc, "Game.iso");
        File.WriteAllBytes(iso, header);

        Assert.Throws<DecodingException>(() => DiscImage.OpenImage(iso));
        Assert.Equal(("GS-9049", "V1.001"), (DiscImage.ReadHeader(iso).Product, DiscImage.ReadHeader(iso).Version));
    }

    [Fact]
    public void ReadHeader_Cue_ReadsTrack1sSectorZero()
    {
        string cue = new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.WriteCue(_disc);

        Assert.Equal("GS-9049", DiscImage.ReadHeader(cue).Product);
    }

    [Fact]
    public void ReadHeader_EmptyIso_Throws()
    {
        string iso = Path.Combine(_disc, "Game.iso");
        File.WriteAllBytes(iso, Array.Empty<byte>());

        Assert.Throws<DecodingException>(() => DiscImage.ReadHeader(iso));
    }

    [Fact]
    public void ImageFiles_NotADiscImage_Throws()
    {
        Assert.Throws<DecodingException>(() => DiscFolder.ImageFiles(Path.Combine(_disc, "Game.bin")));
    }

    [Fact]
    public void CueSheet_Files_ListsEveryFileLineInFull()
    {
        string cue = Path.Combine(_disc, "Game.cue");

        Assert.Equal(new[] { Path.Combine(_disc, "Game (Track 1).bin"), Path.Combine(_disc, "Game (Track 2).bin") },
            CueSheet.Files(SyntheticDisc.Cue("Game (Track 1).bin"), cue));
    }

    [Fact]
    public void ReadHeader_GivesProductAndVersion()
    {
        string iso = new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.WriteIso(_disc);

        SaturnHeader header = DiscImage.ReadHeader(iso);

        Assert.Equal(("GS-9049", "V1.001"), (header.Product, header.Version));
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
    public void DiscFolder_CueNamingTheIso_IsOneImageTheCue()
    {
        string cue = LayoutDisc().WriteCue(_disc, "Game.iso", "Game.cue", CueSheet.CookedMode);

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.One, search.State);
        Assert.Equal(cue, search.ImagePath);
        Assert.Equal(new[] { "Game.cue" }, search.Names);
        Assert.Equal(new[] { cue, Path.Combine(_disc, "Game.iso") }, DiscFolder.ImageFiles(cue));
    }

    [Fact]
    public void DiscFolder_CueNamingTheIsoInAnotherCase_IsOneImageTheCue()
    {
        LayoutDisc().WriteIso(_disc, "Game.ISO");
        string cue = Path.Combine(_disc, "Game.cue");
        File.WriteAllText(cue, SyntheticDisc.Cue("Game.iso", CueSheet.CookedMode));

        DiscSearch search = DiscFolder.Find(_assets);

        // The name is matched without regard to case on every file system, as the Quest's storage reads it.
        Assert.Equal(DiscFolderState.One, search.State);
        Assert.Equal(cue, search.ImagePath);
        Assert.Equal(new[] { "Game.cue" }, search.Names);
    }

    [Fact]
    public void DiscFolder_CueAndAnUnrelatedIso_AreNamedAndNotPicked()
    {
        LayoutDisc().WriteCue(_disc, "a.bin", "a.cue");
        LayoutDisc().WriteIso(_disc, "b.iso");

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.Several, search.State);
        Assert.Null(search.ImagePath);
        Assert.Equal(new[] { "a.cue", "b.iso" }, search.Names);
    }

    [Fact]
    public void DiscFolder_BrokenCueBesideTheIsoItNames_AreNamedAndNotPicked()
    {
        // The sheet names b.iso, then a file name no path can hold, so reading it throws and it names nothing.
        string text = "FILE \"b.iso\" BINARY\n  TRACK 01 MODE1/2048\n    INDEX 01 00:00:00\nFILE \"x\0y.bin\" BINARY\n";
        File.WriteAllText(Path.Combine(_disc, "a.cue"), text);
        Assert.ThrowsAny<ArgumentException>(() => CueSheet.ReadFiles(Path.Combine(_disc, "a.cue")));
        LayoutDisc().WriteIso(_disc, "b.iso");

        DiscSearch search = DiscFolder.Find(_assets);

        Assert.Equal(DiscFolderState.Several, search.State);
        Assert.Equal(new[] { "a.cue", "b.iso" }, search.Names);
    }

    [Fact]
    public void DiscFolder_UnreadableCueBesideTheIsoItWouldName_AreNamedAndNotPicked()
    {
        LayoutDisc().WriteCue(_disc, "Game.iso", "Game.cue", CueSheet.CookedMode);
        string cue = Path.Combine(_disc, "Game.cue");
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(cue, UnixFileMode.None);
        try
        {
            if (CanRead(cue))
            {
                // Run with rights that read any file: the sheet cannot be made unreadable.
                return;
            }

            DiscSearch search = DiscFolder.Find(_assets);

            Assert.Equal(DiscFolderState.Several, search.State);
            Assert.Equal(new[] { "Game.cue", "Game.iso" }, search.Names);
        }
        finally
        {
            File.SetUnixFileMode(cue, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private static bool CanRead(string file)
    {
        try
        {
            File.ReadAllBytes(file);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Fact]
    public void Run_CueNamingAnIso_WritesTheSameFilesAndHeaderAsTheIso()
    {
        var iso = RunOn("iso", f => LayoutDisc().WriteIso(f));
        var cue = RunOn("cue", f => LayoutDisc().WriteCue(f, "Game.iso", "Game.cue", CueSheet.CookedMode));

        Assert.Equal(iso.Result.Source, cue.Result.Source);
        Assert.Equal(iso.Files.Select(f => f.Path), cue.Files.Select(f => f.Path));
        Assert.Equal(iso.Files.Select(f => f.Bytes), cue.Files.Select(f => f.Bytes));
        Assert.NotEmpty(cue.Files);
    }

    // ------------------------------------------------------------ staging and progress

    private string Staging => Path.Combine(_assets, Extraction.StagingFolder);

    /// <summary>An earlier extraction's manifest and a PNG at a path the new run writes too.</summary>
    private (string Manifest, string Png) PreviousExtraction()
    {
        File.WriteAllText(ManifestFile, "{ \"format\": 1, \"assets\": [ \"the previous manifest\" ] }\n");
        string png = Path.Combine(TextureFolder, "0000.png");
        Directory.CreateDirectory(TextureFolder);
        File.WriteAllBytes(png, new byte[] { 1, 2, 3 });
        return (File.ReadAllText(ManifestFile), png);
    }

    [Fact]
    public void Run_DiscPresent_PutsEverythingInPlaceAndLeavesNoStagingFolder()
    {
        ExtractionResult result = Extraction.Run(FixedDisc(), _assets);

        Assert.False(Directory.Exists(Staging));
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "fixed-disc-manifest.json")), File.ReadAllBytes(ManifestFile));
        Assert.All(result.Files, f => Assert.True(File.Exists(Path.Combine(_assets, f.Path)), f.Path));
    }

    [Fact]
    public void Run_DiscPresent_ReportsEachDiscFileReadThenWrittenThenTheMove()
    {
        var steps = new System.Collections.Generic.List<ExtractionProgress>();
        string cue = new SyntheticDisc()
            .With("E03CMN.MDB", Mdb(Record())).With("E03CMN.CGB", Cgb())
            .With("E03B0N0.MDB", Mdb(Record(pmod: 0x0420))).With("E03B0N0.CGB", Cgb())
            .With("E03END.MDB", Mdb(Record())).With("E03END.CGB", Cgb())
            .WriteCue(_disc);

        Extraction.Run(cue, _assets, steps.Add);

        var expected = Extraction.TextureFiles.Select((name, i) => new ExtractionProgress(ExtractionStage.Reading, name, i + 1, 9)).ToList();
        // E03B0N0 has only a mode 4 texture and no colours, so it has nothing to write.
        expected.Add(new ExtractionProgress(ExtractionStage.Writing, "E03CMN", 1, 9));
        expected.Add(new ExtractionProgress(ExtractionStage.Writing, "E03END", 9, 9));
        expected.Add(new ExtractionProgress(ExtractionStage.MovingIntoPlace, null, 9, 9));
        Assert.Equal(expected, steps);
    }

    [Fact]
    public void Run_WhileWriting_TheOutputFolderStillHoldsThePreviousExtraction()
    {
        (string manifest, string png) = PreviousExtraction();
        var seen = new System.Collections.Generic.List<(string, byte[])>();

        Extraction.Run(FixedDisc(), _assets, p =>
        {
            if (p.Stage != ExtractionStage.Reading)
            {
                seen.Add((File.ReadAllText(ManifestFile), File.ReadAllBytes(png)));
            }
        });

        Assert.Equal(2, seen.Count);
        Assert.All(seen, s =>
        {
            Assert.Equal(manifest, s.Item1);
            Assert.Equal(new byte[] { 1, 2, 3 }, s.Item2);
        });
        // Then replaced, now that the new extraction is fully written.
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "fixed-disc-manifest.json")), File.ReadAllBytes(ManifestFile));
        Assert.NotEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
    }

    [Fact]
    public void Run_FailurePartWay_MovesNothingAndKeepsThePreviousContents()
    {
        (string manifest, string png) = PreviousExtraction();
        string cue = DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc().With("E03CMN.MDB", Mdb(Record())).With("E03CMN.CGB", Cgb()));
        string[] stagedBefore = Array.Empty<string>();

        // E03CMN is written into staging; the failure comes as E03B0N0 is about to be.
        var e = Assert.Throws<IOException>(() => Extraction.Run(cue, _assets, p =>
        {
            if (p is { Stage: ExtractionStage.Writing, DiscFile: "E03B0N0" })
            {
                stagedBefore = Directory.GetFiles(Staging, "*", SearchOption.AllDirectories);
                throw new IOException("No space left on device");
            }
        }));

        Assert.Equal("No space left on device", e.Message);
        Assert.Equal(new[] { Path.Combine(Staging, "images", "e03", "e03cmn", "0000.png") }, stagedBefore);
        Assert.Equal(manifest, File.ReadAllText(ManifestFile));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
        Assert.False(Directory.Exists(Path.Combine(_assets, "images", "e03", "e03cmn")));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public void Run_FailureMovingIntoPlace_IsBeforeAnythingMoves()
    {
        (string manifest, string png) = PreviousExtraction();

        Assert.Throws<IOException>(() => Extraction.Run(FixedDisc(), _assets, p =>
        {
            if (p.Stage == ExtractionStage.MovingIntoPlace)
            {
                Assert.True(File.Exists(Path.Combine(Staging, Manifest.FileName)));
                throw new IOException("stopped");
            }
        }));

        Assert.Equal(manifest, File.ReadAllText(ManifestFile));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public void Run_MoveBlockedPartWay_LeavesThePreviousManifest()
    {
        (string manifest, string png) = PreviousExtraction();
        // A folder where the second PNG goes: the first moves into place, the second cannot.
        Directory.CreateDirectory(Path.Combine(TextureFolder, "0001.png"));
        ExtractionStage? last = null;

        Assert.ThrowsAny<IOException>(() => Extraction.Run(FixedDisc(), _assets, p => last = p.Stage));

        Assert.Equal(ExtractionStage.MovingIntoPlace, last);
        Assert.NotEqual(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
        Assert.Equal(manifest, File.ReadAllText(ManifestFile));
        Assert.False(File.Exists(Path.Combine(TextureFolder, "0003.png")));
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public void Run_LeftoverStaging_IsEmptiedThenUsed()
    {
        string leftover = Path.Combine(Staging, "images", "e03", "e03b0n0", "0099.png");
        Directory.CreateDirectory(Path.GetDirectoryName(leftover)!);
        File.WriteAllBytes(leftover, new byte[] { 9 });
        File.WriteAllText(Path.Combine(Staging, Manifest.FileName), "half a manifest");

        Extraction.Run(FixedDisc(), _assets);

        Assert.False(Directory.Exists(Staging));
        Assert.False(File.Exists(Path.Combine(TextureFolder, "0099.png")));
        Assert.Equal(new[] { "0000.png", "0001.png", "0003.png" }, Directory.GetFiles(TextureFolder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "fixed-disc-manifest.json")), File.ReadAllBytes(ManifestFile));
    }

    [Fact]
    public void Run_DiscFileCutShort_NamesTheDiscFileAndKeepsThePreviousContents()
    {
        (string manifest, string png) = PreviousExtraction();
        // E03B0N0.CGB is the last file, so cutting the image short leaves part of it past the end.
        byte[] track = new SyntheticDisc()
            .With("E03CMN.MDB", Mdb(Record())).With("E03CMN.CGB", Cgb())
            .With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb())
            .Build(DiscImage.SectorSize);
        string iso = Path.Combine(_disc, "Game.iso");
        File.WriteAllBytes(iso, track.AsSpan(0, track.Length - DiscImage.SectorSize / 2).ToArray());

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(iso, _assets));

        Assert.Equal("E03B0N0", e.DiscFile);
        Assert.StartsWith("Game.iso: the image ends inside sector ", e.Message);
        Assert.Equal(manifest, File.ReadAllText(ManifestFile));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
    }

    [Fact]
    public void Run_ImageWithNoFileSystem_NamesNoDiscFileAndKeepsThePreviousContents()
    {
        (string manifest, string png) = PreviousExtraction();
        string iso = Path.Combine(_disc, "Game.iso");
        File.WriteAllBytes(iso, new byte[40 * DiscImage.SectorSize]);

        var e = Assert.Throws<DecodingException>(() => Extraction.Run(iso, _assets));

        Assert.Null(e.DiscFile);
        Assert.Equal(manifest, File.ReadAllText(ManifestFile));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(png));
    }

    // ------------------------------------------------------------ the floor and canopy

    private string FloorFile => Path.Combine(_assets, Extraction.FloorPath);

    private string CanopyFile => Path.Combine(_assets, Extraction.CanopyPath);

    [Fact]
    public void Run_ProgramWithThePlanes_WritesTheFloorAndCanopyBesideTheTextures()
    {
        string cue = DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc().With("EPISODE3.PRG", PlaneProgram()));

        ExtractionResult result = Extraction.Run(cue, _assets);

        PlaneImages planes = Planes.FromProgram(PlaneProgram()).Images!;
        Assert.Equal(PngWriter.Encode(512, 512, planes.Floor), File.ReadAllBytes(FloorFile));
        Assert.Equal(PngWriter.Encode(512, 512, planes.Canopy), File.ReadAllBytes(CanopyFile));
        Assert.Equal(new[] { new ExtractedFile("e03.floor", "images/e03/floor.png", 512, 512), new ExtractedFile("e03.canopy", "images/e03/canopy.png", 512, 512) },
            result.PlaneFiles);
        Assert.Equal(new[] { "e03.tex.e03b0n0.0000" }, result.Files.Select(f => f.Id));
        Assert.Null(result.PlaneNotice);
        Assert.Equal(0x600, result.PlaneSources!.Cells);
        Assert.Contains("; floor and canopy from EPISODE3.PRG: cells at 0x600, maps at ", result.Details());
        Assert.False(Directory.Exists(Staging));
    }

    [Fact]
    public void Run_ProgramWithThePlanes_ListsThemInTheManifestSortedById()
    {
        string cue = DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc().With("EPISODE3.PRG", PlaneProgram()));

        Extraction.Run(cue, _assets);

        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(ManifestFile));
        var assets = manifest.RootElement.GetProperty("assets").EnumerateArray().ToArray();
        Assert.Equal(new[] { "e03.canopy", "e03.floor", "e03.tex.e03b0n0.0000" }, assets.Select(a => a.GetProperty("id").GetString()));
        Assert.Equal("images/e03/floor.png", assets[1].GetProperty("path").GetString());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(FloorFile))), assets[1].GetProperty("sha256").GetString());
    }

    [Fact]
    public void Run_ProgramWithThePlanes_AddsNoProgressStage()
    {
        var with = new System.Collections.Generic.List<ExtractionProgress>();
        var without = new System.Collections.Generic.List<ExtractionProgress>();

        Extraction.Run(DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc().With("EPISODE3.PRG", PlaneProgram())), _assets, with.Add);
        string other = Path.Combine(_assets, "other");
        Directory.CreateDirectory(other);
        Extraction.Run(new SyntheticDisc().With("E03CMN.MDB", new byte[8]).With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb()).WriteCue(other),
            other, without.Add);

        Assert.Equal(without, with);
    }

    [Fact]
    public void Run_ProgramWithThePlanes_LeavesTheTexturesAsTheBaselineBuildWroteThem()
    {
        // The disc of Run_FixedDisc_WritesE03B0N0AsTheBaselineBuildDid, plus the planes.
        string cue = DiscWith(Mdb(Record(), Record(lut: 64), Record(pmod: 0x0420), Record(pixels: 32, pmod: Mode1 | 0x40), Record()), Cgb(),
            new SyntheticDisc().With("EPISODE3.PRG", PlaneProgram()));

        Extraction.Run(cue, _assets);

        var written = Directory.GetFiles(TextureFolder).OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f)))));
        Assert.Equal(new[]
        {
            ("0000.png", "d9c8f7e3a4503fcf3966943ab1bdc9dfcf796362131dff5f8fe99709554a4b8e"),
            ("0001.png", "1d48d8b557d91a51b7711fac2639e02e1c9d666791cc44405c2a4a848bc839f5"),
            ("0003.png", "bdcf009ba090d9060e6f784288f1b2d947a0082bd15612f658f43d9ea67ee5c4"),
        }, written);
    }

    [Fact]
    public void Run_NoLiteralPool_WritesTheTexturesAndNoticesTheMissingPlanes()
    {
        string cue = DiscWith(ColourBankMdb(), CountingCgb(), new SyntheticDisc().With("EPISODE3.PRG", TwoTableProgram()));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Equal(3, result.Files.Count);
        Assert.Empty(result.PlaneFiles);
        Assert.Null(result.PlaneSources);
        Assert.Equal("EPISODE3.PRG holds no literal pool with the cells' and maps' VRAM addresses", result.PlaneNotice);
        Assert.Contains("2 blocks; no floor and canopy: EPISODE3.PRG holds no literal pool with the cells' and maps' VRAM addresses", result.Details());
        Assert.False(File.Exists(FloorFile));
        Assert.DoesNotContain("e03.floor", File.ReadAllText(ManifestFile));
    }

    [Fact]
    public void Run_NoProgram_NamesTheMissingFileForThePlanes()
    {
        ExtractionResult result = Extraction.Run(FixedDisc(), _assets);

        Assert.Equal("the disc has no EPISODE3.PRG", result.PlaneNotice);
        Assert.Contains("no colours for modes 0 and 4: the disc has no EPISODE3.PRG; left out: mode 4 1; no floor and canopy: the disc has no EPISODE3.PRG", result.Details());
        Assert.Empty(result.PlaneFiles);
        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "fixed-disc-manifest.json")), File.ReadAllBytes(ManifestFile));
    }

    [Fact]
    public void Run_MapPointerOutsideTheFile_WritesTheTexturesAndNamesThePointer()
    {
        string cue = DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc().With("EPISODE3.PRG", PlaneProgram(maps: new[] { 0x06100000u, 0x06100000u })));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Single(result.Files);
        Assert.Equal("map list entry 0x06100000 at 0x200 is outside EPISODE3.PRG", result.PlaneNotice);
        Assert.False(File.Exists(FloorFile));
    }

    [Fact]
    public void Run_StreamRunningOffTheEnd_WritesTheTexturesAndNamesTheStream()
    {
        int full = PlaneProgram().Length;
        string cue = DiscWith(Mdb(Record()), Cgb(), new SyntheticDisc().With("EPISODE3.PRG", PlaneProgram(size: full - 3)));

        ExtractionResult result = Extraction.Run(cue, _assets);

        Assert.Single(result.Files);
        Assert.StartsWith("EPISODE3.PRG's map 1: the stream at 0x", result.PlaneNotice);
        Assert.EndsWith("runs off the end of the file", result.PlaneNotice);
        Assert.False(File.Exists(CanopyFile));
    }

    [Fact]
    public void TextureId_AndPath_FollowTheAssetContract()
    {
        Assert.Equal("e03.tex.e03b0n0.0007", Extraction.TextureId("E03B0N0", 7));
        Assert.Equal("images/e03/e03b0n0/0007.png", Extraction.TexturePath("E03B0N0", 7));
        Assert.Equal("e03.tex.e03cmn.1234", Extraction.TextureId("E03CMN", 1234));
    }
}
