using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Xunit;
using static PdzDecoding.Tests.SyntheticDragon;
using static PdzDecoding.Tests.SyntheticTextures;

namespace PdzDecoding.Tests;

/// <summary>
/// The dragon forms' geometry, a test or more per row of the story's matrix (both discs, a form's file
/// missing, no table, malformed geometry, a texture that cannot be decoded, a flat polygon, three corners),
/// then matrix order, flips, the axis conversion, the origin and the parts. Every <c>.MDB</c>, <c>.CGB</c>
/// and <c>1ST_READ</c> is built here; nothing comes from the disc.
/// </summary>
public sealed class DragonsTests : IDisposable
{
    private static readonly Seat NoSeat = new(0, 0, 0);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pdz-dragons-" + Guid.NewGuid().ToString("N"));

    public DragonsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static DecodedDragon Decode(TestNode root, Seat seat = default) => Dragons.Decode(Mdb(root), Cgb(), "form01", seat);

    private string DiscWithForms(params string[] without) =>
        WithForms(new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb()), without)
            .WriteCue(_folder);

    private ExtractionResult Run(string cue) => Extraction.Run(cue, Path.Combine(_folder, "out"));

    private string Output(string path) => Path.Combine(_folder, "out", path);

    // ------------------------------------------------------------ the matrix: both discs

    [Theory]
    [InlineData(0x123)]
    [InlineData(0x2000)]
    public void Run_TableAtAnyOffset_WritesTenFormsListedAsModels(int lead)
    {
        var disc = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb());
        var roots = Dragons.FormFiles.Select((_, i) => RootAddress(Form(i), Padding(i))).ToList();
        for (int i = 0; i < 10; i++)
        {
            disc.With(Dragons.FormFiles[i] + ".MDB", Mdb(Form(i), Padding(i))).With(Dragons.FormFiles[i] + ".CGB", Cgb());
        }

        disc.With(Dragons.SeatFile, FirstRead(roots, lead));

        ExtractionResult result = Run(disc.WriteCue(_folder));

        Assert.Equal(Enumerable.Range(1, 10).Select(n => $"dragon.form{n:D2}"), result.ModelFiles.Select(f => f.Id));
        Assert.Equal(Enumerable.Range(1, 10).Select(n => $"models/dragon_form{n:D2}.glb"), result.ModelFiles.Select(f => f.Path));
        Assert.Equal(lead + Dragons.SeatTableGap, result.DragonSources!.SeatTable);
        Assert.Null(result.DragonNotice);
        Assert.False(Directory.Exists(Output(Extraction.StagingFolder)));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Output(Manifest.FileName)));
        JsonElement[] models = manifest.RootElement.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("kind").GetString() == "model").ToArray();
        Assert.Equal(10, models.Length);
        Assert.Equal("dragon.form01", models[0].GetProperty("id").GetString());
        Assert.Equal("models/dragon_form01.glb", models[0].GetProperty("path").GetString());
        Assert.Equal(new FileInfo(Output("models/dragon_form01.glb")).Length, models[0].GetProperty("bytes").GetInt64());
    }

    [Fact]
    public void Run_EachForm_HasItsOwnSeatInItsOrigin()
    {
        Run(DiscWithForms());

        // Form index 8 is DRA09I, second in the game's order, so a mix-up of the orders moves it.
        for (int i = 0; i < 10; i++)
        {
            Seat seat = SeatOf(i);
            byte[] expected = GlbWriter.Write(Dragons.Decode(Mdb(Form(i), Padding(i)), Cgb(), $"form{i + 1:D2}", seat).Model);
            Assert.Equal(expected, File.ReadAllBytes(Output($"models/dragon_form{i + 1:D2}.glb")));
        }
    }

    [Fact]
    public void Run_Twice_WritesTheSameBytes()
    {
        string cue = DiscWithForms();
        Run(cue);
        byte[] first = File.ReadAllBytes(Output("models/dragon_form03.glb"));
        Run(cue);

        Assert.Equal(first, File.ReadAllBytes(Output("models/dragon_form03.glb")));
    }

    [Fact]
    public void Run_WithDragons_LeavesThePngsAsTheyWere()
    {
        ExtractionResult with = Run(DiscWithForms());
        string png = Output(with.Files.Single().Path);
        byte[] bytes = File.ReadAllBytes(png);
        File.Delete(png);

        var other = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb()).WriteCue(_folder, "Other.bin", "Other.cue");
        Run(other);

        Assert.Equal(bytes, File.ReadAllBytes(png));
    }

    [Fact]
    public void Run_WithDragons_SaysSoInTheDetails()
    {
        ExtractionResult result = Run(DiscWithForms());

        Assert.Contains("; 10 dragon forms, seats from 0x173 of 1ST_READ.PRG", result.Details());
        Assert.DoesNotContain("grey", result.Details());
    }

    // ------------------------------------------------------------ the matrix: no dragons

    [Theory]
    [InlineData("DRA05E.MDB")]
    [InlineData("DRA10J.CGB")]
    public void Run_FormFileMissing_NoDragonsAndTheRestWritten(string absent)
    {
        ExtractionResult result = Run(DiscWithForms(absent));

        Assert.Empty(result.ModelFiles);
        Assert.Null(result.DragonSources);
        Assert.Equal($"the disc has no {absent}", result.DragonNotice);
        Assert.Contains($"no dragons: the disc has no {absent}", result.Details());
        Assert.Single(result.Files);
        Assert.False(Directory.Exists(Output("models")));
    }

    [Fact]
    public void Run_FirstReadAbsent_NoDragonsAndTheRestWritten()
    {
        ExtractionResult result = Run(DiscWithForms(Dragons.SeatFile));

        Assert.Empty(result.ModelFiles);
        Assert.Equal("the disc has no 1ST_READ.PRG", result.DragonNotice);
        Assert.Single(result.Files);
    }

    [Fact]
    public void Run_NoMatchInFirstRead_NoDragonsAndTheRestWritten()
    {
        var disc = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb());
        WithForms(disc, Dragons.SeatFile).With(Dragons.SeatFile, new byte[0x200]);

        ExtractionResult result = Run(disc.WriteCue(_folder));

        Assert.Empty(result.ModelFiles);
        Assert.Equal("1ST_READ.PRG: it holds no table of the 10 forms' body roots", result.DragonNotice);
        Assert.Single(result.Files);
    }

    [Fact]
    public void FindSeats_TableRunsOffTheEnd_Throws()
    {
        var roots = Enumerable.Range(0, 10).Select(i => Load + (uint)(i * 12)).ToArray();
        byte[] whole = FirstRead(roots);

        var e = Assert.Throws<DecodingException>(() => Dragons.FindSeats(whole.AsSpan(0, whole.Length - 16 - 1).ToArray(), roots));

        Assert.Equal("the seat table would run off the end of the file", e.Message);
    }

    [Fact]
    public void Run_MalformedGeometry_NoDragonsAndTheRestWritten()
    {
        var disc = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb());
        WithForms(disc, "DRA03C.MDB").With("DRA03C.MDB", Mdb(new TestNode(Move(0, 0, 0), new TestMesh(Square, new[] { Textured(d: 9) })), Padding(2)));

        ExtractionResult result = Run(disc.WriteCue(_folder));

        Assert.Empty(result.ModelFiles);
        Assert.StartsWith("DRA03C: the polygon at 0x", result.DragonNotice);
        Assert.EndsWith("has corner index 9 of a mesh with 4 vertices", result.DragonNotice);
        Assert.DoesNotContain('\n', result.DragonNotice!);
        Assert.Single(result.Files);
    }

    [Fact]
    public void Decode_CornerIndexOutOfRange_Throws()
    {
        var root = new TestNode(Move(0, 0, 0), new TestMesh(Square, new[] { Textured(a: 4) }));

        Assert.Throws<DecodingException>(() => Decode(root));
    }

    [Fact]
    public void Decode_MeshPointerOutOfRange_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh()));
        int root = Dragons.BodyRoot(mdb);
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan(root), Load + (uint)mdb.Length - 8);

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", NoSeat));

        Assert.Contains("mesh pointer", e.Message);
    }

    [Fact]
    public void Decode_VertexListOutOfRange_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh()));
        int mesh = (int)(BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(Dragons.BodyRoot(mdb))) - Load);
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan(mesh + 4), 0xFFFFFF00); // A vertex count larger than the file.

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", NoSeat));

        Assert.Contains("vertex list pointer", e.Message);
    }

    [Fact]
    public void Decode_NodeReachedTwice_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh(), new TestNode(Move(0, 0, 0), null)));
        uint child = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(Dragons.BodyRoot(mdb) + 4));
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan((int)(child - Load) + 8), child); // The child is its own sibling.

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", NoSeat));

        Assert.EndsWith("is reached twice", e.Message);
    }

    [Fact]
    public void Decode_MatricesRunOffTheEnd_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh()));
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan(Dragons.BodyRoot(mdb)), Load + (uint)mdb.Length - 40); // A last mesh too near the end.

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", NoSeat));

        Assert.EndsWith("run off the end of the file", e.Message);
    }

    [Fact]
    public void Decode_TreeWithNoMesh_Throws()
    {
        var e = Assert.Throws<DecodingException>(() => Decode(new TestNode(Move(0, 0, 0), null, new TestNode(Move(1, 0, 0), null))));

        Assert.Equal("the body tree holds no mesh", e.Message);
    }

    // ------------------------------------------------------------ the matrix: how polygons are drawn

    [Fact]
    public void Decode_ColourModeNeedingColourRam_IsFlatGreyAndCounted()
    {
        (ushort _, ushort colr, ushort srca, ushort size) = Record();
        var mode4 = new TestPolygon(0, 1, 2, 3, Mode4, colr, srca, size);
        var mode1 = Textured();

        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), SquareMesh(mode4, mode1, mode4)));

        ModelQuad[] quads = dragon.Model.Parts[0].Quads.ToArray();
        Assert.Equal(2, dragon.GreyPolygons);
        Assert.Null(quads[0].Texture);
        Assert.Equal(Dragons.Grey, quads[0].Colour);
        Assert.NotNull(quads[1].Texture);
    }

    [Fact]
    public void Decode_TextureOutsideTheCgb_IsFlatGreyAndCounted()
    {
        (ushort _, ushort colr, ushort _, ushort size) = Record();
        var outside = new TestPolygon(0, 1, 2, 3, 0x04CC, colr, 0xF000, size);

        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), SquareMesh(Textured(), outside)));

        Assert.Equal(1, dragon.GreyPolygons);
        Assert.Equal(Dragons.Grey, dragon.Model.Parts[0].Quads[1].Colour);
    }

    [Fact]
    public void DragonSources_Describe_NamesTheGreyCount()
    {
        Assert.Equal("10 dragon forms, seats from 0x54840 of 1ST_READ.PRG, 3 polygons drawn flat grey", new DragonSources(10, 0x54840, 3).Describe());
        Assert.Equal("10 dragon forms, seats from 0x54840 of 1ST_READ.PRG, 1 polygon drawn flat grey", new DragonSources(10, 0x54840, 1).Describe());
    }

    [Fact]
    public void Decode_FlatPolygon_IsItsRgb555ColourWithNoTexture()
    {
        // Red 31, green 0, blue 8, with the top bit set.
        ushort colour = 0x8000 | 31 | (8 << 10);

        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), SquareMesh(Flat(colour), Textured())));

        ModelQuad quad = dragon.Model.Parts[0].Quads[0];
        Assert.Null(quad.Texture);
        Assert.Equal(new ModelColour(255, 0, 65), quad.Colour);
        Assert.Equal(DrawingMode.Opaque, quad.Mode);
        Assert.Equal(0, dragon.GreyPolygons);
    }

    [Fact]
    public void Decode_ThreeCorners_KeepsTheRepeat()
    {
        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), new TestMesh(Square, new[] { Textured(d: 2) })));

        ModelQuad quad = dragon.Model.Parts[0].Quads[0];
        Assert.Equal(quad.C, quad.D);
        Assert.NotEqual(quad.A, quad.D);
    }

    [Fact]
    public void Decode_Texture_IsTheModeOneDecodingOfThePolygonsWords()
    {
        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), SquareMesh()));

        ModelTexture texture = dragon.Model.Parts[0].Quads[0].Texture!;
        TextureRecord r = Textures.FindRecords(Mdb(Record())).Single();
        Assert.Equal((8, 2), (texture.Width, texture.Height));
        Assert.Equal(Textures.DecodeMode1(Cgb(), Base, r with { Pmod = 0x04CC }), texture.Rgba);
    }

    [Theory]
    [InlineData(0x04CC, DrawingMode.Opaque)]
    [InlineData(0x048C, DrawingMode.Cutout)]
    [InlineData(0x04CB, DrawingMode.Half)]
    [InlineData(0x04CF, DrawingMode.Half)]
    [InlineData(0x05CC, DrawingMode.Mesh)]
    public void Decode_DrawingMode_ComesFromCmdpmod(int pmod, DrawingMode mode)
    {
        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), SquareMesh(Textured(pmod: (ushort)pmod))));

        Assert.Equal(mode, dragon.Model.Parts[0].Quads[0].Mode);
    }

    [Theory]
    [InlineData(0x02, false, false)]
    [InlineData(0x12, true, false)]
    [InlineData(0x22, false, true)]
    [InlineData(0x32, true, true)]
    public void Decode_Flips_ComeFromTheFlagWordAndLeaveTheCornerOrder(byte flags, bool flipH, bool flipV)
    {
        DecodedDragon dragon = Decode(new TestNode(Move(0, 0, 0), SquareMesh(Textured(flags: flags))));

        ModelQuad quad = dragon.Model.Parts[0].Quads[0];
        Assert.Equal((flipH, flipV), (quad.FlipH, quad.FlipV));
        Assert.Equal(new Vector3(0.1f, 0.1f, 0), quad.A);
        Assert.Equal(new Vector3(-0.1f, 0.1f, 0), quad.B);
    }

    // ------------------------------------------------------------ axes, origin, matrices, parts

    [Fact]
    public void Decode_Axes_AreAHalfTurnAboutYThenMetres()
    {
        var mesh = new TestMesh(new[] { (4000, 8000, -12000), (0, 0, 0), (0, 0, 0), (0, 0, 0) }, new[] { Textured() });

        DecodedDragon dragon = Decode(new TestNode(Move(2000, 4000, 6000), mesh));

        ModelPart part = dragon.Model.Parts[0];
        Assert.Equal(new Vector3(-0.5f, 1, -1.5f), part.Origin);
        Assert.Equal(new Vector3(-1, 2, 3), part.Quads[0].A);
    }

    [Fact]
    public void Decode_AxisConversion_LeavesNoNegativeZero()
    {
        DecodedDragon dragon = Decode(new TestNode(Move(0, 400, 0), SquareMesh()));

        Vector3 origin = dragon.Model.Parts[0].Origin;
        Assert.Equal(0f, origin.X);
        Assert.False(float.IsNegative(origin.X));
        Assert.False(float.IsNegative(origin.Z));
    }

    [Fact]
    public void Decode_Seat_IsSubtractedFromOriginsAndNotFromCorners()
    {
        var root = new TestNode(Move(0, 8000, 4000), SquareMesh());

        ModelPart without = Decode(root).Model.Parts[0];
        ModelPart with = Decode(root, new Seat(4000, 4000, 8000)).Model.Parts[0];

        Assert.Equal(new Vector3(0, 2, -1), without.Origin);
        Assert.Equal(new Vector3(1, 1, 1), with.Origin);
        Assert.Equal(without.Quads[0].A, with.Quads[0].A);
        Assert.Equal(without.Quads[0].C, with.Quads[0].C);
    }

    [Fact]
    public void Decode_MatrixOrder_IsParentTimesChild()
    {
        // The parent turns and moves; the child moves along what is, after the parent's turn, another axis.
        // Parent then child takes the child's +X 4000 to -Z, so to (1000, 0, -4000), and glTF to (-0.25, 0, 1).
        // The other order would give (4000 + 1000, 0, 0).
        var root = new TestNode(QuarterTurnY(1000, 0, 0), null, new TestNode(Move(4000, 0, 0), SquareMesh()));

        ModelPart part = Decode(root).Model.Parts.Single();

        Assert.Equal(new Vector3(-0.25f, 0, 1), part.Origin);
    }

    [Fact]
    public void Decode_ThreeLevels_MultiplyFromTheRootDown()
    {
        var root = new TestNode(Move(4000, 0, 0), null,
            new TestNode(QuarterTurnY(), null,
                new TestNode(Move(0, 0, 4000), SquareMesh())));

        // The quarter turn takes the last +Z 4000 to +X 4000, added to the root's.
        Assert.Equal(new Vector3(-2, 0, 0), Decode(root).Model.Parts.Single().Origin);
    }

    [Fact]
    public void Decode_Corners_TakeTheTurnOfTheProductButNotItsMove()
    {
        var mesh = new TestMesh(new[] { (4000, 0, 0), (0, 0, 0), (0, 0, 0), (0, 0, 0) }, new[] { Textured() });

        ModelPart part = Decode(new TestNode(QuarterTurnY(8000, 0, 0), mesh)).Model.Parts[0];

        // +X 4000 turns to -Z 4000, which glTF makes +Z 1.
        Assert.Equal(new Vector3(0, 0, 1), part.Quads[0].A);
        Assert.Equal(new Vector3(-2, 0, 0), part.Origin);
    }

    [Fact]
    public void Decode_FractionalMatrix_UsesIntegerArithmetic()
    {
        // A third in 16.16: the sum of the three products is shifted once, so the fractions add up.
        int third = 0x5555;
        int[] scale = { third, third, third, 0, 0, One, 0, 0, 0, 0, One, 0 };
        var mesh = new TestMesh(new[] { (1, 1, 2), (0, 0, 0), (0, 0, 0), (0, 0, 0) }, new[] { Textured() });

        ModelPart part = Decode(new TestNode(scale, mesh)).Model.Parts[0];

        // 4 * 0x5555 >> 16 is 1, where shifting each product alone would give 0.
        Assert.Equal(-1f / Dragons.UnitsPerMetre, part.Quads[0].A.X);
    }

    [Fact]
    public void Decode_Parts_AreMeshNodesNamedByDepthFirstIndex()
    {
        // 0 root; 1 joint, 2 under it; 3 sibling of 1.
        var root = new TestNode(Move(0, 0, 0), SquareMesh(),
            new TestNode(Move(0, 0, 0), null,
                new TestNode(Move(0, 0, 0), SquareMesh())),
            new TestNode(Move(0, 0, 0), SquareMesh()));

        Assert.Equal(new[] { "node00", "node02", "node03" }, Decode(root).Model.Parts.Select(p => p.Name));
    }

    [Fact]
    public void Decode_MatricesFollowTheLastMesh_ByDepthFirstIndex()
    {
        var root = new TestNode(Move(4000, 0, 0), null,
            new TestNode(Move(0, 4000, 0), SquareMesh()),
            new TestNode(Move(0, 0, 4000), SquareMesh()));

        ModelPart[] parts = Decode(root).Model.Parts.ToArray();

        Assert.Equal(new Vector3(-1, 1, 0), parts[0].Origin);
        Assert.Equal(new Vector3(-1, 0, -1), parts[1].Origin);
    }

    [Fact]
    public void BodyRoot_IsTheFirstNodeNothingPointsAt_NotTheShadowRoot()
    {
        var root = new TestNode(Move(0, 0, 0), SquareMesh());

        Assert.Equal(RootAddress(root) - Load, (uint)Dragons.BodyRoot(Mdb(root)));
        Assert.Equal(new[] { "node00" }, Decode(root).Model.Parts.Select(p => p.Name));
    }

    [Fact]
    public void Write_DecodedModel_PassesThroughTheWriterWithPartsAndName()
    {
        var root = new TestNode(Move(0, 0, 4000), SquareMesh(), new TestNode(Move(0, 0, 4000), SquareMesh(Flat(0x801F))));

        byte[] glb = GlbWriter.Write(Decode(root).Model);

        int length = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        using JsonDocument json = JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, length));
        string[] names = json.RootElement.GetProperty("nodes").EnumerateArray().Select(n => n.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(new[] { "form01", "node00", "node01" }, names);
    }

    [Fact]
    public void Manifest_ModelKind_IsWrittenAsIs()
    {
        byte[] written = Manifest.Write(new SaturnHeader("SEGA SEGASATURN", "SEGA", "GS-9049", "V1.001", "", ""), new[] { new ManifestEntry("dragon.form01", "models/dragon_form01.glb", new byte[] { 1 }, Manifest.ModelKind) });

        Assert.Contains("\"kind\": \"model\",", Encoding.UTF8.GetString(written));
    }
}
