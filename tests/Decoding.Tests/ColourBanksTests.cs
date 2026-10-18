using System.Linq;
using Xunit;
using static PdzDecoding.Tests.SyntheticTextures;

namespace PdzDecoding.Tests;

/// <summary>Finding <c>EPISODE3.PRG</c>'s colour upload tables and building colour RAM from one, on programs built here.</summary>
public sealed class ColourBanksTests
{
    [Fact]
    public void FindTables_FindsEachTableByItsPatternAndEndsItAtTheFirstOtherDestination()
    {
        var tables = ColourBanks.FindTables(TwoTableProgram());

        Assert.Equal(new[] { 0x10, 0x102 }, tables.Select(t => t.Offset));
        Assert.Equal(new[] { 0x102, 0x10A }, tables[1].Pairs.Select(p => p.Offset));
        Assert.Equal(new[] { (0x25F00000u, 0x06080600u), (0x25F00400u, 0x06080800u) }, tables[1].Pairs.Select(p => (p.Destination, p.Source)));
    }

    [Fact]
    public void FindTables_NeedsDestination0x25F00000ThenA060Source()
    {
        byte[] prg = Program(0x400, new (int, int)[0],
            (0x10, new[] { (0x25F00200u, 0x06080000u) }),     // does not start at colour RAM 0
            (0x40, new[] { (0x25F00000u, 0x00200000u) }));    // its source is not 0x060xxxxx

        Assert.Empty(ColourBanks.FindTables(prg));
    }

    [Fact]
    public void FromProgram_TwoTables_BuildsFromTheSecond()
    {
        (ColourRam? colours, string? notice) = ColourBanks.FromProgram(TwoTableProgram());

        Assert.Null(notice);
        Assert.Equal(0x102, colours!.Table.Offset);
        Assert.Equal(2, colours.TablesFound);
        Assert.Equal(BlockColour(2, 5), colours.Colour(0x005));
        Assert.Equal(BlockColour(3, 0xFF), colours.Colour(0x2FF));
        Assert.Equal("colour table at 0x102 of EPISODE3.PRG (table 2 of 2), 2 blocks", colours.Describe());
    }

    [Fact]
    public void FromProgram_OneTable_BuildsFromIt()
    {
        byte[] prg = Program(0x1000, new[] { (0x400, 1) }, (0x22, new[] { (0x25F00000u, 0x06080400u) }));

        ColourRam colours = ColourBanks.FromProgram(prg).Colours!;

        Assert.Equal(0x22, colours.Table.Offset);
        Assert.Equal(BlockColour(1, 9), colours.Colour(9));
        Assert.Equal("colour table at 0x22 of EPISODE3.PRG (table 1 of 1), 1 block", colours.Describe());
    }

    [Fact]
    public void Describe_NamesTheTableItWasBuiltFrom()
    {
        byte[] prg = TwoTableProgram();
        var tables = ColourBanks.FindTables(prg);

        ColourRam first = ColourBanks.Build(prg, tables[0], 0, tables.Count);

        Assert.Equal("colour table at 0x10 of EPISODE3.PRG (table 1 of 2), 2 blocks", first.Describe());
    }

    [Fact]
    public void FromProgram_NoTable_GivesANoticeNamingWhy()
    {
        Assert.Equal((null, "EPISODE3.PRG holds no colour upload table"), ColourBanks.FromProgram(new byte[0x400]));
        Assert.Equal((null, "the disc has no EPISODE3.PRG"), ColourBanks.FromProgram(null));
    }

    [Fact]
    public void Build_PairOutsideTheFileOrColourRam_IsSkippedAndTheRestApplied()
    {
        byte[] prg = Program(0x800, new[] { (0x400, 1), (0x600, 2) }, (0x10, new[]
        {
            (0x25F00000u, 0x06080400u),
            (0x25F00200u, 0x06080700u),     // its block would run past the end of the file
            (0x25F00400u, 0x07000000u),     // its source is not in the file at all
            (0x25F01000u, 0x06080400u),     // its destination is past colour RAM
            (0x25F00600u, 0x06080600u),
        }));

        ColourRam colours = ColourBanks.FromProgram(prg).Colours!;

        Assert.Equal(new[] { 0x18, 0x20, 0x28 }, colours.Skipped.Select(p => p.Offset));
        Assert.Equal(BlockColour(1, 3), colours.Colour(0x003));
        Assert.Equal(0, colours.Colour(0x103));
        Assert.Equal(BlockColour(2, 3), colours.Colour(0x303));
        Assert.Contains("2 blocks; skipped: the pair at 0x18 (destination 0x25F00200, source 0x06080700), the pair at 0x20", colours.Describe());
    }

    [Fact]
    public void Colour_CodeNoPairFilled_IsBlack()
    {
        ColourRam colours = ColourBanks.FromProgram(TwoTableProgram()).Colours!;

        Assert.Equal(0, colours.Colour(0x100));
        Assert.Equal(0, colours.Colour(0x7FF));
        // Only the low 11 bits of a code count.
        Assert.Equal(colours.Colour(0x005), colours.Colour(0x805));
    }
}
