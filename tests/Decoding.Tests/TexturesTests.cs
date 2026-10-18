using System.Linq;
using Xunit;
using static PdzDecoding.Tests.SyntheticTextures;

namespace PdzDecoding.Tests;

/// <summary>Finding, numbering and decoding the textures of <c>.MDB</c>/<c>.CGB</c> pairs built here.</summary>
public sealed class TexturesTests
{
    [Fact]
    public void FindRecords_ReadsTheFourWordsAndTheSize()
    {
        TextureRecord r = Assert.Single(Textures.FindRecords(Mdb(Record(height: 3))));

        Assert.Equal(6, r.Offset);
        Assert.Equal((Mode1, (ushort)(Base + 12), (ushort)Base, (ushort)0x0103), r.Words);
        Assert.Equal(8, r.Width);
        Assert.Equal(3, r.Height);
        Assert.Equal(1, r.ColourMode);
    }

    [Fact]
    public void FindRecords_LooksAtEvenOffsetsOnly()
    {
        byte[] mdb = new byte[] { 0 }.Concat(Mdb(Record())).ToArray();

        Assert.Empty(Textures.FindRecords(mdb));
    }

    [Fact]
    public void FindRecords_SkipsWordsThatAreNoRecord()
    {
        byte[] mdb = Mdb(
            Record(pmod: 0x0508),                    // CMDPMOD's high byte is not 0x04
            (Mode1, Base, Base, 0x4102),            // CMDSIZE's top bits set
            (Mode1, Base, Base, 0x0002));           // width 0

        Assert.Empty(Textures.FindRecords(mdb));
    }

    [Fact]
    public void InferBase_PicksTheBaseThatFitsMostRecords()
    {
        var records = Textures.FindRecords(Mdb(Record(), Record(pixels: 16), Record(pixels: 32)));

        Assert.Equal(Base, Textures.InferBase(records, Cgb().Length));
    }

    [Fact]
    public void Number_RepeatedRecordTakesNoNewNumber()
    {
        var records = Textures.FindRecords(Mdb(Record(), Record(pixels: 16), Record(), Record(pixels: 32)));

        var numbered = Textures.Number(records, Base, Cgb().Length);

        Assert.Equal(new[] { 0, 1, 2 }, numbered.Select(t => t.Number));
        Assert.Equal(new[] { 6, 14, 30 }, numbered.Select(t => t.Record.Offset));
    }

    [Theory]
    [InlineData(0x0448, 0, 0, 0)]   // CMDPMOD: the transparency bit
    [InlineData(0, 1, 0, 0)]        // CMDCOLR: another lookup table, 8 bytes earlier
    [InlineData(0, 0, 1, 0)]        // CMDSRCA
    [InlineData(0, 0, 0, 0x0101)]   // CMDSIZE: the same pixels, one row
    public void Number_RecordDifferingInAnyWordIsANewTexture(int pmod, int colr, int srca, int size)
    {
        var first = Record();
        var second = (pmod == 0 ? first.Pmod : (ushort)pmod, (ushort)(first.Colr - colr), (ushort)(first.Srca + srca),
            size == 0 ? first.Size : (ushort)size);

        var numbered = Textures.Number(Textures.FindRecords(Mdb(first, second)), Base, Cgb().Length);

        Assert.Equal(new[] { 0, 1 }, numbered.Select(t => t.Number));
    }

    [Fact]
    public void Number_GivesTexturesInOtherColourModesTheirNumbers()
    {
        // Mode 4 (256 colours) is not decoded yet, but keeps its place so decoding it later renumbers nothing.
        var records = Textures.FindRecords(Mdb(Record(pmod: 0x0420), Record()));

        var numbered = Textures.Number(records, Base, Cgb().Length);

        Assert.Equal(new[] { 4, 1 }, numbered.Select(t => t.Record.ColourMode));
        Assert.Equal(new[] { 0, 1 }, numbered.Select(t => t.Number));
    }

    [Fact]
    public void Number_LeavesOutRecordsOutsideTheCgb()
    {
        var records = Textures.FindRecords(Mdb(Record(pixels: 128), Record(lut: 112), Record()));

        var numbered = Textures.Number(records, Base, Cgb().Length);

        Assert.Equal(22, Assert.Single(numbered).Record.Offset);
    }

    [Fact]
    public void DecodeMode1_LooksIndicesUpInTheTable()
    {
        TextureRecord r = Textures.FindRecords(Mdb(Record())).Single();

        byte[] rgba = Textures.DecodeMode1(Cgb(), Base, r);

        Assert.Equal(8 * 2 * 4, rgba.Length);
        // Pixel 3 is index 3: red 3, green 28, blue 1 in RGB555, each scaled by 255 / 31.
        Assert.Equal(new byte[] { 3 * 255 / 31, 28 * 255 / 31, 1 * 255 / 31, 255 }, rgba[12..16]);
    }

    [Fact]
    public void DecodeMode1_IndexZeroIsTransparent()
    {
        TextureRecord r = Textures.FindRecords(Mdb(Record())).Single();

        byte[] rgba = Textures.DecodeMode1(Cgb(), Base, r);

        Assert.Equal(0, rgba[3]);
        Assert.All(Enumerable.Range(1, 15), p => Assert.Equal(255, rgba[p * 4 + 3]));
    }

    [Fact]
    public void DecodeMode1_IndexZeroIsOpaqueWithBit0x40()
    {
        TextureRecord r = Textures.FindRecords(Mdb(Record(pmod: Mode1 | 0x40))).Single();

        byte[] rgba = Textures.DecodeMode1(Cgb(), Base, r);

        Assert.All(Enumerable.Range(0, 16), p => Assert.Equal(255, rgba[p * 4 + 3]));
        Assert.Equal(new byte[] { 0, 255, 1 * 255 / 31 }, rgba[0..3]);
    }

    [Fact]
    public void DecodeMode5_ReadsEachWordAsRgb555()
    {
        TextureRecord r = Textures.FindRecords(Mdb(Record(pmod: Mode5))).Single();

        byte[] rgba = Textures.DecodeMode5(Cgb(), Base, r);

        Assert.Equal(8 * 2 * 4, rgba.Length);
        // Pixel 1 is the word 0x4567: red 7, green 11, blue 17.
        Assert.Equal(new byte[] { 7 * 255 / 31, 11 * 255 / 31, 17 * 255 / 31 }, rgba[4..7]);
    }

    [Fact]
    public void DecodeMode5_AlphaIsBit15()
    {
        TextureRecord r = Textures.FindRecords(Mdb(Record(pmod: Mode5 | 0x40))).Single();

        byte[] rgba = Textures.DecodeMode5(Cgb(), Base, r);

        // The words run 0x0123, 0x4567, 0x89AB, 0xCDEF, then repeat; the transparency bit plays no part.
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0, 255, 255 }, Enumerable.Range(0, 8).Select(p => rgba[p * 4 + 3]));
    }

    [Fact]
    public void DecodeMode5_TextureOutsideTheCgbThrows()
    {
        // 8 x 2 in mode 5 is 32 bytes, so at offset 112 it runs past the 128-byte .CGB.
        var r = new TextureRecord(0, Mode5, Base, (ushort)(Base + 14), 0x0102);

        Assert.Throws<DecodingException>(() => Textures.DecodeMode5(Cgb(), Base, r));
    }

    private static ColourRam Colours => ColourBanks.FromProgram(TwoTableProgram()).Colours!;

    private static byte[] Rgba(ushort colour, byte alpha)
    {
        var rgba = new byte[4];
        Textures.Rgb555(colour, rgba);
        rgba[3] = alpha;
        return rgba;
    }

    [Fact]
    public void DecodeMode4_LooksEachPixelUpInColourRamThroughCmdcolrsHighByte()
    {
        // CMDCOLR 0x1234: the bank is 0x1200, so pixel p is code (0x1200 | p) & 0x7FF = 0x200 | p.
        TextureRecord r = Textures.FindRecords(Mdb(Record(pmod: Mode4, colr: 0x1234))).Single();

        byte[] rgba = Textures.DecodeMode4(CountingCgb(), Base, r, Colours);

        Assert.Equal(8 * 2 * 4, rgba.Length);
        Assert.Equal(Rgba(BlockColour(3, 5), 255), rgba[20..24]);
        Assert.Equal(Rgba(BlockColour(3, 15), 255), rgba[60..64]);
    }

    [Fact]
    public void DecodeMode4_PixelZeroIsTransparentUnlessBit0x40()
    {
        TextureRecord on = Textures.FindRecords(Mdb(Record(pmod: Mode4))).Single();
        TextureRecord off = Textures.FindRecords(Mdb(Record(pmod: Mode4 | 0x40))).Single();

        byte[] transparent = Textures.DecodeMode4(CountingCgb(), Base, on, Colours);
        byte[] opaque = Textures.DecodeMode4(CountingCgb(), Base, off, Colours);

        Assert.Equal(Rgba(BlockColour(2, 0), 0), transparent[0..4]);
        Assert.Equal(Rgba(BlockColour(2, 0), 255), opaque[0..4]);
        Assert.All(Enumerable.Range(1, 15), p => Assert.Equal(255, transparent[p * 4 + 3]));
    }

    [Fact]
    public void DecodeMode4_CodeNoPairFilledIsBlack()
    {
        // CMDCOLR 0x0100: codes 0x100 to 0x1FF, which the second table leaves empty.
        TextureRecord r = Textures.FindRecords(Mdb(Record(pmod: Mode4, colr: 0x0100))).Single();

        byte[] rgba = Textures.DecodeMode4(CountingCgb(), Base, r, Colours);

        Assert.Equal(new byte[] { 0, 0, 0, 255 }, rgba[4..8]);
    }

    [Fact]
    public void DecodeMode0_LooksEachNibbleUpThroughCmdcolrsHigh12Bits()
    {
        // Pixels from offset 8 run 0, 8, 0, 9 ...; CMDCOLR 0x1237 makes pixel p code (0x1230 | p) & 0x7FF.
        TextureRecord r = Textures.FindRecords(Mdb(Record(pixels: 8, pmod: Mode0, colr: 0x1237))).Single();

        byte[] rgba = Textures.DecodeMode0(CountingCgb(), Base, r, Colours);

        Assert.Equal(8 * 2 * 4, rgba.Length);
        Assert.Equal(Rgba(BlockColour(3, 0x38), 255), rgba[4..8]);
        Assert.Equal(Rgba(BlockColour(3, 0x39), 255), rgba[12..16]);
        Assert.Equal(Rgba(BlockColour(3, 0x30), 0), rgba[0..4]);
    }

    [Fact]
    public void DecodeMode0_PixelZeroIsOpaqueWithBit0x40()
    {
        TextureRecord r = Textures.FindRecords(Mdb(Record(pixels: 8, pmod: Mode0 | 0x40, colr: 0x1230))).Single();

        byte[] rgba = Textures.DecodeMode0(CountingCgb(), Base, r, Colours);

        Assert.Equal(Rgba(BlockColour(3, 0x30), 255), rgba[0..4]);
        Assert.All(Enumerable.Range(0, 16), p => Assert.Equal(255, rgba[p * 4 + 3]));
    }

    [Fact]
    public void DecodeMode4_TextureOutsideTheCgbThrows()
    {
        // 8 x 2 in mode 4 is 16 bytes, so at offset 120 it runs past the 128-byte .CGB.
        var r = new TextureRecord(0, Mode4, Base, (ushort)(Base + 15), 0x0102);

        Assert.Throws<DecodingException>(() => Textures.DecodeMode4(CountingCgb(), Base, r, Colours));
    }

    [Fact]
    public void Rgb555_ScalesEachChannelTo8Bits()
    {
        var rgb = new byte[3];

        Textures.Rgb555(0x7FFF, rgb);
        Assert.Equal(new byte[] { 255, 255, 255 }, rgb);

        Textures.Rgb555(31 << 10 | 16, rgb);
        Assert.Equal(new byte[] { 131, 0, 255 }, rgb);
    }
}
