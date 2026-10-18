using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The game's check of a model file before the engine sees it, one or more tests per row of the
/// asset contract's "When a model is invalid" table. The small <c>.glb</c> files are built here.
/// </summary>
public sealed class GlbCheckTests : IDisposable
{
    private const string Flyer = "e03.flyer";

    private readonly string _folder;

    public GlbCheckTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pdz-glb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    // ------------------------------------------------------------ building .glb files

    /// <summary>One triangle: three positions, then three indices. 42 bytes.</summary>
    private static readonly byte[] TriangleData = Enumerable.Repeat((byte)0, 36).Concat(new byte[] { 0, 0, 1, 0, 2, 0 }).ToArray();

    /// <summary>
    /// A glTF JSON for one triangle in one mesh used by one node. <paramref name="extra"/> is added at
    /// the top level; <paramref name="buffer"/> and <paramref name="attributes"/> replace the defaults.
    /// </summary>
    private static string Json(string extra = "", string? buffer = null, string attributes = "\"POSITION\": 0",
        string meshes = """[{ "primitives": [{ "attributes": { ATTRS }, "indices": 1 }] }]""",
        string nodes = """[{ "name": "flyer", "mesh": 0 }]""", string version = "2.0")
    {
        buffer ??= $$"""{ "byteLength": {{TriangleData.Length}} }""";
        string comma = extra.Length == 0 ? "" : ", ";
        return $$"""
            {
              "asset": { "version": "{{version}}" },
              "scene": 0,
              "scenes": [{ "nodes": [0] }],
              "nodes": {{nodes}},
              "meshes": {{meshes.Replace("ATTRS", attributes)}},
              "buffers": [{{buffer}}],
              "bufferViews": [{ "buffer": 0, "byteOffset": 0, "byteLength": 36 }, { "buffer": 0, "byteOffset": 36, "byteLength": 6 }],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [0, 0, 0] },
                { "bufferView": 1, "componentType": 5123, "count": 3, "type": "SCALAR" }
              ]{{comma}}{{extra}}
            }
            """;
    }

    /// <summary>A <c>.glb</c> of the given JSON and binary chunk, each padded to four bytes as glTF requires.</summary>
    private static byte[] Glb(string json, byte[]? bin = null, uint version = 2)
    {
        static byte[] Chunk(uint type, byte[] data, byte pad)
        {
            int padded = (data.Length + 3) / 4 * 4;
            byte[] chunk = new byte[8 + padded];
            BinaryPrimitives.WriteUInt32LittleEndian(chunk, (uint)padded);
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), type);
            data.CopyTo(chunk, 8);
            chunk.AsSpan(8 + data.Length).Fill(pad);
            return chunk;
        }

        byte[] body = Chunk(0x4E4F534A, Encoding.UTF8.GetBytes(json), (byte)' ');
        if (bin != null)
        {
            body = body.Concat(Chunk(0x004E4942, bin, 0)).ToArray();
        }

        byte[] header = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), version);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)(12 + body.Length));
        return header.Concat(body).ToArray();
    }

    private static byte[] ValidGlb(string extra = "") => Glb(Json(extra), TriangleData);

    private static GlbCheckResult Check(byte[] file) => GlbCheck.Check(file);

    // ------------------------------------------------------------ model supplied

    [Fact]
    public void ValidModel_IsUsable()
    {
        GlbCheckResult result = Check(ValidGlb());

        Assert.Equal(GlbProblemKind.None, result.Kind);
        Assert.True(result.IsUsable);
        Assert.Null(result.Line(Flyer, "models/e03.flyer.glb"));
    }

    [Fact]
    public void ValidModel_WithAnEmbeddedImage_CountsIt()
    {
        string images = """
            "images": [{ "bufferView": 1, "mimeType": "image/png" }],
            "textures": [{ "source": 0 }]
            """;

        GlbCheckResult result = Check(ValidGlb(images));

        Assert.True(result.IsUsable);
        Assert.Equal(1, result.ImageCount);
    }

    [Fact]
    public void Conventions_AreNotPoliced()
    {
        // Unlit listed as used, a lit material of another name, linear filtering and a JPEG mime type:
        // all break the contract's conventions and none is the game's to reject.
        string extra = """
            "extensionsUsed": ["KHR_materials_unlit", "KHR_texture_transform"],
            "materials": [{ "name": "shiny", "pbrMetallicRoughness": { "metallicFactor": 1 } }],
            "samplers": [{ "magFilter": 9729, "minFilter": 9987 }],
            "images": [{ "bufferView": 1, "mimeType": "image/jpeg" }],
            "animations": []
            """;

        Assert.True(Check(ValidGlb(extra)).IsUsable);
    }

    // ------------------------------------------------------------ vertex colours

    [Fact]
    public void VertexColours_AreNotAProblem()
    {
        byte[] file = Glb(Json(attributes: "\"POSITION\": 0, \"COLOR_0\": 0"), TriangleData);

        Assert.True(Check(file).IsUsable);
    }

    // ------------------------------------------------------------ not readable

    [Fact]
    public void NotAGlb_IsUnreadable()
    {
        GlbCheckResult result = Check(Encoding.ASCII.GetBytes("this is not a model at all"));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
        Assert.Equal("e03.flyer: models/f.glb: unreadable model: not a .glb file: no glTF header", result.Line(Flyer, "models/f.glb"));
    }

    [Fact]
    public void TooShort_IsUnreadable()
    {
        Assert.Equal(GlbProblemKind.Unreadable, Check(new byte[] { (byte)'g', (byte)'l', (byte)'T', (byte)'F' }).Kind);
        Assert.Equal(GlbProblemKind.Unreadable, Check(Array.Empty<byte>()).Kind);
    }

    [Fact]
    public void GlbVersion1_IsUnreadable()
    {
        GlbCheckResult result = Check(Glb(Json(), TriangleData, version: 1));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
        Assert.Contains("version 1", result.Detail);
    }

    [Fact]
    public void AssetVersion1_IsUnreadable()
    {
        GlbCheckResult result = Check(Glb(Json(version: "1.0"), TriangleData));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
        Assert.Contains("'1.0'", result.Detail);
    }

    [Fact]
    public void NoAssetVersion_IsUnreadable()
    {
        Assert.Equal(GlbProblemKind.Unreadable, Check(Glb("""{ "meshes": [] }""")).Kind);
    }

    [Fact]
    public void CutShort_IsUnreadable()
    {
        byte[] whole = ValidGlb();

        GlbCheckResult result = Check(whole.AsSpan(0, whole.Length - 8).ToArray());

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
        Assert.Contains("length", result.Detail);
    }

    [Fact]
    public void JsonChunkRunningPastTheEnd_IsUnreadable()
    {
        byte[] file = ValidGlb();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(12), (uint)file.Length);

        Assert.Equal(GlbProblemKind.Unreadable, Check(file).Kind);
    }

    [Fact]
    public void FirstChunkNotJson_IsUnreadable()
    {
        byte[] file = ValidGlb();
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(16), 0x004E4942);

        Assert.Equal(GlbProblemKind.Unreadable, Check(file).Kind);
    }

    [Fact]
    public void JsonThatIsNotJson_IsUnreadable()
    {
        GlbCheckResult result = Check(Glb("{ \"asset\": ", TriangleData));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
        Assert.Contains("not valid JSON", result.Detail);
    }

    [Fact]
    public void LoneSurrogateInAssetVersion_IsUnreadable()
    {
        GlbCheckResult result = Check(Glb(Json(version: "\\uD800"), TriangleData));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
        Assert.Equal("e03.flyer: models/f.glb: unreadable model: the JSON chunk could not be read", result.Line(Flyer, "models/f.glb"));
    }

    [Fact]
    public void LoneSurrogateInARequiredExtension_IsUnreadable()
    {
        GlbCheckResult result = Check(ValidGlb("\"extensionsRequired\": [\"\\uD800\"]"));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
    }

    [Fact]
    public void LoneSurrogateInAnImageUri_IsUnreadable()
    {
        GlbCheckResult result = Check(ValidGlb("\"images\": [{ \"uri\": \"\\uD800.png\" }]"));

        Assert.Equal(GlbProblemKind.Unreadable, result.Kind);
    }

    [Fact]
    public void BufferWithNoBinaryChunk_IsUnreadable()
    {
        Assert.Equal(GlbProblemKind.Unreadable, Check(Glb(Json())).Kind);
    }

    [Fact]
    public void BufferLongerThanTheBinaryChunk_IsUnreadable()
    {
        Assert.Equal(GlbProblemKind.Unreadable, Check(Glb(Json(buffer: """{ "byteLength": 4096 }"""), TriangleData)).Kind);
    }

    // ------------------------------------------------------------ not self-contained

    [Fact]
    public void ExternalBuffer_IsNotSelfContained()
    {
        GlbCheckResult result = Check(Glb(Json(buffer: """{ "uri": "flyer.bin", "byteLength": 42 }""")));

        Assert.Equal(GlbProblemKind.NotSelfContained, result.Kind);
        Assert.Equal("e03.flyer: models/f.glb: not self-contained: buffer 0 names the file 'flyer.bin', which was not opened",
            result.Line(Flyer, "models/f.glb"));
    }

    [Fact]
    public void DataUriBuffer_IsNotSelfContained()
    {
        GlbCheckResult result = Check(Glb(Json(buffer: """{ "uri": "data:application/octet-stream;base64,AAAA", "byteLength": 3 }""")));

        Assert.Equal(GlbProblemKind.NotSelfContained, result.Kind);
        Assert.Equal("buffer 0 has a data: URI", result.Detail);
    }

    [Fact]
    public void ExternalImage_IsNotSelfContained()
    {
        GlbCheckResult result = Check(ValidGlb("""
            "images": [{ "bufferView": 1, "mimeType": "image/png" }, { "uri": "../../../secret.png" }]
            """));

        Assert.Equal(GlbProblemKind.NotSelfContained, result.Kind);
        Assert.Contains("image 1 names the file '../../../secret.png'", result.Detail);
    }

    [Fact]
    public void DataUriImage_IsNotSelfContained()
    {
        GlbCheckResult result = Check(ValidGlb("""
            "images": [{ "uri": "data:image/png;base64,iVBORw0KGgo=" }]
            """));

        Assert.Equal(GlbProblemKind.NotSelfContained, result.Kind);
        Assert.Equal("image 0 has a data: URI", result.Detail);
    }

    [Fact]
    public void ALongFileName_IsShortened()
    {
        string name = new string('a', 200) + ".bin";

        GlbCheckResult result = Check(Glb(Json(buffer: $$"""{ "uri": "{{name}}", "byteLength": 42 }""")));

        Assert.Equal(GlbProblemKind.NotSelfContained, result.Kind);
        Assert.True(result.Detail!.Length < 120);
    }

    [Fact]
    public void AReferenceOutside_IsFoundBeforeARequiredExtension()
    {
        string extra = """
            "extensionsRequired": ["KHR_draco_mesh_compression"],
            "images": [{ "uri": "skin.png" }]
            """;

        Assert.Equal(GlbProblemKind.NotSelfContained, Check(ValidGlb(extra)).Kind);
    }

    // ------------------------------------------------------------ required extension

    [Fact]
    public void RequiredExtension_IsUnsupported_WithItsName()
    {
        GlbCheckResult result = Check(ValidGlb("""
            "extensionsUsed": ["KHR_draco_mesh_compression"],
            "extensionsRequired": ["KHR_draco_mesh_compression"]
            """));

        Assert.Equal(GlbProblemKind.UnsupportedModel, result.Kind);
        Assert.Equal("e03.flyer: models/f.glb: unsupported model: it requires the extension KHR_draco_mesh_compression",
            result.Line(Flyer, "models/f.glb"));
    }

    [Fact]
    public void EvenTheUnlitExtension_IsUnsupported_WhenRequired()
    {
        GlbCheckResult result = Check(ValidGlb("""
            "extensionsUsed": ["KHR_materials_unlit"],
            "extensionsRequired": ["KHR_materials_unlit", "EXT_meshopt_compression"]
            """));

        Assert.Equal(GlbProblemKind.UnsupportedModel, result.Kind);
        Assert.Equal("it requires the extensions KHR_materials_unlit, EXT_meshopt_compression", result.Detail);
    }

    [Fact]
    public void EmptyRequiredList_IsNotAProblem()
    {
        Assert.True(Check(ValidGlb("\"extensionsRequired\": []")).IsUsable);
    }

    // ------------------------------------------------------------ no mesh

    [Fact]
    public void NoMeshes_IsEmpty()
    {
        GlbCheckResult result = Check(Glb(Json(meshes: "[]", nodes: """[{ "name": "flyer" }]"""), TriangleData));

        Assert.Equal(GlbProblemKind.Empty, result.Kind);
        Assert.Equal("e03.flyer: models/f.glb: empty model: it has no mesh", result.Line(Flyer, "models/f.glb"));
    }

    [Fact]
    public void MeshNoNodeUses_IsEmpty()
    {
        GlbCheckResult result = Check(Glb(Json(nodes: """[{ "name": "flyer" }]"""), TriangleData));

        Assert.Equal(GlbProblemKind.Empty, result.Kind);
    }

    [Fact]
    public void NoBuffersOrMeshesAtAll_IsEmpty()
    {
        Assert.Equal(GlbProblemKind.Empty, Check(Glb("""{ "asset": { "version": "2.0" } }""")).Kind);
    }

    // ------------------------------------------------------------ through the manifest

    private void Put(string path, byte[] contents)
    {
        string full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, contents);
    }

    private AssetLookup FindFlyer(string kind, string path, byte[] contents, string? listedKind = null)
    {
        Put(path, contents);
        Put(AssetManifest.FileName, Encoding.UTF8.GetBytes($$"""
            { "format": 1, "generator": "test", "source": null, "assets": [
              { "id": "{{Flyer}}", "kind": "{{listedKind ?? kind}}", "path": "{{path}}", "bytes": {{contents.Length}} } ] }
            """));
        return AssetManifest.Load(_folder).Find(Flyer, AssetManifest.ModelKind);
    }

    [Fact]
    public void Manifest_ValidModel_IsFound()
    {
        AssetLookup lookup = FindFlyer("model", "models/e03.flyer.glb", ValidGlb());

        Assert.Equal(AssetLookupStatus.Found, lookup.Status);
        Assert.Equal(Path.Combine(_folder, "models/e03.flyer.glb"), lookup.FullPath);
    }

    [Fact]
    public void Manifest_NotListed_IsNotAProblem()
    {
        // The manifest lists another ID only: an empty list would make the manifest unreadable.
        Put(AssetManifest.FileName, Encoding.UTF8.GetBytes("""
            { "format": 1, "generator": "test", "source": null, "assets": [
              { "id": "e03.floor", "kind": "image", "path": "images/e03.floor.png", "bytes": 1 } ] }
            """));

        Assert.Equal(AssetLookupStatus.NotListed, AssetManifest.Load(_folder).Find(Flyer, AssetManifest.ModelKind).Status);
    }

    [Fact]
    public void Manifest_ExternalBuffer_IsAProblem_AndTheNamedFileIsNotNeeded()
    {
        AssetLookup lookup = FindFlyer("model", "models/f.glb", Glb(Json(buffer: """{ "uri": "f.bin", "byteLength": 42 }""")));

        Assert.Equal(AssetLookupStatus.Problem, lookup.Status);
        Assert.StartsWith("e03.flyer: models/f.glb: not self-contained:", lookup.Problem);
    }

    [Fact]
    public void Manifest_UnreadableModel_IsAProblem()
    {
        AssetLookup lookup = FindFlyer("model", "models/f.glb", Encoding.ASCII.GetBytes("not a model"));

        Assert.Equal(AssetLookupStatus.Problem, lookup.Status);
        Assert.StartsWith("e03.flyer: models/f.glb: unreadable model:", lookup.Problem);
    }

    [Fact]
    public void Manifest_ListedAsImage_IsWrongKind()
    {
        AssetLookup lookup = FindFlyer("model", "models/f.glb", ValidGlb(), listedKind: "image");

        Assert.Equal(AssetLookupStatus.Problem, lookup.Status);
        Assert.Equal("e03.flyer: wrong kind: the manifest says 'image', the game needs 'model'", lookup.Problem);
    }

    [Fact]
    public void Manifest_ModelProblem_JoinsTheLaunchReport()
    {
        AssetLookup lookup = FindFlyer("model", "models/f.glb", ValidGlb("\"extensionsRequired\": [\"EXT_x\"]"));

        LaunchReport report = LaunchReport.Build(_folder, AssetManifest.Load(_folder), new[] { lookup });

        string problem = Assert.Single(report.Problems);
        Assert.Equal("e03.flyer: models/f.glb: unsupported model: it requires the extension EXT_x", problem);
    }
}
