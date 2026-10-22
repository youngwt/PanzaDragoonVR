using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The asset folder's manifest, read as the game reads it. Each test works in its own
/// temporary folder with small files made here; the cases mirror tests/tools/test_manifest.py.
/// </summary>
public sealed class AssetManifestTests : IDisposable
{
    private const string Floor = "e03.floor";
    private const string Canopy = "e03.canopy";

    private readonly string _folder;

    public AssetManifestTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pdz-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>A 1 x 1 PNG with the given bit depth and colour type.</summary>
    private static byte[] Png(byte depth = 8, byte colour = 6)
    {
        static byte[] Chunk(string kind, byte[] data)
        {
            byte[] name = Encoding.ASCII.GetBytes(kind);
            byte[] length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            byte[] crc = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(name.Concat(data).ToArray()));
            return length.Concat(name).Concat(data).Concat(crc).ToArray();
        }

        byte[] header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 1);
        header[8] = depth;
        header[9] = colour;
        var pixels = new MemoryStream();
        using (var z = new ZLibStream(pixels, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(new byte[9]);
        }

        byte[] signature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };
        return signature.Concat(Chunk("IHDR", header)).Concat(Chunk("IDAT", pixels.ToArray())).Concat(Chunk("IEND", Array.Empty<byte>())).ToArray();
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }

    private string Put(string path, byte[] contents)
    {
        string full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, contents);
        return full;
    }

    private void Manifest(string json) => Put(AssetManifest.FileName, Encoding.UTF8.GetBytes(json));

    private static string Entry(string id, string path, long bytes, string kind = "image") =>
        $$"""{ "id": "{{id}}", "kind": "{{kind}}", "path": "{{path}}", "bytes": {{bytes}} }""";

    private static string WithAssets(params string[] entries) =>
        $$"""{ "format": 1, "generator": "test", "source": null, "assets": [ {{string.Join(", ", entries)}} ] }""";

    /// <summary>Writes a PNG at <paramref name="path"/> and returns its manifest entry.</summary>
    private string Image(string id, string path)
    {
        byte[] png = Png();
        Put(path, png);
        return Entry(id, path, png.Length);
    }

    private ManifestRead Load() => AssetManifest.Load(_folder);

    // ------------------------------------------------------------ both images supplied

    [Fact]
    public void BothImagesSupplied_AreFound()
    {
        Manifest(WithAssets(Image(Floor, "images/e03.floor.png"), Image(Canopy, "images/e03.canopy.png")));

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Read, read.Status);
        Assert.Empty(read.Manifest!.Problems);
        AssetLookup floor = read.Find(Floor, AssetManifest.ImageKind);
        Assert.Equal(AssetLookupStatus.Found, floor.Status);
        Assert.Equal(Path.Combine(_folder, "images/e03.floor.png"), floor.FullPath);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Canopy, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void ContractExample_IsRead_WithOptionalFieldsAndUnknownFieldsIgnored()
    {
        byte[] png = Png();
        Put("images/e03_floor.png", png);
        Manifest($$"""
            {
              "format": 1,
              "generator": "pdz-extract 0.1",
              "source": { "product": "GS-9049", "version": "V1.001" },
              "later": "a field this build does not know",
              "assets": [
                { "id": "e03.floor", "kind": "image", "path": "images/e03_floor.png", "bytes": {{png.Length}},
                  "sha256": "3a7bd3e2360a3d29eea436fcfb7e44c735d117c42d1c1835420b6b9942dd4f1b", "meta": {} },
                { "id": "e03.flyer", "kind": "model", "path": "models/e03_flyer.glb", "bytes": 48392 }
              ]
            }
            """);

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Read, read.Status);
        Assert.Empty(read.Manifest!.Problems);
        Assert.Equal(new[] { Floor, "e03.flyer" }, read.Manifest.Ids);
        // The game does not read sha256, so a checksum that does not match is not a problem here.
        Assert.Equal(AssetLookupStatus.Found, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    // ------------------------------------------------------------ empty folder

    [Fact]
    public void NoManifest_IsAbsent_AndEveryIdIsUnlisted()
    {
        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Absent, read.Status);
        Assert.Null(read.Manifest);
        Assert.Contains(_folder, read.Problem);
        Assert.Equal(AssetLookupStatus.NotListed, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void FolderThatDoesNotExist_IsAbsent()
    {
        ManifestRead read = AssetManifest.Load(Path.Combine(_folder, "not-there"));

        Assert.Equal(ManifestStatus.Absent, read.Status);
    }

    // ------------------------------------------------------------ one image supplied

    [Fact]
    public void OneImageSupplied_TheOtherIsUnlisted_WithNoProblem()
    {
        Manifest(WithAssets(Image(Floor, "e03.floor.png")));

        ManifestRead read = Load();

        Assert.Equal(AssetLookupStatus.Found, read.Find(Floor, AssetManifest.ImageKind).Status);
        AssetLookup canopy = read.Find(Canopy, AssetManifest.ImageKind);
        Assert.Equal(AssetLookupStatus.NotListed, canopy.Status);
        Assert.Null(canopy.Problem);
    }

    [Fact]
    public void EmptyAssetList_IsUnreadable_AndEveryIdIsUnlisted()
    {
        Manifest(WithAssets());

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Equal("manifest.json: lists no assets", read.Problem);
        Assert.Equal(AssetLookupStatus.NotListed, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void UnsupportedFormatWithAnEmptyAssetList_IsTheUnsupportedFormat()
    {
        Manifest("""{ "format": 2, "generator": "test", "source": null, "assets": [] }""");

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.UnsupportedFormat, read.Status);
        Assert.Contains("unsupported format 2", read.Problem);
    }

    // ------------------------------------------------------------ bad manifest

    [Theory]
    [InlineData("not json at all")]
    [InlineData("nope\n")]
    [InlineData("{ \"format\": 1, ")]
    [InlineData("")]
    public void NotJson_IsUnreadable(string text)
    {
        Manifest(text);

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Contains("not valid JSON", read.Problem);
        Assert.DoesNotContain("\n", read.Problem!);
        Assert.Equal(AssetLookupStatus.NotListed, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void InvalidUtf8InAString_IsUnreadable()
    {
        byte[] start = Encoding.UTF8.GetBytes("{ \"format\": 1, \"generator\": \"bad ");
        byte[] end = Encoding.UTF8.GetBytes(" \", \"source\": null, \"assets\": [] }");
        Put(AssetManifest.FileName, start.Concat(new byte[] { 0xFF, 0xFE }).Concat(end).ToArray());

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Equal("manifest.json: is not UTF-8", read.Problem);
    }

    [Fact]
    public void InvalidUtf8InAnEntry_IsUnreadable_NotAnException()
    {
        byte[] start = Encoding.UTF8.GetBytes("{ \"format\": 1, \"generator\": \"t\", \"source\": null, \"assets\": [ { \"id\": \"e03.");
        byte[] end = Encoding.UTF8.GetBytes("\", \"kind\": \"image\", \"path\": \"a.png\", \"bytes\": 1 } ] }");
        Put(AssetManifest.FileName, start.Concat(new byte[] { 0xC3, 0x28 }).Concat(end).ToArray());

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Equal(AssetLookupStatus.NotListed, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void JsonThatIsNotAnObject_IsUnreadable()
    {
        Manifest("[1, 2]");

        Assert.Equal(ManifestStatus.Unreadable, Load().Status);
    }

    [Fact]
    public void ByteOrderMark_IsUnreadable()
    {
        Put(AssetManifest.FileName, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(WithAssets())).ToArray());

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Contains("byte-order mark", read.Problem);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("generator")]
    [InlineData("source")]
    [InlineData("assets")]
    public void RequiredTopLevelFieldAbsent_IsUnreadable(string field)
    {
        var fields = new[]
        {
            ("format", "1"), ("generator", "\"test\""), ("source", "null"), ("assets", "[]"),
        };
        Manifest("{ " + string.Join(", ", fields.Where(f => f.Item1 != field).Select(f => $"\"{f.Item1}\": {f.Item2}")) + " }");

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Contains($"'{field}' is absent", read.Problem);
    }

    [Theory]
    [InlineData("\"1\"")]
    [InlineData("1.0")]
    [InlineData("true")]
    public void FormatThatIsNotAnInteger_IsUnreadable(string format)
    {
        Manifest($$"""{ "format": {{format}}, "generator": "test", "source": null, "assets": [] }""");

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Unreadable, read.Status);
        Assert.Contains("'format' must be an integer", read.Problem);
    }

    [Fact]
    public void AssetsThatIsNotAnArray_IsUnreadable()
    {
        Manifest("""{ "format": 1, "generator": "test", "source": null, "assets": {} }""");

        Assert.Equal(ManifestStatus.Unreadable, Load().Status);
    }

    [Fact]
    public void FormatTooLargeFor64Bits_IsUnsupported_NotUnreadable()
    {
        Manifest("""{ "format": 99999999999999999999, "generator": "test", "source": null, "assets": [] }""");

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.UnsupportedFormat, read.Status);
        Assert.Contains("unsupported format 99999999999999999999", read.Problem);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void OtherFormat_IsUnsupported(int format)
    {
        Put("e03.floor.png", Png());
        Manifest($$"""{ "format": {{format}}, "generator": "test", "source": null, "assets": [ {{Entry(Floor, "e03.floor.png", Png().Length)}} ] }""");

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.UnsupportedFormat, read.Status);
        Assert.Contains($"unsupported format {format}", read.Problem);
        Assert.Equal(AssetLookupStatus.NotListed, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    // ------------------------------------------------------------ bad entry

    [Fact]
    public void MissingFile_IsAProblemForThatIdOnly()
    {
        Manifest(WithAssets(Entry(Floor, "images/e03.floor.png", 100), Image(Canopy, "e03.canopy.png")));

        ManifestRead read = Load();

        AssetLookup floor = read.Find(Floor, AssetManifest.ImageKind);
        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Equal("e03.floor: images/e03.floor.png: missing", floor.Problem);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Canopy, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void PathThatIsAFolder_IsMissing()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "e03.floor.png"));
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", 100)));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Contains("not a file", floor.Problem);
    }

    [Fact]
    public void WrongSize_IsIncompleteOrChanged()
    {
        byte[] png = Png();
        Put("e03.floor.png", png);
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", png.Length + 1)));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Equal($"e03.floor: e03.floor.png: incomplete or changed: the file is {png.Length} bytes, the manifest says {png.Length + 1}", floor.Problem);
    }

    [Theory]
    [InlineData("/e03.floor.png", "no leading slash")]
    [InlineData("../e03.floor.png", "'.' or '..' parts")]
    [InlineData("images/./e03.floor.png", "'.' or '..' parts")]
    [InlineData("images\\\\e03.floor.png", "backslashes")]
    [InlineData("images//e03.floor.png", "empty parts")]
    [InlineData("Images/e03.floor.png", "lowercase letters")]
    [InlineData("", "must not be empty")]
    public void PathBreakingTheRules_IsAnInvalidEntry(string path, string reason)
    {
        Put("e03.floor.png", Png());
        Manifest(WithAssets(Entry(Floor, path, Png().Length), Image(Canopy, "e03.canopy.png")));

        ManifestRead read = Load();

        AssetLookup floor = read.Find(Floor, AssetManifest.ImageKind);
        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.StartsWith("e03.floor: invalid entry: invalid path:", floor.Problem);
        Assert.Contains(reason, floor.Problem);
        Assert.Single(read.Manifest!.Problems);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Canopy, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void PathLeavingTheFolderThroughALink_IsAnInvalidEntry()
    {
        string outside = Path.Combine(Path.GetTempPath(), "pdz-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            byte[] png = Png();
            File.WriteAllBytes(Path.Combine(outside, "e03.floor.png"), png);
            Directory.CreateSymbolicLink(Path.Combine(_folder, "images"), outside);
            Manifest(WithAssets(Entry(Floor, "images/e03.floor.png", png.Length)));

            AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

            Assert.Equal(AssetLookupStatus.Problem, floor.Status);
            Assert.Contains("leaves the asset folder", floor.Problem);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void LinkInAParentPartLeavingTheFolder_IsAnInvalidEntry()
    {
        // images -> <folder>/evil/x, and evil -> a folder outside.
        string outside = Path.Combine(Path.GetTempPath(), "pdz-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(outside, "x"));
        try
        {
            byte[] png = Png();
            File.WriteAllBytes(Path.Combine(outside, "x", "e03.floor.png"), png);
            Directory.CreateSymbolicLink(Path.Combine(_folder, "evil"), outside);
            Directory.CreateSymbolicLink(Path.Combine(_folder, "images"), Path.Combine(_folder, "evil", "x"));
            Manifest(WithAssets(Entry(Floor, "images/e03.floor.png", png.Length)));

            AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

            Assert.Equal(AssetLookupStatus.Problem, floor.Status);
            Assert.Contains("leaves the asset folder", floor.Problem);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void DotDotAfterALinkLeavingTheFolder_IsAnInvalidEntry()
    {
        // images -> sub/../real, and sub -> <outside>/o, so images is <outside>/real, not <folder>/real.
        string outside = Path.Combine(Path.GetTempPath(), "pdz-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(outside, "o"));
        Directory.CreateDirectory(Path.Combine(outside, "real"));
        try
        {
            byte[] png = Png();
            File.WriteAllBytes(Path.Combine(outside, "real", "e03.floor.png"), png);
            Put("real/e03.floor.png", png);
            Directory.CreateSymbolicLink(Path.Combine(_folder, "sub"), Path.Combine(outside, "o"));
            Directory.CreateSymbolicLink(Path.Combine(_folder, "images"), "sub/../real");
            Manifest(WithAssets(Entry(Floor, "images/e03.floor.png", png.Length)));

            AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

            Assert.Equal(AssetLookupStatus.Problem, floor.Status);
            Assert.Contains("leaves the asset folder", floor.Problem);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void LinkThatStaysInsideTheFolder_IsFollowed()
    {
        Image(Floor, "real/e03.floor.png");
        Directory.CreateSymbolicLink(Path.Combine(_folder, "images"), Path.Combine(_folder, "real"));
        Manifest(WithAssets(Entry(Floor, "images/e03.floor.png", Png().Length)));

        Assert.Equal(AssetLookupStatus.Found, Load().Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void NegativeBytes_IsAnInvalidEntry()
    {
        Put("e03.floor.png", Png());
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", -1)));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Equal("e03.floor: invalid entry: 'bytes' is negative", floor.Problem);
    }

    [Fact]
    public void BytesTooLargeFor64Bits_IsIncompleteOrChanged()
    {
        Put("e03.floor.png", Png());
        Manifest(WithAssets("""{ "id": "e03.floor", "kind": "image", "path": "e03.floor.png", "bytes": 99999999999999999999 }"""));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Contains("incomplete or changed", floor.Problem);
        Assert.EndsWith("the manifest says 99999999999999999999", floor.Problem);
    }

    [Fact]
    public void NegativeBytesTooLargeFor64Bits_IsNegative()
    {
        Put("e03.floor.png", Png());
        Manifest(WithAssets("""{ "id": "e03.floor", "kind": "image", "path": "e03.floor.png", "bytes": -99999999999999999999 }"""));

        Assert.Equal("e03.floor: invalid entry: 'bytes' is negative", Load().Find(Floor, AssetManifest.ImageKind).Problem);
    }

    [Theory]
    [InlineData("10.0")]
    [InlineData("1e3")]
    public void BytesWithAFractionOrExponent_IsNotAnInteger(string bytes)
    {
        Put("e03.floor.png", Png());
        Manifest(WithAssets($$"""{ "id": "e03.floor", "kind": "image", "path": "e03.floor.png", "bytes": {{bytes}} }"""));

        Assert.Equal("e03.floor: invalid entry: field 'bytes' must be an integer", Load().Find(Floor, AssetManifest.ImageKind).Problem);
    }

    [Fact]
    public void EntryWithARequiredFieldAbsentOrWrong_IsAnInvalidEntry()
    {
        Put("e03.floor.png", Png());
        Manifest(WithAssets(
            """{ "id": "e03.floor", "path": "e03.floor.png", "bytes": 10 }""",
            """{ "id": "e03.canopy", "kind": "image", "path": "e03.canopy.png", "bytes": "10" }"""));

        ManifestRead read = Load();

        Assert.Equal("e03.floor: invalid entry: required field 'kind' is absent", read.Find(Floor, AssetManifest.ImageKind).Problem);
        Assert.Equal("e03.canopy: invalid entry: field 'bytes' must be an integer", read.Find(Canopy, AssetManifest.ImageKind).Problem);
        Assert.Equal(2, read.Manifest!.Problems.Count);
    }

    [Fact]
    public void EntriesWithNoIdOrNotAnObject_AreProblemsButHideNoOtherId()
    {
        Manifest(WithAssets("""{ "kind": "image", "path": "x.png", "bytes": 1 }""", "42", Image(Floor, "e03.floor.png")));

        ManifestRead read = Load();

        Assert.Equal(new[] { "entry 1: invalid entry: required field 'id' is absent", "entry 2: must be an object" }, read.Manifest!.Problems);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void LoneSurrogateInAnId_IsAnInvalidEntryAndHidesNoOtherId()
    {
        Manifest(WithAssets(Entry("\\ud800", "x.png", 1), Image(Floor, "e03.floor.png")));

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Read, read.Status);
        Assert.Equal(new[] { "entry 1: invalid entry: field 'id' is not readable text: it holds a lone surrogate escape" }, read.Manifest!.Problems);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Floor, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void LoneSurrogateInAPath_IsAnInvalidEntryForThatIdOnly()
    {
        Manifest(WithAssets(Entry(Floor, "\\ud800.png", 1), Image(Canopy, "e03.canopy.png")));

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Read, read.Status);
        Assert.Equal(
            "e03.floor: invalid entry: field 'path' is not readable text: it holds a lone surrogate escape",
            read.Find(Floor, AssetManifest.ImageKind).Problem);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Canopy, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void LoneSurrogateInAKind_IsAKindTheGameDoesNotKnow()
    {
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", 1, kind: "\\ud800"), Image(Canopy, "e03.canopy.png")));

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Read, read.Status);
        Assert.Empty(read.Manifest!.Problems);
        Assert.Equal(
            "e03.floor: wrong kind: the manifest says '\"\\ud800\"', the game needs 'image'",
            read.Find(Floor, AssetManifest.ImageKind).Problem);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Canopy, AssetManifest.ImageKind).Status);
    }

    [Theory]
    [InlineData("E03.floor")]
    [InlineData("e03..floor")]
    [InlineData(".e03")]
    [InlineData("e03.")]
    [InlineData("e03-floor")]
    public void InvalidId_IsAnInvalidEntry(string id)
    {
        Manifest(WithAssets(Image(id, "a.png")));

        ManifestRead read = Load();

        Assert.Single(read.Manifest!.Problems);
        Assert.Contains("not a valid ID", read.Manifest.Problems[0]);
        Assert.Equal(AssetLookupStatus.Problem, read.Find(id, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void WrongKind_IsAProblem_WithTheKindFound()
    {
        Put("e03.floor.glb", new byte[12]);
        Manifest(WithAssets(Entry(Floor, "e03.floor.glb", 12, kind: "model")));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Equal("e03.floor: wrong kind: the manifest says 'model', the game needs 'image'", floor.Problem);
    }

    [Fact]
    public void UnknownKind_IsWrongKind_NotAnUnreadableManifest()
    {
        Put("e03.floor.wav", new byte[4]);
        Manifest(WithAssets(Entry(Floor, "e03.floor.wav", 4, kind: "sound")));

        ManifestRead read = Load();

        Assert.Equal(ManifestStatus.Read, read.Status);
        Assert.Empty(read.Manifest!.Problems);
        Assert.Contains("wrong kind", read.Find(Floor, AssetManifest.ImageKind).Problem);
    }

    [Fact]
    public void DuplicateId_IsAProblemForThatId_AndNeitherEntryIsUsed()
    {
        Manifest(WithAssets(Image(Floor, "a.png"), Image(Floor, "b.png"), Image(Canopy, "c.png")));

        ManifestRead read = Load();

        AssetLookup floor = read.Find(Floor, AssetManifest.ImageKind);
        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Equal("e03.floor: duplicate ID, listed for both a.png and b.png", floor.Problem);
        Assert.Equal(new[] { floor.Problem! }, read.Manifest!.Problems);
        Assert.Equal(AssetLookupStatus.Found, read.Find(Canopy, AssetManifest.ImageKind).Status);
    }

    [Fact]
    public void DuplicateId_WhoseFirstEntryHasNoPath_NamesThatEntryByItsPlace()
    {
        Manifest(WithAssets("""{ "id": "e03.floor", "kind": "image", "bytes": 1 }""", Image(Floor, "b.png")));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal("e03.floor: duplicate ID, listed for both entry 1 and b.png", floor.Problem);
    }

    // ------------------------------------------------------------ unreadable image

    [Fact]
    public void FileThatIsNotAPng_IsAnUnreadableImage()
    {
        byte[] text = Encoding.ASCII.GetBytes("this is not a picture at all, just words");
        Put("e03.floor.png", text);
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", text.Length)));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Equal("e03.floor: e03.floor.png: unreadable image: not a PNG file", floor.Problem);
    }

    [Fact]
    public void FileTooShortForAPngHeader_IsAnUnreadableImage()
    {
        Put("e03.floor.png", new byte[] { 0x89, (byte)'P' });
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", 2)));

        Assert.Contains("not a PNG file", Load().Find(Floor, AssetManifest.ImageKind).Problem);
    }

    [Theory]
    [InlineData(16, 6, "a 16-bit RGBA PNG")]
    [InlineData(8, 3, "a 8-bit palette PNG")]
    [InlineData(8, 0, "a 8-bit greyscale PNG")]
    public void PngThatIsNot8BitRgbOrRgba_IsAnUnreadableImage(byte depth, byte colour, string described)
    {
        byte[] png = Png(depth, colour);
        Put("e03.floor.png", png);
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", png.Length)));

        AssetLookup floor = Load().Find(Floor, AssetManifest.ImageKind);

        Assert.Equal(AssetLookupStatus.Problem, floor.Status);
        Assert.Contains(described, floor.Problem);
    }

    [Fact]
    public void RgbPng_IsFound()
    {
        byte[] png = Png(8, 2);
        Put("e03.floor.png", png);
        Manifest(WithAssets(Entry(Floor, "e03.floor.png", png.Length)));

        Assert.Equal(AssetLookupStatus.Found, Load().Find(Floor, AssetManifest.ImageKind).Status);
    }
}
