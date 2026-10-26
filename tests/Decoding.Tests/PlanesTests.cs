using System;
using System.Buffers.Binary;
using System.Text;
using Xunit;
using static PdzDecoding.Tests.SyntheticTextures;
using static PdzDecoding.Tests.SyntheticPlanes;

namespace PdzDecoding.Tests;

/// <summary>
/// The floor and canopy from <c>EPISODE3.PRG</c>, on streams and programs built here: the unpacker
/// on the streams <c>tests/tools/test_plane_probe.py</c> builds by hand, finding the sources by the
/// literal pool, drawing pattern names as VDP2 reads them (cell, palette, flips), and the notices for
/// a program with no pool, too few maps or a bad stream.
/// </summary>
public sealed class PlanesTests
{
    [Fact]
    public void Unpack_LiteralsShortCopyAndLongCopy()
    {
        // Flags 0x93, least significant bit first: literal, literal, short copy (length 2 + 2,
        // offset -2), long copy (word 0xFFD3: offset -6, length 3 + 2); then flags 0x02: end.
        byte[] stream = { 0x93, 0x41, 0x42, 0xFE, 0xD3, 0xFF, 0x02, 0x00, 0x00 };

        (byte[] bytes, int packed) = Planes.Unpack(stream, 0);

        Assert.Equal("ABABABABABA", Encoding.ASCII.GetString(bytes));
        Assert.Equal(9, packed);
    }

    [Fact]
    public void Unpack_LongCopyWithALengthByte()
    {
        // Flags 0x15: literal, long copy (word 0xFFF8: offset -1, length in the next byte, 9 + 1), then the end word.
        byte[] stream = { 0x15, 0x5A, 0xF8, 0xFF, 0x09, 0x00, 0x00 };

        (byte[] bytes, int packed) = Planes.Unpack(stream, 0);

        Assert.Equal(new string('Z', 11), Encoding.ASCII.GetString(bytes));
        Assert.Equal(7, packed);
    }

    [Fact]
    public void Unpack_StartsAtTheGivenOffset()
    {
        byte[] stream = { 0xFF, 0xFF, 0x93, 0x41, 0x42, 0xFE, 0xD3, 0xFF, 0x02, 0x00, 0x00 };

        Assert.Equal("ABABABABABA", Encoding.ASCII.GetString(Planes.Unpack(stream, 2).Bytes));
    }

    [Fact]
    public void Unpack_CopyFromBeforeTheStream_IsAnError()
    {
        var e = Assert.Throws<DecodingException>(() => Planes.Unpack(new byte[] { 0x00, 0xFE }, 0));

        Assert.Equal("the stream at 0x0 copies from before its start", e.Message);
    }

    [Fact]
    public void Unpack_StreamRunningOffTheEnd_IsAnError()
    {
        // Two literals, then the file ends where a third byte should be.
        var e = Assert.Throws<DecodingException>(() => Planes.Unpack(new byte[] { 0xFF, 0x41, 0x42 }, 0));

        Assert.Equal("the stream at 0x0 runs off the end of the file", e.Message);
    }

    [Fact]
    public void Unpack_MoreThanTheLimit_IsAnError()
    {
        byte[] stream = PackLiterals(new byte[0x20]);

        Assert.Equal(0x20, Planes.Unpack(stream, 0, 0x20).Bytes.Length);
        var e = Assert.Throws<DecodingException>(() => Planes.Unpack(stream, 0, 0x1F));
        Assert.Equal("the stream at 0x0 unpacks to more than 0x1F bytes", e.Message);
    }

    [Fact]
    public void PackLiterals_UnpacksToItsInput()
    {
        byte[] input = PlaneCells();

        (byte[] bytes, int packed) = Planes.Unpack(PackLiterals(input), 0);

        Assert.Equal(input, bytes);
        Assert.Equal(PackLiterals(input).Length, packed);
    }

    [Fact]
    public void FindSources_ComeFromTheLiteralPool()
    {
        byte[] prg = PlaneProgram();

        PlaneSources sources = Planes.FindSources(prg);

        Assert.Equal(0x600, sources.Cells);
        Assert.Equal(new[] { 0x600 + PackLiterals(PlaneCells()).Length, 0x600 + PackLiterals(PlaneCells()).Length + PackLiterals(FloorMap()).Length }, sources.Maps);
        Assert.Equal(new[] { 0x300, 0x500 }, sources.Colours);
    }

    [Fact]
    public void FindSources_PoolElsewhere_IsFoundThere()
    {
        Assert.Equal(new[] { 0x300, 0x500 }, Planes.FindSources(PlaneProgram(pool: 0x180)).Colours);
    }

    [Fact]
    public void FindSources_MapPointerOutsideTheFile_IsAnError()
    {
        var e = Assert.Throws<DecodingException>(() => Planes.FindSources(PlaneProgram(maps: new[] { 0x06100000u })));

        Assert.Equal("map list entry 0x06100000 at 0x200 is outside EPISODE3.PRG", e.Message);
    }

    [Fact]
    public void FindSources_CellsPointerOutsideTheFile_IsAnErrorNamingIt()
    {
        byte[] prg = PlaneProgram();
        BinaryPrimitives.WriteUInt32BigEndian(prg.AsSpan(0x104), 0x06100000);

        var e = Assert.Throws<DecodingException>(() => Planes.FindSources(prg));

        Assert.Equal("the cells pointer 0x06100000 at 0x104 is outside EPISODE3.PRG", e.Message);
    }

    [Fact]
    public void FindSources_MapListPointerOutsideTheFile_IsAnErrorNamingIt()
    {
        byte[] prg = PlaneProgram();
        BinaryPrimitives.WriteUInt32BigEndian(prg.AsSpan(0x110), 0x06100000);

        var e = Assert.Throws<DecodingException>(() => Planes.FindSources(prg));

        Assert.Equal("the map list pointer 0x06100000 at 0x110 is outside EPISODE3.PRG", e.Message);
    }

    [Fact]
    public void FindSources_NoColourLiteral_IsAnError()
    {
        byte[] prg = PlaneProgram();
        BinaryPrimitives.WriteUInt32BigEndian(prg.AsSpan(0x100 - 0x30), 0);

        var e = Assert.Throws<DecodingException>(() => Planes.FindSources(prg));

        Assert.Equal("no colour RAM 0x25F00E00 source inside EPISODE3.PRG 0x30 bytes before the literal pool at 0x100", e.Message);
    }

    [Fact]
    public void FromProgram_DrawsTheFloorAndCanopyFromTheUnpackedCellsMapsAndColours()
    {
        (PlaneImages? images, string? notice) = Planes.FromProgram(PlaneProgram());

        Assert.Null(notice);
        Assert.Equal(Planes.Size * Planes.Size * 4, images!.Floor.Length);
        // The floor at cell (1, 0): cell 1, palette 6, so pixel (x, y) is block 0's entry at the cell byte VDP2 reads.
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(Rgba(PlaneColour(0, CellByte(x, y))), Pixel(images.Floor, 16 + x, y));
            }
        }

        // The canopy: cell 2 (byte i is 255 - i), palette 7: codes 0x700 up, of which only 128 are filled.
        int value = 255 - CellByte(3, 5);
        Assert.Equal(Rgba(value < 128 ? PlaneColour(1, value) : (ushort)0), Pixel(images.Canopy, 3, 5));
        Assert.Equal(Rgba(PlaneColour(1, 255 - CellByte(15, 15))), Pixel(images.Canopy, 15, 15));
    }

    [Fact]
    public void FromProgram_FlippedName_IsDrawnFlipped()
    {
        (PlaneImages? images, _) = Planes.FromProgram(PlaneProgram());

        // The floor's (0, 0) is cell 2 flipped both ways: pixel (x, y) shows the cell's (15 - x, 15 - y).
        Assert.Equal(Rgba(PlaneColour(0, 255 - CellByte(15, 15))), Pixel(images!.Floor, 0, 0));
        Assert.Equal(Rgba(PlaneColour(0, 255 - CellByte(15 - 2, 15 - 9))), Pixel(images.Floor, 2, 9));
    }

    [Theory]
    [InlineData(0x0400, 15, 0)]   // horizontal flip only
    [InlineData(0x0800, 0, 15)]   // vertical flip only
    public void Render_FlipBits_MirrorTheCell(int flip, int mirrorX, int mirrorY)
    {
        var vram = new byte[0x80000];
        PlaneCells().CopyTo(vram, 0x40000);
        BinaryPrimitives.WriteUInt16BigEndian(vram.AsSpan(0x60000), (ushort)(0x6002 | flip));
        var cram = new byte[0x1000];
        for (int i = 0; i < 256; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(cram.AsSpan(0xC00 + i * 2), PlaneColour(0, i));
        }

        byte[] floor = Planes.Render(vram, cram, 0);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                int sx = mirrorX == 0 ? x : mirrorX - x, sy = mirrorY == 0 ? y : mirrorY - y;
                Assert.Equal(Rgba(PlaneColour(0, CellByte(sx, sy))), Pixel(floor, x, y));
            }
        }
    }

    [Fact]
    public void Render_PlaneB_ReadsTheSecondPage()
    {
        var vram = new byte[0x80000];
        PlaneCells().CopyTo(vram, 0x40000);
        BinaryPrimitives.WriteUInt16BigEndian(vram.AsSpan(0x60800), 0x6002);
        var cram = new byte[0x1000];
        BinaryPrimitives.WriteUInt16BigEndian(cram.AsSpan(0xC00 + 9 * 2), 0x7FFF);

        Assert.Equal(Rgba(0x7FFF), Pixel(Planes.Render(vram, cram, 1), 1, 1));
        Assert.Equal(Rgba(0), Pixel(Planes.Render(vram, cram, 0), 1, 1));
    }

    [Fact]
    public void FromProgram_NoProgram_IsANotice()
    {
        Assert.Equal((null, "the disc has no EPISODE3.PRG"), Planes.FromProgram(null));
    }

    [Fact]
    public void FromProgram_NoLiteralPool_IsANotice()
    {
        (PlaneImages? images, string? notice) = Planes.FromProgram(TwoTableProgram());

        Assert.Null(images);
        Assert.Equal("EPISODE3.PRG holds no literal pool with the cells' and maps' VRAM addresses", notice);
    }

    [Fact]
    public void FromProgram_OneMap_IsANotice()
    {
        byte[] prg = PlaneProgram();
        uint floor = BinaryPrimitives.ReadUInt32BigEndian(prg.AsSpan(0x200));

        Assert.Equal("EPISODE3.PRG's map list holds 1 map, not the 2 the floor and canopy need",
            Planes.FromProgram(PlaneProgram(maps: new[] { floor })).Notice);
    }

    [Fact]
    public void FromProgram_StreamRunningOffTheEnd_IsANoticeNamingTheStream()
    {
        int full = PlaneProgram().Length;

        (PlaneImages? images, string? notice) = Planes.FromProgram(PlaneProgram(size: full - 3));

        Assert.Null(images);
        Assert.Equal($"EPISODE3.PRG's map 1: the stream at 0x{full - PackLiterals(CanopyMap()).Length:X} runs off the end of the file", notice);
    }

    [Fact]
    public void FromProgram_BadCellsStream_IsANoticeNamingIt()
    {
        byte[] prg = PlaneProgram();
        prg[0x600] = 0x00;
        prg[0x601] = 0xFE; // a short copy from before the start

        Assert.Equal("EPISODE3.PRG's cells: the stream at 0x600 copies from before its start", Planes.FromProgram(prg).Notice);
    }

    [Fact]
    public void Describe_NamesEverySource()
    {
        Assert.Equal("floor and canopy from EPISODE3.PRG: cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244",
            new PlaneSources(0x3FDAC, new[] { 0x52608, 0x5287C }, new[] { 0x52C44, 0x53244 }).Describe());
    }

    /// <summary>The byte of a 16x16 cell VDP2 shows at (x, y): four 8x8 quarters, top left, top right, bottom left, bottom right.</summary>
    private static int CellByte(int x, int y) => ((y / 8) * 2 + x / 8) * 64 + (y % 8) * 8 + x % 8;

    private static byte[] Rgba(ushort colour)
    {
        var rgba = new byte[] { 0, 0, 0, 255 };
        Textures.Rgb555(colour, rgba);
        return rgba;
    }

    private static byte[] Pixel(byte[] image, int x, int y) => image.AsSpan((y * Planes.Size + x) * 4, 4).ToArray();
}
