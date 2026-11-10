using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;
using static PdzDecoding.Tests.SyntheticDragon;
using static PdzDecoding.Tests.SyntheticTextures;

namespace PdzDecoding.Tests;

/// <summary>
/// The dragon forms' geometry, a test or more per row of the story's matrix (both discs, a form's file
/// missing, no table, malformed geometry, a model the writer rejects, a texture that cannot be decoded, a flat polygon, three corners),
/// then matrix order, flips, the axis conversion, the origin and the parts, then the wing beat (both discs,
/// the motion unreadable, its keys). Every <c>.MDB</c>, <c>.CGB</c>
/// and <c>1ST_READ</c> is built here; nothing comes from the disc.
/// </summary>
public sealed class DragonsTests : IDisposable
{
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

        Assert.Equal(Enumerable.Range(1, 10).Select(n => $"dragon.form{n:D2}"), result.ModelFiles.Take(10).Select(f => f.Id));
        Assert.Equal(Enumerable.Range(1, 10).Select(n => $"models/dragon_form{n:D2}.glb"), result.ModelFiles.Take(10).Select(f => f.Path));
        Assert.Equal(new ExtractedModel("dragon.ride", "models/dragon_ride.glb"), result.ModelFiles[10]);
        Assert.Equal(10, result.DragonSources!.Forms);
        Assert.Equal(lead + Dragons.SeatTableGap, result.DragonSources!.SeatTable);
        Assert.Null(result.DragonNotice);
        Assert.False(Directory.Exists(Output(Extraction.StagingFolder)));

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Output(Manifest.FileName)));
        JsonElement[] models = manifest.RootElement.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("kind").GetString() == "model").ToArray();
        Assert.Equal(11, models.Length);
        Assert.Equal("dragon.ride", models[10].GetProperty("id").GetString());
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
    public void Run_Ride_IsTheChosenFormWithItsOwnRootName()
    {
        Run(DiscWithForms());

        int i = Dragons.RideForm - 1;
        byte[] expected = GlbWriter.Write(Dragons.Decode(Mdb(Form(i), Padding(i)), Cgb(), "ride", SeatOf(i)).Model);
        Assert.Equal(expected, File.ReadAllBytes(Output(Dragons.RidePath)));
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
    public void Write_WriterRejectsTheModel_ThrowsOneLineDecodingException()
    {
        var model = new Model("form01", new[] { new ModelPart("node00", Vector3.Zero, Array.Empty<ModelQuad>()) });

        var e = Assert.Throws<DecodingException>(() => Dragons.Write(model));

        Assert.Equal("the model cannot be written: part 'node00' has no quads", e.Message);
        Assert.IsType<ArgumentException>(e.InnerException);
    }

    [Fact]
    public void Write_ValidModel_IsTheWritersBytes()
    {
        Model model = Decode(Form(0)).Model;

        Assert.Equal(GlbWriter.Write(model), Dragons.Write(model));
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

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", default));

        Assert.Contains("mesh pointer", e.Message);
    }

    [Fact]
    public void Decode_VertexListOutOfRange_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh()));
        int mesh = (int)(BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(Dragons.BodyRoot(mdb))) - Load);
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan(mesh + 4), 0xFFFFFF00); // A vertex count larger than the file.

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", default));

        Assert.Contains("vertex list pointer", e.Message);
    }

    [Fact]
    public void Decode_NodeReachedTwice_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh(), new TestNode(Move(0, 0, 0), null)));
        uint child = BinaryPrimitives.ReadUInt32BigEndian(mdb.AsSpan(Dragons.BodyRoot(mdb) + 4));
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan((int)(child - Load) + 8), child); // The child is its own sibling.

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", default));

        Assert.EndsWith("is reached twice", e.Message);
    }

    [Fact]
    public void Decode_MatricesRunOffTheEnd_Throws()
    {
        byte[] mdb = Mdb(new TestNode(Move(0, 0, 0), SquareMesh()));
        BinaryPrimitives.WriteUInt32BigEndian(mdb.AsSpan(Dragons.BodyRoot(mdb)), Load + (uint)mdb.Length - 40); // A last mesh too near the end.

        var e = Assert.Throws<DecodingException>(() => Dragons.Decode(mdb, Cgb(), "form01", default));

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

    // ------------------------------------------------------------ the wing beat: both discs

    private string DiscWithBeatingForms(Func<int, TestNode>? body = null, Func<int, MotionSlot, MotionSlot>? slot = null, string? without = null) =>
        WithBeatingForms(new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb()), body, slot, without)
            .WriteCue(_folder);

    private sealed record GlbJson(JsonElement Json, byte[] Bin);

    private static GlbJson ReadGlb(byte[] glb)
    {
        int length = BinaryPrimitives.ReadInt32LittleEndian(glb.AsSpan(12));
        using JsonDocument json = JsonDocument.Parse(glb.AsMemory(20, length));
        return new GlbJson(json.RootElement.Clone(), glb[(20 + length + 8)..]);
    }

    [Fact]
    public void Run_BeatingForms_EveryModelHasOneSteppedWingBeatOnTheMovedParts()
    {
        ExtractionResult result = Run(DiscWithBeatingForms());

        Assert.Equal(11, result.ModelFiles.Count);
        foreach (ExtractedModel file in result.ModelFiles)
        {
            GlbJson glb = ReadGlb(File.ReadAllBytes(Output(file.Path)));
            JsonElement animation = Assert.Single(glb.Json.GetProperty("animations").EnumerateArray());
            Assert.Equal(Dragons.BeatName, animation.GetProperty("name").GetString());
            JsonElement[] samplers = animation.GetProperty("samplers").EnumerateArray().ToArray();
            Assert.All(samplers, s => Assert.Equal("STEP", s.GetProperty("interpolation").GetString()));
            JsonElement input = glb.Json.GetProperty("accessors")[samplers[0].GetProperty("input").GetInt32()];
            Assert.Equal(BeatFrames, input.GetProperty("count").GetInt32());

            JsonElement[] nodes = glb.Json.GetProperty("nodes").EnumerateArray().ToArray();
            var targets = animation.GetProperty("channels").EnumerateArray()
                .Select(c => (nodes[c.GetProperty("target").GetProperty("node").GetInt32()].GetProperty("name").GetString()!, c.GetProperty("target").GetProperty("path").GetString()!))
                .ToArray();
            Assert.Equal(MovedParts.SelectMany(p => new[] { (p, "rotation"), (p, "translation") }), targets);
        }
    }

    [Fact]
    public void Run_BeatingForms_EachFormCarriesItsOwnBeat()
    {
        Run(DiscWithBeatingForms());

        for (int i = 0; i < 10; i++)
        {
            (byte[] mtb, MotionSlot slot) = BeatMtb(i);
            byte[] mdb = Mdb(Body(i), Padding(i));
            Model model = Dragons.Decode(mdb, Cgb(), $"form{i + 1:D2}", SeatOf(i)).Model with { Animation = Dragons.WingBeat(mdb, mtb, slot, SeatOf(i)) };
            Assert.Equal(GlbWriter.Write(model), File.ReadAllBytes(Output($"models/dragon_form{i + 1:D2}.glb")));
        }

        int ride = Dragons.RideForm - 1;
        (byte[] rideMtb, MotionSlot rideSlot) = BeatMtb(ride);
        byte[] rideMdb = Mdb(Body(ride), Padding(ride));
        Model rideModel = Dragons.Decode(rideMdb, Cgb(), "ride", SeatOf(ride)).Model with { Animation = Dragons.WingBeat(rideMdb, rideMtb, rideSlot, SeatOf(ride)) };
        Assert.Equal(GlbWriter.Write(rideModel), File.ReadAllBytes(Output(Dragons.RidePath)));
    }

    [Fact]
    public void Run_BeatingForms_TheLineSaysWhereTheBeatsCameFrom()
    {
        ExtractionResult result = Run(DiscWithBeatingForms());

        Assert.Equal(0x123 - Dragons.MotionTableGap, result.DragonSources!.MotionTable);
        Assert.Null(result.DragonSources.NoBeat);
        Assert.Contains("; 10 dragon forms, seats from 0x173 of 1ST_READ.PRG, wing beats from 0x83 of 1ST_READ.PRG", result.Details());
        Assert.DoesNotContain("no wing beat", result.Details());
    }

    [Fact]
    public void Run_BeatingFormsTwice_WritesTheSameBytes()
    {
        string cue = DiscWithBeatingForms();
        Run(cue);
        byte[] first = File.ReadAllBytes(Output(Dragons.RidePath));
        Run(cue);

        Assert.Equal(first, File.ReadAllBytes(Output(Dragons.RidePath)));
    }

    // ------------------------------------------------------------ the wing beat: motion unreadable

    /// <summary>Every form written still, as <see cref="Dragons.Decode"/> alone gives it, and the line saying why.</summary>
    private void AssertStill(ExtractionResult result, Func<int, TestNode> body, string why)
    {
        Assert.Equal(11, result.ModelFiles.Count);
        Assert.Null(result.DragonSources!.MotionTable);
        Assert.Equal(why, result.DragonSources.NoBeat);
        Assert.EndsWith($", no wing beat: {why}", result.DragonLine());
        for (int i = 0; i < 10; i++)
        {
            byte[] expected = GlbWriter.Write(Dragons.Decode(Mdb(body(i), Padding(i)), Cgb(), $"form{i + 1:D2}", SeatOf(i)).Model);
            Assert.Equal(expected, File.ReadAllBytes(Output($"models/dragon_form{i + 1:D2}.glb")));
        }

        Assert.False(ReadGlb(File.ReadAllBytes(Output(Dragons.RidePath))).Json.TryGetProperty("animations", out _));
    }

    [Fact]
    public void Run_NoMotionTable_DragonsStillAndTheLineSaysWhy()
    {
        ExtractionResult result = Run(DiscWithForms());

        AssertStill(result, Form, "1ST_READ.PRG: it holds no motion table 0xA0 before the body roots");
    }

    [Fact]
    public void Run_MotionTableBeforeTheFile_DragonsStillAndTheLineSaysWhy()
    {
        var disc = new SyntheticDisc().With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb());
        var roots = Dragons.FormFiles.Select((_, i) => RootAddress(Form(i), Padding(i))).ToList();
        WithForms(disc, Dragons.SeatFile).With(Dragons.SeatFile, FirstRead(roots, lead: 0x10));

        AssertStill(Run(disc.WriteCue(_folder)), Form, "1ST_READ.PRG: it holds no motion table 0xA0 before the body roots");
    }

    [Fact]
    public void Run_MtbMissing_DragonsStillAndTheLineSaysWhy()
    {
        ExtractionResult result = Run(DiscWithBeatingForms(without: "DRA04D.MTB"));

        AssertStill(result, Body, "the disc has no DRA04D.MTB");
    }

    [Fact]
    public void Run_SlotPointerOutsideTheMtb_DragonsStillAndTheLineSaysWhy()
    {
        ExtractionResult result = Run(DiscWithBeatingForms(slot: (i, s) => i == 1 ? s with { B = 0x20300000 } : s));

        AssertStill(result, Body, "DRA02B: a slot 0 pointer, 0x20300000, is outside the .MTB");
    }

    [Fact]
    public void Run_FrameCountNotWhole_DragonsStillAndTheLineSaysWhy()
    {
        var disc = new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }.With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb());
        WithBeatingForms(disc, without: Dragons.SeatFile);
        var roots = Dragons.FormFiles.Select((_, i) => RootAddress(Body(i), Padding(i))).ToList();
        var slots = Dragons.FormFiles.Select((_, i) => BeatMtb(i).Slot).ToList();
        byte[] data = FirstRead(roots, slots);
        int slot = (int)(BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0x123 - Dragons.MotionTableGap)) - Dragons.FirstReadLoadAddress);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(slot + 12), 0x00038000); // 3.5 frames: DRA01A, first in the game's order.
        disc.With(Dragons.SeatFile, data);

        AssertStill(Run(disc.WriteCue(_folder)), Body, "1ST_READ.PRG: DRA01A's wing beat has a frame count of 0x00038000, not a whole number of frames above 0");
    }

    [Fact]
    public void Run_BodyNotTwentySevenNodes_DragonsStillAndTheLineSaysWhy()
    {
        TestNode Body3(int i) => i == 2 ? Form(i) : Body(i);

        ExtractionResult result = Run(DiscWithBeatingForms(body: Body3));

        AssertStill(result, Body3, "DRA03C: the body tree is not the 27 nodes the motion is for: it has 1");
    }

    [Fact]
    public void Run_RestTurnNotARotation_DragonsStillAndTheLineSaysWhy()
    {
        // Node 23 of DRA01A has a rest matrix with no turn at all, which has no inverse.
        TestNode Flat(int i) => i == 0
            ? Body(node => node == 23 ? new[] { 0, 0, 0, RestMove(0, 23).X, 0, 0, 0, RestMove(0, 23).Y, 0, 0, 0, RestMove(0, 23).Z } : RestMatrix(0, node))
            : Body(i);

        ExtractionResult result = Run(DiscWithBeatingForms(body: Flat));

        AssertStill(result, Flat, "DRA01A: the rest turn of node23 cannot be inverted");
    }

    [Fact]
    public void WingBeat_FramePointerOutsideTheMtb_Throws()
    {
        (byte[] mtb, MotionSlot slot) = BeatMtb(0);
        BinaryPrimitives.WriteUInt32BigEndian(mtb.AsSpan((int)(slot.C - Dragons.MotionLoadAddress) + 4), 0x20215FFC);

        var e = Assert.Throws<DecodingException>(() => Dragons.WingBeat(Mdb(Body(0)), mtb, slot, default));

        Assert.Equal("a frame pointer, 0x20215FFC, is outside the .MTB", e.Message);
    }

    [Fact]
    public void WingBeat_SlotPointerBeforeTheMtb_Throws()
    {
        (byte[] mtb, MotionSlot slot) = BeatMtb(0);

        var e = Assert.Throws<DecodingException>(() => Dragons.WingBeat(Mdb(Body(0)), mtb, slot with { A = 0x20214000 }, default));

        Assert.Equal("a slot 0 pointer, 0x20214000, is outside the .MTB", e.Message);
    }

    // ------------------------------------------------------------ the wing beat: its keys

    private static ModelAnimation Beat(int frames, Func<int, int, TestRecord> record, Seat seat = default) =>
        Dragons.WingBeat(Mdb(Body(0)), Mtb(frames, record).Mtb, Mtb(frames, record).Slot, seat);

    [Fact]
    public void WingBeat_IsSteppedAtThirtyFramesASecondWithOneKeyAFrame()
    {
        ModelAnimation beat = Beat(5, (f, node) => BeatRecord(0, f, node));

        Assert.Equal("wing_beat", beat.Name);
        Assert.Equal(30, beat.FramesPerSecond);
        Assert.Equal(MovedParts, beat.Channels.Select(c => c.Part));
        Assert.All(beat.Channels, c => Assert.Equal((5, 5), (c.Rotations.Count, c.Translations.Count)));
    }

    [Fact]
    public void WingBeat_RestRecords_GiveNoTurnAndTheOrigins()
    {
        var seat = new Seat(0, 160, -320);
        Model rest = Dragons.Decode(Mdb(Body(0)), Cgb(), "form01", seat).Model;

        ModelAnimation beat = Beat(2, (_, node) => RestRecord(0, node), seat);

        foreach (ModelChannel channel in beat.Channels)
        {
            ModelPart part = rest.Parts.Single(p => p.Name == channel.Part);
            Assert.All(channel.Rotations, q => Assert.Equal(Quaternion.Identity, q));
            Assert.All(channel.Translations, t => Assert.Equal(part.Origin, t));
        }
    }

    [Fact]
    public void WingBeat_RootsOwnMotion_IsLeftOut()
    {
        ModelAnimation with = Beat(3, (f, node) => BeatRecord(0, f, node));
        ModelAnimation without = Beat(3, (f, node) => node == 0 ? RestRecord(0, 0) : BeatRecord(0, f, node));

        Assert.DoesNotContain(with.Channels, c => c.Part is "node00" or "node01");
        for (int c = 0; c < with.Channels.Count; c++)
        {
            Assert.Equal(without.Channels[c].Rotations, with.Channels[c].Rotations);
            Assert.Equal(without.Channels[c].Translations, with.Channels[c].Translations);
        }
    }

    /// <summary>
    /// A record's local matrix for quarter turns, worked out here with whole numbers: <c>Ry * Rx * Rz</c>,
    /// the translation times 16.
    /// </summary>
    private static int[] QuarterTurnMatrix(TestRecord r)
    {
        static (int C, int S) Turn(int angle) => (((angle / 1024 % 4) + 4) % 4) switch { 0 => (1, 0), 1 => (0, 1), 2 => (-1, 0), _ => (0, -1) };
        static int[,] Mul(int[,] a, int[,] b)
        {
            var m = new int[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    m[i, j] = a[i, 0] * b[0, j] + a[i, 1] * b[1, j] + a[i, 2] * b[2, j];
                }
            }

            return m;
        }

        Assert.True(r.Rx % 1024 == 0 && r.Ry % 1024 == 0 && r.Rz % 1024 == 0);
        (int cx, int sx) = Turn(r.Rx);
        (int cy, int sy) = Turn(r.Ry);
        (int cz, int sz) = Turn(r.Rz);
        int[,] x = { { 1, 0, 0 }, { 0, cx, -sx }, { 0, sx, cx } };
        int[,] y = { { cy, 0, sy }, { 0, 1, 0 }, { -sy, 0, cy } };
        int[,] z = { { cz, -sz, 0 }, { sz, cz, 0 }, { 0, 0, 1 } };
        int[,] t = Mul(Mul(y, x), z);
        return new[]
        {
            t[0, 0] * One, t[0, 1] * One, t[0, 2] * One, r.Tx * 16,
            t[1, 0] * One, t[1, 1] * One, t[1, 2] * One, r.Ty * 16,
            t[2, 0] * One, t[2, 1] * One, t[2, 2] * One, r.Tz * 16,
        };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WingBeat_Keys_PutEveryCornerWhereTheFramesMatricesPutIt(int frame)
    {
        var seat = new Seat(0, 320, 640);
        Model rest = Dragons.Decode(Mdb(Body(0)), Cgb(), "form01", seat).Model;
        TestNode posedBody = Body(node => node == 0 ? RestMatrix(0, 0) : QuarterTurnMatrix(BeatRecord(0, frame, node)));
        Model posed = Dragons.Decode(Mdb(posedBody), Cgb(), "form01", seat).Model;

        ModelAnimation beat = Beat(BeatFrames, (f, node) => BeatRecord(0, f, node), seat);

        foreach (ModelPart part in rest.Parts)
        {
            ModelChannel? channel = beat.Channels.SingleOrDefault(c => c.Part == part.Name);
            Quaternion q = channel?.Rotations[frame] ?? Quaternion.Identity;
            Vector3 t = channel?.Translations[frame] ?? part.Origin;
            ModelPart want = posed.Parts.Single(p => p.Name == part.Name);
            Assert.Equal(want.Origin, t);
            for (int k = 0; k < part.Quads.Count; k++)
            {
                Vector3[] corners = { part.Quads[k].A, part.Quads[k].B, part.Quads[k].C, part.Quads[k].D };
                Vector3[] wanted = { want.Quads[k].A, want.Quads[k].B, want.Quads[k].C, want.Quads[k].D };
                for (int c = 0; c < 4; c++)
                {
                    Vector3 got = Vector3.Transform(corners[c], q) + t;
                    Assert.True(Vector3.Distance(wanted[c] + want.Origin, got) < 1e-5f, $"{part.Name} frame {frame} corner {c}: {got} where {wanted[c] + want.Origin}");
                }
            }
        }
    }

    [Fact]
    public void WingBeat_Rotations_AreUnitQuaternionsWithWNotBelowZero()
    {
        ModelAnimation beat = Beat(8, (f, node) => node == 23 ? RestRecord(0, node) with { Rx = (short)(f * 333), Ry = (short)(f * -517), Rz = (short)(f * 1201) } : RestRecord(0, node));

        Assert.All(beat.Channels.SelectMany(c => c.Rotations), q =>
        {
            Assert.InRange(q.Length(), 1 - 1e-6f, 1 + 1e-6f);
            Assert.True(q.W >= 0);
        });
    }

    [Fact]
    public void Sin_TableHasTheQuartersExactlyAndIsWithinOneStepOfTheSine()
    {
        Assert.Equal((0, 65536, 0, -65536), (Dragons.Sin(0), Dragons.Sin(1024), Dragons.Sin(2048), Dragons.Sin(3072)));
        Assert.Equal((65536, 0, -65536), (Dragons.Cos(0), Dragons.Cos(1024), Dragons.Cos(2048)));
        Assert.Equal(Dragons.Sin(-1024), Dragons.Sin(3072));
        Assert.Equal(46341, Dragons.Sin(512)); // sin 45° is 46340.95 in 16.16.
        for (int a = 0; a < 4096; a++)
        {
            Assert.InRange(Dragons.Sin(a) - Math.Sin(a * Math.PI / 2048) * 65536, -0.5001, 0.5001);
        }
    }

    [Fact]
    public void DragonSources_Describe_NamesTheBeatsOrWhyThereAreNone()
    {
        Assert.Equal("10 dragon forms, seats from 0x54840 of 1ST_READ.PRG, wing beats from 0x54750 of 1ST_READ.PRG, 2 polygons drawn flat grey",
            new DragonSources(10, 0x54840, 2, 0x54750).Describe());
        Assert.Equal("10 dragon forms, seats from 0x54840 of 1ST_READ.PRG, no wing beat: the disc has no DRA01A.MTB",
            new DragonSources(10, 0x54840, 0, null, "the disc has no DRA01A.MTB").Describe());
    }

    // ------------------------------------------------------------ the recorded output and the version

    /// <summary>
    /// Everything extraction writes from a synthetic disc with textures, the floor and canopy, the ten forms
    /// and the ride with their wing beats, as one SHA-256, recorded beside <see cref="Extraction.Version"/> in
    /// <c>expected/extraction-output.sha256</c> as <c>&lt;version&gt; &lt;sha256&gt;</c>. Output that changes
    /// while the version stays the same fails, because release mode trusts the version to say when to
    /// extract again; a new version fails until it is recorded with <c>PDZ_RECORD_EXPECTED=1</c>.
    /// </summary>
    [Fact]
    public void Run_WholeOutput_EqualsTheRecordedOutputForThisVersion()
    {
        string cue = WithBeatingForms(new SyntheticDisc { Product = "GS-9049", Version = "V1.001" }
            .With("E03B0N0.MDB", Mdb(Record())).With("E03B0N0.CGB", Cgb())
            .With("EPISODE3.PRG", SyntheticPlanes.PlaneProgram())).WriteCue(_folder);
        ExtractionResult result = Run(cue);
        Assert.Equal(2, result.PlaneFiles.Count);
        Assert.Equal(11, result.ModelFiles.Count);

        string line = $"{Extraction.Version} {OutputHash(Path.Combine(_folder, "out"))}\n";
        if (Environment.GetEnvironmentVariable("PDZ_RECORD_EXPECTED") == "1")
        {
            File.WriteAllText(Path.Combine(SourceFolder(), "expected", RecordedOutput), line);
            return;
        }

        string[] recorded = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expected", RecordedOutput)).Trim().Split(' ');
        Assert.True(recorded[0] == Extraction.Version,
            $"Extraction.Version is {Extraction.Version} and the output was recorded for {recorded[0]}: record again with PDZ_RECORD_EXPECTED=1");
        Assert.True(recorded[1] == line.Trim().Split(' ')[1],
            $"extraction's output changed: raise Extraction.Version and record again (recorded for {recorded[0]})");
    }

    private const string RecordedOutput = "extraction-output.sha256";

    /// <summary>
    /// One SHA-256 over every file under <paramref name="folder"/>, in ordinal order of their paths: of a
    /// line per file, its own SHA-256, two spaces and its path with forward slashes, as <c>sha256sum</c> gives.
    /// </summary>
    private static string OutputHash(string folder)
    {
        var lines = new StringBuilder();
        foreach (string path in Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal))
        {
            lines.Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, path)))).ToLowerInvariant())
                .Append("  ").Append(path).Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(lines.ToString()))).ToLowerInvariant();
    }

    private static string SourceFolder([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
