using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PdzDecoding;

/// <summary>
/// One pair of a colour upload table: a destination in VDP2 colour RAM (<c>0x25F00000</c> plus a byte
/// offset) and a source load address, from which 0x200 bytes are copied.
/// </summary>
/// <param name="Offset">Where the pair is in <c>EPISODE3.PRG</c>, in bytes.</param>
public readonly record struct UploadPair(int Offset, uint Destination, uint Source);

/// <summary>A colour upload table in <c>EPISODE3.PRG</c>: its file offset and its pairs, in file order.</summary>
public sealed record UploadTable(int Offset, IReadOnlyList<UploadPair> Pairs);

/// <summary>
/// VDP2 colour RAM as Episode 3 fills it for the colour-bank modes (0 and 4): 2048 RGB555 colours,
/// copied from one of <c>EPISODE3.PRG</c>'s upload tables. A colour code no pair filled is black (0).
/// </summary>
/// <param name="Bytes">The 0x1000 bytes of colour RAM, big-endian words in Saturn order.</param>
/// <param name="Table">The table they were copied from.</param>
/// <param name="TableIndex">The table's place among those <c>EPISODE3.PRG</c> holds, from 0.</param>
/// <param name="TablesFound">How many tables <c>EPISODE3.PRG</c> holds, the chosen one among them.</param>
/// <param name="Skipped">The table's pairs left out, their source lying outside the file or their destination outside colour RAM.</param>
public sealed record ColourRam(byte[] Bytes, UploadTable Table, int TableIndex, int TablesFound, IReadOnlyList<UploadPair> Skipped)
{
    /// <summary>The RGB555 colour of a colour code; only its low 11 bits count.</summary>
    public ushort Colour(int code) => BinaryPrimitives.ReadUInt16BigEndian(Bytes.AsSpan((code & 0x7FF) * 2));

    /// <summary>
    /// One line, with no full stop, naming the table: <c>colour table at 0xE23C of EPISODE3.PRG (table 2 of 2), 5 blocks</c>,
    /// and any pair skipped: <c>; skipped: the pair at 0xE25C (destination 0x25F00800, source 0x07000000)</c>.
    /// </summary>
    public string Describe()
    {
        int blocks = Table.Pairs.Count - Skipped.Count;
        string line = string.Create(CultureInfo.InvariantCulture,
            $"colour table at 0x{Table.Offset:X} of {ColourBanks.ProgramFile} (table {TableIndex + 1} of {TablesFound}), {blocks} {(blocks == 1 ? "block" : "blocks")}");
        if (Skipped.Count > 0)
        {
            line += "; skipped: " + string.Join(", ", Skipped.Select(p => string.Create(CultureInfo.InvariantCulture,
                $"the pair at 0x{p.Offset:X} (destination 0x{p.Destination:X8}, source 0x{p.Source:X8})")));
        }

        return line;
    }
}

/// <summary>
/// Finding <c>EPISODE3.PRG</c>'s colour upload tables and building colour RAM from one, as
/// <c>zwei-disc-formats.md</c>, "Palettes for colour banks", says. Nothing is taken from a save state.
/// </summary>
public static class ColourBanks
{
    /// <summary>The disc file holding the tables and the colours they copy.</summary>
    public const string ProgramFile = "EPISODE3.PRG";

    /// <summary>The address <c>EPISODE3.PRG</c> is loaded at: a source address minus this is a file offset.</summary>
    public const uint LoadAddress = 0x06080000;

    /// <summary>VDP2 colour RAM's address: a destination minus this is a colour RAM byte offset.</summary>
    public const uint ColourRamAddress = 0x25F00000;

    /// <summary>Colour RAM's size in bytes: 2048 colours of two bytes.</summary>
    public const int ColourRamSize = 0x1000;

    /// <summary>The bytes each pair copies: 256 colours.</summary>
    public const int BlockSize = 0x200;

    /// <summary>
    /// Every upload table in <paramref name="prg"/>, in file order, found by its pattern: at an even
    /// offset, a big-endian destination of exactly <c>0x25F00000</c> followed by a source of the form
    /// <c>0x060xxxxx</c>, then pairs of 8 bytes until the first whose destination is not <c>0x25Fxxxxx</c>.
    /// The search goes on after each table's end.
    /// </summary>
    public static List<UploadTable> FindTables(ReadOnlySpan<byte> prg)
    {
        var tables = new List<UploadTable>();
        int o = 0;
        while (o + 8 <= prg.Length)
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(prg[o..]) != ColourRamAddress || BinaryPrimitives.ReadUInt32BigEndian(prg[(o + 4)..]) >> 20 != 0x060)
            {
                o += 2;
                continue;
            }

            var pairs = new List<UploadPair>();
            int p = o;
            while (p + 8 <= prg.Length)
            {
                uint destination = BinaryPrimitives.ReadUInt32BigEndian(prg[p..]);
                if (destination >> 20 != 0x25F)
                {
                    break;
                }

                pairs.Add(new UploadPair(p, destination, BinaryPrimitives.ReadUInt32BigEndian(prg[(p + 4)..])));
                p += 8;
            }

            tables.Add(new UploadTable(o, pairs));
            o = p;
        }

        return tables;
    }

    /// <summary>
    /// Which of <paramref name="found"/> tables colour RAM is built from: the second when there are two
    /// or more, as the author chose on 2026-10-17 (on GS-9049 V1.001 it is the one at 0xE23C, which the
    /// Episode 3 save state matches), else the only one.
    /// </summary>
    public static int Chosen(int found) => found >= 2 ? 1 : 0;

    /// <summary>
    /// Colour RAM after <paramref name="table"/>'s pairs are applied in order to all-black colour RAM,
    /// each copying 0x200 bytes. A pair whose source block lies outside <paramref name="prg"/>, or whose
    /// destination block lies outside colour RAM, is skipped and listed in the result.
    /// <paramref name="tableIndex"/> is the table's place, from 0, among the <paramref name="tablesFound"/> tables.
    /// </summary>
    public static ColourRam Build(ReadOnlySpan<byte> prg, UploadTable table, int tableIndex, int tablesFound)
    {
        var bytes = new byte[ColourRamSize];
        var skipped = new List<UploadPair>();
        foreach (UploadPair pair in table.Pairs)
        {
            long source = (long)pair.Source - LoadAddress;
            long destination = (long)pair.Destination - ColourRamAddress;
            if (source < 0 || source + BlockSize > prg.Length || destination < 0 || destination + BlockSize > ColourRamSize)
            {
                skipped.Add(pair);
                continue;
            }

            prg.Slice((int)source, BlockSize).CopyTo(bytes.AsSpan((int)destination));
        }

        return new ColourRam(bytes, table, tableIndex, tablesFound, skipped);
    }

    /// <summary>
    /// Colour RAM from <paramref name="prg"/>'s chosen table (see <see cref="Chosen"/>), or null and a
    /// one-line notice, with no full stop, saying why there is none. A table none of whose pairs could be
    /// applied counts as none: its colour RAM would be all black.
    /// </summary>
    public static (ColourRam? Colours, string? Notice) FromProgram(byte[]? prg)
    {
        if (prg == null)
        {
            return (null, $"the disc has no {ProgramFile}");
        }

        List<UploadTable> tables = FindTables(prg);
        if (tables.Count == 0)
        {
            return (null, $"{ProgramFile} holds no colour upload table");
        }

        int chosen = Chosen(tables.Count);
        ColourRam colours = Build(prg, tables[chosen], chosen, tables.Count);
        if (colours.Skipped.Count == tables[chosen].Pairs.Count)
        {
            return (null, $"{ProgramFile}'s colour table at 0x{tables[chosen].Offset:X} has no block inside the file and colour RAM");
        }

        return (colours, null);
    }

    /// <summary>
    /// As <see cref="FromProgram(byte[])"/>, reading <c>EPISODE3.PRG</c> from <paramref name="disc"/>. A file
    /// that cannot be read is treated as missing, with the reason in the notice, so modes 1 and 5 are still written.
    /// </summary>
    public static (ColourRam? Colours, string? Notice) FromDisc(DiscImage disc)
    {
        if (disc.Find(ProgramFile) is not DiscFile file)
        {
            return FromProgram(null);
        }

        byte[] prg;
        try
        {
            prg = disc.ReadFile(file);
        }
        catch (DecodingException e)
        {
            return (null, $"{ProgramFile} cannot be read ({e.Message})");
        }

        return FromProgram(prg);
    }
}
