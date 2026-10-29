using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace PdzDecoding.Tests;

/// <summary>
/// The model writer, one or more tests per row of the story's matrix (one part, parts, all four
/// modes, flipped, a triangle, too many textures, a bad model), then the atlas's border and pixels,
/// UVs, the sampler, the materials, no <c>uri</c>, accessor bounds, determinism and the recorded
/// samples in <c>expected/models/</c>. Every model here is built by the tests; nothing comes from the
/// disc. With <c>PDZ_RECORD_EXPECTED=1</c> the recorded-sample test rewrites the samples in the
/// source tree instead of comparing.
/// </summary>
public sealed class GlbWriterTests
{
    /// <summary>The widest or tallest texture a 2048 x 2048 atlas holds with its 2-pixel border.</summary>
    private const int MaxTexture = 2044;

    // ------------------------------------------------------------ building models

    /// <summary>A <paramref name="width"/> x <paramref name="height"/> texture whose pixels differ, with a transparent top-left pixel when <paramref name="hole"/>.</summary>
    private static ModelTexture Pattern(int width, int height, int seed = 0, bool hole = false)
    {
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                rgba[i] = (byte)(x * 29 + seed);
                rgba[i + 1] = (byte)(y * 53 + seed * 3);
                rgba[i + 2] = (byte)((x ^ y) + 100 + seed);
                rgba[i + 3] = (byte)(hole && x == 0 && y == 0 ? 0 : 255);
            }
        }

        return new ModelTexture(width, height, rgba);
    }

    private static readonly ModelColour Red = new(200, 30, 40);
    private static readonly ModelColour Blue = new(20, 40, 220);

    /// <summary>A unit square in the XY plane with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>), corners clockwise from there.</summary>
    private static ModelQuad Square(float x, float y, ModelTexture? texture, DrawingMode mode = DrawingMode.Opaque,
        bool flipH = false, bool flipV = false, ModelColour colour = default, float z = 0) =>
        new(new Vector3(x, y, z), new Vector3(x + 1, y, z), new Vector3(x + 1, y - 1, z), new Vector3(x, y - 1, z),
            texture, colour, mode, flipH, flipV);

    private static ModelPart Part(string name, params ModelQuad[] quads) => new(name, Vector3.Zero, quads);

    private static Model OneModel(params ModelQuad[] quads) => new("flyer", new[] { Part("body", quads) });

    /// <summary>Two textured quads sharing one texture (as two equal copies), and one flat-coloured quad.</summary>
    private static Model OnePartSample() => new("one_part", new[]
    {
        Part("body",
            Square(0, 0, Pattern(8, 4, hole: true)),
            Square(1, 0, Pattern(8, 4, hole: true)),
            Square(0, -1, null, colour: Red)),
    });

    /// <summary>Three parts with origins, all four drawing modes, every flip and a triangle.</summary>
    private static Model PartsSample()
    {
        ModelTexture t1 = Pattern(8, 4, hole: true);
        ModelTexture t2 = Pattern(16, 8, seed: 7);
        return new Model("parts", new[]
        {
            new ModelPart("body", new Vector3(0, 0.5f, 0), new[]
            {
                Square(-0.5f, 0.25f, t1),
                Square(-0.5f, 0.25f, t2, DrawingMode.Cutout, flipH: true, z: 0.125f),
                Square(-0.5f, 0.25f, null, DrawingMode.Half, colour: Red, z: -0.125f),
            }),
            new ModelPart("left_wing", new Vector3(-1, 0, 0.25f), new[]
            {
                Square(-1, 0.1f, t1, DrawingMode.Mesh, flipV: true),
                Square(-2, 0.1f, t2, DrawingMode.Opaque, flipH: true, flipV: true),
            }),
            new ModelPart("right_wing", new Vector3(1, 0, 0.25f), new[]
            {
                new ModelQuad(new Vector3(0, 0, 0), new Vector3(1.5f, 0, 0), new Vector3(0, -0.75f, 0), new Vector3(0, -0.75f, 0),
                    t2, default, DrawingMode.Cutout),
                Square(0, 0.3f, null, DrawingMode.Mesh, colour: Blue),
            }),
        });
    }

    /// <summary>A texture as large as an atlas can hold, then a colour and a texture that go on to a second atlas.</summary>
    private static Model TwoAtlasesSample()
    {
        ModelTexture big = Pattern(MaxTexture, MaxTexture, seed: 3);
        return new Model("two_atlases", new[]
        {
            Part("body",
                Square(0, 0, big),
                Square(1, 0, null, colour: Blue),
                Square(2, 0, Pattern(8, 4, hole: true), DrawingMode.Cutout)),
        });
    }

    public static IEnumerable<object[]> Samples() => new[]
    {
        new object[] { "one_part" },
        new object[] { "parts" },
        new object[] { "two_atlases" },
    };

    private static Model Sample(string name) => name switch
    {
        "one_part" => OnePartSample(),
        "parts" => PartsSample(),
        "two_atlases" => TwoAtlasesSample(),
        _ => throw new ArgumentException(name),
    };

    // ------------------------------------------------------------ reading what was written

    private sealed record Glb(JsonElement Json, byte[] Bin);

    /// <summary>The JSON and binary chunk of a written file, the container checked on the way.</summary>
    private static Glb Read(byte[] file)
    {
        Assert.Equal(0x46546C67u, BinaryPrimitives.ReadUInt32LittleEndian(file));
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4)));
        Assert.Equal((uint)file.Length, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(8)));
        int jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12));
        Assert.Equal(0x4E4F534Au, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(16)));
        Assert.Equal(0, jsonLength % 4);
        int b = 20 + jsonLength;
        int binLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(b));
        Assert.Equal(0x004E4942u, BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(b + 4)));
        Assert.Equal(0, binLength % 4);
        Assert.Equal(file.Length, b + 8 + binLength);
        using JsonDocument document = JsonDocument.Parse(file.AsMemory(20, jsonLength));
        return new Glb(document.RootElement.Clone(), file[(b + 8)..]);
    }

    private static Glb Write(Model model) => Read(GlbWriter.Write(model));

    private static JsonElement[] Array(JsonElement e, string field) =>
        e.TryGetProperty(field, out JsonElement a) ? a.EnumerateArray().ToArray() : System.Array.Empty<JsonElement>();

    private static byte[] ViewBytes(Glb glb, int view)
    {
        JsonElement v = Array(glb.Json, "bufferViews")[view];
        int offset = v.GetProperty("byteOffset").GetInt32();
        return glb.Bin[offset..(offset + v.GetProperty("byteLength").GetInt32())];
    }

    private static float[] Floats(Glb glb, int accessor)
    {
        JsonElement a = Array(glb.Json, "accessors")[accessor];
        Assert.Equal(5126, a.GetProperty("componentType").GetInt32());
        byte[] data = ViewBytes(glb, a.GetProperty("bufferView").GetInt32());
        return Enumerable.Range(0, data.Length / 4).Select(i => BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(i * 4))).ToArray();
    }

    private static int[] Indices(Glb glb, int accessor)
    {
        JsonElement a = Array(glb.Json, "accessors")[accessor];
        byte[] data = ViewBytes(glb, a.GetProperty("bufferView").GetInt32());
        return a.GetProperty("componentType").GetInt32() switch
        {
            5123 => Enumerable.Range(0, data.Length / 2).Select(i => (int)BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i * 2))).ToArray(),
            5125 => Enumerable.Range(0, data.Length / 4).Select(i => (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i * 4))).ToArray(),
            int other => throw new InvalidOperationException($"component type {other}"),
        };
    }

    /// <summary>An image in the file, decoded from the PNG <see cref="PngWriter"/> writes.</summary>
    private static (int Size, byte[] Rgba) Image(Glb glb, int image)
    {
        JsonElement i = Array(glb.Json, "images")[image];
        Assert.Equal("image/png", i.GetProperty("mimeType").GetString());
        byte[] png = ViewBytes(glb, i.GetProperty("bufferView").GetInt32());
        int width = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16));
        int height = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20));
        Assert.Equal(width, height);
        int idat = 33;
        int length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(idat));
        Assert.Equal("IDAT", Encoding.ASCII.GetString(png, idat + 4, 4));
        using var output = new MemoryStream();
        using (var z = new ZLibStream(new MemoryStream(png, idat + 8, length), CompressionMode.Decompress))
        {
            z.CopyTo(output);
        }

        byte[] raw = output.ToArray();
        int stride = width * 4;
        var rgba = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            Assert.Equal(0, raw[y * (stride + 1)]);
            System.Array.Copy(raw, y * (stride + 1) + 1, rgba, y * stride, stride);
        }

        return (width, rgba);
    }

    private static JsonElement Primitive(Glb glb, int mesh, int primitive) =>
        Array(Array(glb.Json, "meshes")[mesh], "primitives")[primitive];

    private static string MaterialName(Glb glb, JsonElement primitive) =>
        Array(glb.Json, "materials")[primitive.GetProperty("material").GetInt32()].GetProperty("name").GetString()!;

    /// <summary>The UVs of a primitive's quad <paramref name="k"/>, corners A to D, as atlas pixels.</summary>
    private static (float U, float V)[] QuadUvs(Glb glb, JsonElement primitive, int k, int atlasSize)
    {
        float[] uv = Floats(glb, primitive.GetProperty("attributes").GetProperty("TEXCOORD_0").GetInt32());
        return Enumerable.Range(0, 4).Select(c => (uv[(k * 4 + c) * 2] * atlasSize, uv[(k * 4 + c) * 2 + 1] * atlasSize)).ToArray();
    }

    /// <summary>The atlas holds <paramref name="texture"/> at (<paramref name="x"/>, <paramref name="y"/>), unchanged, with a 2-pixel border repeating its edge.</summary>
    private static void AssertPlaced((int Size, byte[] Rgba) atlas, int x, int y, ModelTexture texture)
    {
        for (int ty = -2; ty < texture.Height + 2; ty++)
        {
            for (int tx = -2; tx < texture.Width + 2; tx++)
            {
                int sx = Math.Clamp(tx, 0, texture.Width - 1), sy = Math.Clamp(ty, 0, texture.Height - 1);
                Assert.Equal(texture.Rgba.AsSpan((sy * texture.Width + sx) * 4, 4).ToArray(),
                    atlas.Rgba.AsSpan(((y + ty) * atlas.Size + x + tx) * 4, 4).ToArray());
            }
        }
    }

    // ------------------------------------------------------------ one part

    [Fact]
    public void OnePart_RootNodeHoldsTheOneMesh()
    {
        Glb glb = Write(OnePartSample());

        JsonElement node = Assert.Single(Array(glb.Json, "nodes"));
        Assert.Equal("one_part", node.GetProperty("name").GetString());
        Assert.Equal(0, node.GetProperty("mesh").GetInt32());
        Assert.False(node.TryGetProperty("children", out _));
        Assert.False(node.TryGetProperty("translation", out _));
        Assert.Equal(0, glb.Json.GetProperty("scene").GetInt32());
        JsonElement scene = Assert.Single(Array(glb.Json, "scenes"));
        Assert.Equal(new[] { 0 }, Array(scene, "nodes").Select(n => n.GetInt32()));
        JsonElement mesh = Assert.Single(Array(glb.Json, "meshes"));
        Assert.Equal("one_part", mesh.GetProperty("name").GetString());
        Assert.Single(Array(mesh, "primitives"));
    }

    [Fact]
    public void OnePart_AtlasHoldsTheTextureOnceAndOneColourBlock()
    {
        Glb glb = Write(OnePartSample());

        (int Size, byte[] Rgba) atlas = Image(glb, 0);
        Assert.Single(Array(glb.Json, "images"));
        Assert.Equal(16, atlas.Size);
        // The 8 x 4 texture with its border is 12 x 8 at the top-left; the 5 x 5 colour block does not fit beside it, so it starts the next shelf.
        AssertPlaced(atlas, 2, 2, Pattern(8, 4, hole: true));
        AssertPlaced(atlas, 2, 10, new ModelTexture(1, 1, new byte[] { Red.R, Red.G, Red.B, 255 }));
        int covered = 12 * 8 + 5 * 5;
        Assert.Equal(16 * 16 - covered, Enumerable.Range(0, 16 * 16).Count(p => atlas.Rgba.AsSpan(p * 4, 4).ToArray().All(b => b == 0)));

        JsonElement primitive = Primitive(glb, 0, 0);
        Assert.Equal(QuadUvs(glb, primitive, 0, 16), QuadUvs(glb, primitive, 1, 16));
        Assert.Equal(new[] { (2f, 10f), (3f, 10f), (3f, 11f), (2f, 11f) }, QuadUvs(glb, primitive, 2, 16));
    }

    [Fact]
    public void OnePart_UnusedMaterialsAreLeftOut()
    {
        Glb glb = Write(OnePartSample());

        Assert.Equal(new[] { "opaque" }, Array(glb.Json, "materials").Select(m => m.GetProperty("name").GetString()));
    }

    [Fact]
    public void OnePart_OriginIsTheRootNodesTranslation()
    {
        var model = new Model("flyer", new[] { new ModelPart("body", new Vector3(0, 2, -1), new[] { Square(0, 0, null) }) });

        JsonElement node = Assert.Single(Array(Write(model).Json, "nodes"));

        Assert.Equal(new[] { 0f, 2f, -1f }, Array(node, "translation").Select(n => n.GetSingle()));
    }

    // ------------------------------------------------------------ parts

    [Fact]
    public void Parts_RootHasThreeNamedChildrenWithTranslationAndMesh()
    {
        Glb glb = Write(PartsSample());

        JsonElement[] nodes = Array(glb.Json, "nodes");
        Assert.Equal(4, nodes.Length);
        Assert.Equal("parts", nodes[0].GetProperty("name").GetString());
        Assert.False(nodes[0].TryGetProperty("mesh", out _));
        Assert.Equal(new[] { 1, 2, 3 }, Array(nodes[0], "children").Select(c => c.GetInt32()));
        Assert.Equal(new[] { "body", "left_wing", "right_wing" }, nodes.Skip(1).Select(n => n.GetProperty("name").GetString()));
        Assert.Equal(new[] { 0, 1, 2 }, nodes.Skip(1).Select(n => n.GetProperty("mesh").GetInt32()));
        Assert.Equal(new[] { 0f, 0.5f, 0f }, Array(nodes[1], "translation").Select(n => n.GetSingle()));
        Assert.Equal(new[] { -1f, 0f, 0.25f }, Array(nodes[2], "translation").Select(n => n.GetSingle()));
        Assert.Equal(new[] { 1f, 0f, 0.25f }, Array(nodes[3], "translation").Select(n => n.GetSingle()));
        Assert.Equal(new[] { "body", "left_wing", "right_wing" }, Array(glb.Json, "meshes").Select(m => m.GetProperty("name").GetString()));
    }

    // ------------------------------------------------------------ all four modes

    [Fact]
    public void AllFourModes_PrimitivesInOrderEachWithItsMaterial()
    {
        Model model = OneModel(
            Square(0, 0, null, DrawingMode.Mesh),
            Square(1, 0, null, DrawingMode.Half),
            Square(2, 0, null, DrawingMode.Cutout),
            Square(3, 0, null, DrawingMode.Opaque),
            Square(4, 0, null, DrawingMode.Cutout));

        Glb glb = Write(model);

        JsonElement[] primitives = Array(Array(glb.Json, "meshes")[0], "primitives");
        Assert.Equal(new[] { "opaque", "cutout", "half", "mesh" }, primitives.Select(p => MaterialName(glb, p)));
        Assert.Equal(new[] { 1, 2, 1, 1 }, primitives.Select(p => Array(glb.Json, "accessors")[p.GetProperty("indices").GetInt32()].GetProperty("count").GetInt32() / 6));
        // The cutout primitive keeps its quads in the model's order: x = 2 then x = 4.
        float[] cutout = Floats(glb, primitives[1].GetProperty("attributes").GetProperty("POSITION").GetInt32());
        Assert.Equal(2f, cutout[0]);
        Assert.Equal(4f, cutout[12]);
    }

    [Fact]
    public void AllFourModes_MaterialsHaveTheTablesSettings()
    {
        Glb glb = Write(OneModel(Square(0, 0, null, DrawingMode.Opaque), Square(0, 0, null, DrawingMode.Cutout),
            Square(0, 0, null, DrawingMode.Half), Square(0, 0, null, DrawingMode.Mesh)));

        JsonElement[] materials = Array(glb.Json, "materials");
        Assert.Equal(new[] { "opaque", "cutout", "half", "mesh" }, materials.Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(new[] { "OPAQUE", "MASK", "BLEND", "BLEND" }, materials.Select(m => m.GetProperty("alphaMode").GetString()));
        Assert.Equal(0.5, materials[1].GetProperty("alphaCutoff").GetDouble());
        foreach (JsonElement m in materials)
        {
            Assert.True(m.GetProperty("doubleSided").GetBoolean());
            JsonElement pbr = m.GetProperty("pbrMetallicRoughness");
            Assert.Equal(0, pbr.GetProperty("baseColorTexture").GetProperty("index").GetInt32());
            Assert.Equal(0, pbr.GetProperty("metallicFactor").GetDouble());
            Assert.Equal(JsonValueKind.Object, m.GetProperty("extensions").GetProperty("KHR_materials_unlit").ValueKind);
            Assert.Single(m.GetProperty("extensions").EnumerateObject());
            bool blended = m.GetProperty("name").GetString() is "half" or "mesh";
            Assert.Equal(blended, pbr.TryGetProperty("baseColorFactor", out JsonElement factor));
            if (blended)
            {
                Assert.Equal(new[] { 1.0, 1.0, 1.0, 0.5 }, factor.EnumerateArray().Select(f => f.GetDouble()));
            }

            Assert.False(m.TryGetProperty("alphaCutoff", out _) && m.GetProperty("name").GetString() != "cutout");
        }

        Assert.Equal(new[] { "KHR_materials_unlit" }, Array(glb.Json, "extensionsUsed").Select(e => e.GetString()));
        Assert.False(glb.Json.TryGetProperty("extensionsRequired", out _));
    }

    // ------------------------------------------------------------ flipped

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Flipped_SameAtlasPixelsUvCornersSwapped(bool flipH, bool flipV)
    {
        ModelTexture texture = Pattern(8, 4, hole: true);
        Glb plain = Write(OneModel(Square(0, 0, texture)));
        Glb flipped = Write(OneModel(Square(0, 0, texture, flipH: flipH, flipV: flipV)));

        Assert.Equal(Image(plain, 0).Rgba, Image(flipped, 0).Rgba);
        float l = 2, r = 10, t = 2, b = 6;
        if (flipH)
        {
            (l, r) = (r, l);
        }

        if (flipV)
        {
            (t, b) = (b, t);
        }

        Assert.Equal(new[] { (l, t), (r, t), (r, b), (l, b) }, QuadUvs(flipped, Primitive(flipped, 0, 0), 0, 16));
    }

    // ------------------------------------------------------------ triangle

    [Fact]
    public void Triangle_IsWrittenAsFourVerticesLikeAnyQuad()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(1, 0, 0);
        var c = new Vector3(0, -1, 0);
        Glb glb = Write(OneModel(new ModelQuad(a, b, c, c, Pattern(8, 4), default, DrawingMode.Opaque)));

        JsonElement primitive = Primitive(glb, 0, 0);
        Assert.Equal(new float[] { 0, 0, 0, 1, 0, 0, 0, -1, 0, 0, -1, 0 },
            Floats(glb, primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32()));
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, Indices(glb, primitive.GetProperty("indices").GetInt32()));
    }

    [Fact]
    public void Quads_EachHasFourVerticesOfItsOwnAndTwoTriangles()
    {
        Glb glb = Write(OneModel(Square(0, 0, null), Square(0, 0, null), Square(5, 0, null)));

        JsonElement primitive = Primitive(glb, 0, 0);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7, 8, 9, 10, 8, 10, 11 }, Indices(glb, primitive.GetProperty("indices").GetInt32()));
        float[] positions = Floats(glb, primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32());
        Assert.Equal(new float[] { 5, 0, 0, 6, 0, 0, 6, -1, 0, 5, -1, 0 }, positions[24..]);
        Assert.Equal(4, primitive.GetProperty("mode").GetInt32());
        Assert.Equal(new[] { "POSITION", "TEXCOORD_0" }, primitive.GetProperty("attributes").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Quads_MoreThan65535VerticesUseUnsignedIntIndices()
    {
        Glb small = Write(OneModel(Enumerable.Range(0, 16383).Select(i => Square(i, 0, null)).ToArray()));
        Glb large = Write(OneModel(Enumerable.Range(0, 16384).Select(i => Square(i, 0, null)).ToArray()));

        Assert.Equal(5123, Array(small.Json, "accessors")[Primitive(small, 0, 0).GetProperty("indices").GetInt32()].GetProperty("componentType").GetInt32());
        int[] indices = Indices(large, Primitive(large, 0, 0).GetProperty("indices").GetInt32());
        Assert.Equal(5125, Array(large.Json, "accessors")[Primitive(large, 0, 0).GetProperty("indices").GetInt32()].GetProperty("componentType").GetInt32());
        Assert.Equal(65535, indices[^1]);
    }

    // ------------------------------------------------------------ too many textures

    [Fact]
    public void TooManyTextures_SecondAtlasImageTextureAndMaterials()
    {
        Glb glb = Write(TwoAtlasesSample());

        Assert.Equal(2, Array(glb.Json, "images").Length);
        Assert.Equal(new[] { 0, 1 }, Array(glb.Json, "textures").Select(t => t.GetProperty("source").GetInt32()));
        Assert.All(Array(glb.Json, "textures"), t => Assert.Equal(0, t.GetProperty("sampler").GetInt32()));
        Assert.Equal(2048, Image(glb, 0).Size);
        Assert.Equal(16, Image(glb, 1).Size);
        Assert.Equal(new[] { "opaque", "opaque_2", "cutout_2" }, Array(glb.Json, "materials").Select(m => m.GetProperty("name").GetString()));
        Assert.Equal(new[] { 0, 1, 1 }, Array(glb.Json, "materials").Select(m =>
            m.GetProperty("pbrMetallicRoughness").GetProperty("baseColorTexture").GetProperty("index").GetInt32()));
        Assert.Equal(new[] { "opaque", "opaque_2", "cutout_2" }, Array(Array(glb.Json, "meshes")[0], "primitives").Select(p => MaterialName(glb, p)));
        AssertPlaced(Image(glb, 0), 2, 2, Pattern(MaxTexture, MaxTexture, seed: 3));
        AssertPlaced(Image(glb, 1), 2, 2, new ModelTexture(1, 1, new byte[] { Blue.R, Blue.G, Blue.B, 255 }));
        // The colour block is 5 x 5 at the top-left; the texture, 12 x 8 with its border, does not fit beside it in 16.
        AssertPlaced(Image(glb, 1), 2, 7, Pattern(8, 4, hole: true));
    }

    [Fact]
    public void Atlas_IsTheSmallestPowerOfTwoThatHoldsItsSet()
    {
        Assert.Equal(16, Image(Write(OneModel(Square(0, 0, Pattern(12, 12)))), 0).Size);
        Assert.Equal(32, Image(Write(OneModel(Square(0, 0, Pattern(13, 1)))), 0).Size);
        Assert.Equal(2048, Image(Write(OneModel(Square(0, 0, Pattern(1, MaxTexture)))), 0).Size);
        // Two 12 x 12 textures need 32 x 16 with their borders, so a 32 x 32 atlas.
        Assert.Equal(32, Image(Write(OneModel(Square(0, 0, Pattern(12, 12)), Square(0, 0, Pattern(12, 12, seed: 1)))), 0).Size);
    }

    // ------------------------------------------------------------ bad models

    public static IEnumerable<object[]> BadModels()
    {
        ModelQuad ok = Square(0, 0, null);
        yield return new object[] { new Model("flyer", System.Array.Empty<ModelPart>()), "the model has no parts" };
        yield return new object[] { new Model("flyer", new[] { Part("body") }), "part 'body' has no quads" };
        yield return new object[] { new Model("Flyer", new[] { Part("body", ok) }), "the model's name 'Flyer' is not only lowercase letters, digits and underscores" };
        yield return new object[] { new Model("", new[] { Part("body", ok) }), "the model's name is empty" };
        yield return new object[] { new Model("flyer", new[] { Part("left wing", ok) }), "part 0's name 'left wing' is not only lowercase letters, digits and underscores" };
        yield return new object[] { new Model("flyer", new[] { Part("wing", ok), Part("wing", ok) }), "part 1's name 'wing' is already used in the model" };
        yield return new object[] { new Model("flyer", new[] { Part("flyer", ok), Part("wing", ok) }), "part 0's name 'flyer' is already used in the model" };
        yield return new object[] { OneModel(Square(0, 0, Pattern(MaxTexture + 1, 1))), "part 'body' quad 0 has a 2045 x 1 texture; a texture is 1 to 2044 pixels each way" };
        yield return new object[] { OneModel(ok, Square(0, 0, Pattern(1, MaxTexture + 1))), "part 'body' quad 1 has a 1 x 2045 texture; a texture is 1 to 2044 pixels each way" };
        yield return new object[] { OneModel(Square(0, 0, new ModelTexture(0, 4, System.Array.Empty<byte>()))), "part 'body' quad 0 has a 0 x 4 texture; a texture is 1 to 2044 pixels each way" };
        yield return new object[] { OneModel(Square(0, 0, new ModelTexture(2, 2, new byte[15]))), "part 'body' quad 0 has a 2 x 2 texture of 15 bytes; it needs 16" };
        yield return new object[] { OneModel(Square(float.NaN, 0, null)), "part 'body' quad 0 corner A is not a finite position" };
        yield return new object[] { OneModel(Square(0, 0, null, (DrawingMode)4)), "part 'body' quad 0 has drawing mode 4, which is not one of opaque, cutout, half and mesh" };
    }

    [Theory]
    [MemberData(nameof(BadModels))]
    public void BadModel_ThrowsNamingTheProblem(Model model, string problem)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => GlbWriter.Write(model));

        Assert.Equal(problem, e.Message);
    }

    [Fact]
    public void OnePart_PartMayShareTheRootsName()
    {
        // With one part the part is not a node, so its name cannot clash.
        Assert.NotEmpty(GlbWriter.Write(new Model("body", new[] { Part("body", Square(0, 0, null)) })));
    }

    // ------------------------------------------------------------ the rest of the contract

    [Theory]
    [MemberData(nameof(Samples))]
    public void Sampler_IsOneNearestClampSampler(string sample)
    {
        Glb glb = Write(Sample(sample));

        JsonElement sampler = Assert.Single(Array(glb.Json, "samplers"));
        Assert.Equal(9728, sampler.GetProperty("magFilter").GetInt32());
        Assert.Equal(9728, sampler.GetProperty("minFilter").GetInt32());
        Assert.Equal(33071, sampler.GetProperty("wrapS").GetInt32());
        Assert.Equal(33071, sampler.GetProperty("wrapT").GetInt32());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Structure_NoUriNormalsOrVertexColours(string sample)
    {
        byte[] file = GlbWriter.Write(Sample(sample));
        Glb glb = Read(file);

        Assert.DoesNotContain("uri", Encoding.UTF8.GetString(file, 20, (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(12))));
        JsonElement buffer = Assert.Single(Array(glb.Json, "buffers"));
        Assert.Equal(glb.Bin.Length, buffer.GetProperty("byteLength").GetInt32());
        foreach (JsonElement mesh in Array(glb.Json, "meshes"))
        {
            foreach (JsonElement primitive in Array(mesh, "primitives"))
            {
                Assert.False(primitive.GetProperty("attributes").TryGetProperty("NORMAL", out _));
                Assert.False(primitive.GetProperty("attributes").TryGetProperty("COLOR_0", out _));
            }
        }

        foreach (string field in new[] { "animations", "skins", "cameras", "extensionsRequired" })
        {
            Assert.False(glb.Json.TryGetProperty(field, out _));
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Accessors_PositionMinMaxAreTheExactBounds(string sample)
    {
        Glb glb = Write(Sample(sample));

        foreach (JsonElement mesh in Array(glb.Json, "meshes"))
        {
            foreach (JsonElement primitive in Array(mesh, "primitives"))
            {
                int accessor = primitive.GetProperty("attributes").GetProperty("POSITION").GetInt32();
                float[] p = Floats(glb, accessor);
                JsonElement a = Array(glb.Json, "accessors")[accessor];
                Assert.Equal(p.Length / 3, a.GetProperty("count").GetInt32());
                for (int axis = 0; axis < 3; axis++)
                {
                    float[] values = p.Where((_, i) => i % 3 == axis).ToArray();
                    Assert.Equal((double)values.Min(), Array(a, "min")[axis].GetDouble());
                    Assert.Equal((double)values.Max(), Array(a, "max")[axis].GetDouble());
                }
            }
        }
    }

    [Fact]
    public void Accessors_MinMaxOfAnInexactFloatIsItsExactDouble()
    {
        // 0.1f is 0.100000001490116119384765625 exactly; written as that double, not as "0.1".
        Glb glb = Write(OneModel(Square(0.1f, 0, null)));

        JsonElement a = Array(glb.Json, "accessors")[Primitive(glb, 0, 0).GetProperty("attributes").GetProperty("POSITION").GetInt32()];
        Assert.Equal("0.10000000149011612", Array(a, "min")[0].GetRawText());
        Assert.Equal((double)0.1f, Array(a, "min")[0].GetDouble());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Write_TwiceGivesTheSameBytes(string sample)
    {
        Assert.Equal(GlbWriter.Write(Sample(sample)), GlbWriter.Write(Sample(sample)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Write_SampleEqualsTheRecordedFile(string sample)
    {
        byte[] written = GlbWriter.Write(Sample(sample));

        // tools/validate-gltf.sh sets this to have the samples written for the validator.
        if (Environment.GetEnvironmentVariable("PDZ_GLB_SAMPLES") is { Length: > 0 } samples)
        {
            Directory.CreateDirectory(samples);
            File.WriteAllBytes(Path.Combine(samples, sample + ".glb"), written);
        }

        // A sample with a full 2048 x 2048 atlas is 16 MB of stored PNG, too large to commit, so only its SHA-256 is recorded.
        bool whole = written.Length <= RecordedWholeBytes;
        string name = sample + (whole ? ".glb" : ".glb.sha256");
        byte[] recorded = whole ? written : Encoding.ASCII.GetBytes(Convert.ToHexString(SHA256.HashData(written)).ToLowerInvariant() + "\n");
        if (Environment.GetEnvironmentVariable("PDZ_RECORD_EXPECTED") == "1")
        {
            string folder = Path.Combine(SourceFolder(), "expected", "models");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name), recorded);
            return;
        }

        Assert.Equal(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "expected", "models", name)), recorded);
    }

    /// <summary>The largest sample recorded as a whole file; a larger one is recorded as its SHA-256.</summary>
    private const int RecordedWholeBytes = 1 << 20;

    private static string SourceFolder([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
