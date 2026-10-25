using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PdzDecoding;

/// <summary>Where <c>EPISODE3.PRG</c> holds the floor and canopy: file offsets of the packed cells, each packed map, and the two colour blocks.</summary>
/// <param name="Cells">The packed cells' offset.</param>
/// <param name="Maps">Each packed map's offset, in the map list's order: plane A's (the floor) first, then plane B's (the canopy).</param>
/// <param name="Colours">The offsets of the colours copied to colour RAM 0xC00 (codes 0x600 to 0x6FF) and 0xE00 (codes 0x700 to 0x77F).</param>
public sealed record PlaneSources(int Cells, IReadOnlyList<int> Maps, IReadOnlyList<int> Colours)
{
    /// <summary>
    /// One line, with no full stop, naming the sources:
    /// <c>floor and canopy from EPISODE3.PRG: cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244</c>.
    /// </summary>
    public string Describe() =>
        $"floor and canopy from {ColourBanks.ProgramFile}: cells at {Hex(Cells)}, maps at {Join(Maps)}, colours at {Join(Colours)}";

    private static string Hex(int offset) => string.Create(CultureInfo.InvariantCulture, $"0x{offset:X}");

    private static string Join(IReadOnlyList<int> offsets) =>
        offsets.Count <= 2 ? string.Join(" and ", offsets.Select(Hex)) : string.Join(", ", offsets.Select(Hex));
}

/// <summary>The floor and canopy, each one 512x512 page as 8-bit RGBA, row-major, and where they came from.</summary>
public sealed record PlaneImages(PlaneSources Sources, byte[] Floor, byte[] Canopy);

/// <summary>
/// Episode 3's floor and canopy: the two rotation-parameter planes of VDP2's RBG0 layer, unpacked
/// from <c>EPISODE3.PRG</c> and drawn as VDP2 shows them, as <c>zwei-disc-formats.md</c>, "Floor and
/// canopy", says and <c>tools/plane_probe.py</c> does. Nothing is taken from a save state.
/// </summary>
/// <remarks>
/// The code that sets VDP2's registers for these planes is not traced, so the plane settings are
/// constants here, read from the Episode 3 save state's registers (the author's decision of
/// 2026-10-25): CHCTLB 0x1100 (RBG0 in 256 colours, 16x16-pixel cells), PNCR 0x8008 (one-word
/// pattern names, no auxiliary mode, supplement 8), MPOFR 0x33 with parameter A on map 0 and B on
/// map 1 (maps 0xC0 and 0xC1: VRAM 0x60000 and 0x60800), CRAOFB 0 (no colour RAM offset) and colour
/// RAM mode 1 (2048 RGB555 colours). Each image is the whole 512x512 page VDP2 holds.
/// </remarks>
public static class Planes
{
    /// <summary>The floor's asset ID: plane A.</summary>
    public const string FloorId = "e03.floor";

    /// <summary>The canopy's asset ID: plane B.</summary>
    public const string CanopyId = "e03.canopy";

    /// <summary>The width and height of each image, in pixels: one page of 32x32 cells of 16x16 pixels.</summary>
    public const int Size = PageCells * CellPixels;

    /// <summary>The cells' destination in the set-up code's literal pool: VDP2 VRAM bank B0.</summary>
    public const uint CellsVram = 0x25E40000;

    /// <summary>The maps' destination in the literal pool: VDP2 VRAM bank B1.</summary>
    public const uint MapsVram = 0x25E60000;

    /// <summary>VDP2 VRAM's address and size.</summary>
    private const uint VramAddress = 0x25E00000;

    private const int VramSize = 0x80000;

    /// <summary>One page of one-word pattern names, as each map unpacks to, one after another from <see cref="MapsVram"/>.</summary>
    private const int MapPage = 0x800;

    /// <summary>The colour copies the set-up code makes: colour RAM destination, colours copied, and how far before the literal pool's <see cref="CellsVram"/> its literal is.</summary>
    private static readonly (uint Destination, int Colours, int Before)[] ColourCopies =
    {
        (0x25F00C00, 256, 0x3C), // codes 0x600 to 0x6FF: the planes' colours
        (0x25F00E00, 128, 0x30), // codes 0x700 to 0x77F
    };

    // The VDP2 settings, from the save state's registers (see the remarks above).
    private const int PageCells = 32;          // CHCTLB 0x1100: 2x2 cells, so a page is 32x32 cells
    private const int CellPixels = 16;
    private const int Supplement = 0x8008 & 0x1F; // PNCR: the character number supplement
    private const int MapOffset = 3;           // MPOFR 0x33, for parameters A and B alike
    private static readonly int[] MapNumbers = { 0, 1 }; // MPABRA, MPABRB: plane A map 0, plane B map 1
    private const int ColourOffset = 0;        // CRAOFB 0

    /// <summary>
    /// Unpacks the LZ77 stream at <paramref name="pos"/> in <paramref name="data"/>, as 1ST_READ's routine
    /// at 0x0600FD1C does: flag bits least significant first, eight to a byte, the stream starting with a
    /// flag byte; 1 copies a literal byte; 0, 0 is a short copy (two more flags, the first high, give the
    /// length minus 2; the next byte is the offset minus 256); 0, 1 is a long copy (a little-endian word:
    /// its top 13 bits are the offset minus 8192, its low 3 the length minus 2, or when 0 the next byte
    /// plus 1 is the length); a long word of 0 ends the stream. Copies go byte by byte, so may overlap.
    /// Returns the bytes and how many packed bytes were read. Throws <see cref="DecodingException"/> for a
    /// stream that runs off the end of <paramref name="data"/>, copies from before its start, or unpacks
    /// to more than <paramref name="limit"/> bytes.
    /// </summary>
    public static (byte[] Bytes, int Packed) Unpack(ReadOnlySpan<byte> data, int pos, int limit = int.MaxValue)
    {
        int start = pos;
        var output = new List<byte>();
        if (pos < 0 || pos >= data.Length)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the stream at 0x{start:X} starts outside the file"));
        }

        int flags = data[pos++];
        int left = 9;

        int Next(ReadOnlySpan<byte> d, ref int p)
        {
            if (p >= d.Length)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the stream at 0x{start:X} runs off the end of the file"));
            }

            return d[p++];
        }

        int Bit(ReadOnlySpan<byte> d, ref int p)
        {
            left--;
            if (left == 0)
            {
                flags = Next(d, ref p);
                left = 8;
            }

            int b = flags & 1;
            flags >>= 1;
            return b;
        }

        while (true)
        {
            int length, offset;
            if (Bit(data, ref pos) == 1)
            {
                Add(output, (byte)Next(data, ref pos), limit, start);
                continue;
            }

            if (Bit(data, ref pos) == 0)
            {
                int high = Bit(data, ref pos);
                length = (high << 1 | Bit(data, ref pos)) + 2;
                offset = Next(data, ref pos) - 256;
            }
            else
            {
                int word = Next(data, ref pos);
                word |= Next(data, ref pos) << 8;
                if (word == 0)
                {
                    return (output.ToArray(), pos - start);
                }

                offset = (word >> 3) - 0x2000;
                length = (word & 7) != 0 ? (word & 7) + 2 : Next(data, ref pos) + 1;
            }

            if (-offset > output.Count)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the stream at 0x{start:X} copies from before its start"));
            }

            for (int i = 0; i < length; i++)
            {
                Add(output, output[output.Count + offset], limit, start);
            }
        }
    }

    private static void Add(List<byte> output, byte b, int limit, int start)
    {
        if (output.Count >= limit)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture, $"the stream at 0x{start:X} unpacks to more than 0x{limit:X} bytes"));
        }

        output.Add(b);
    }

    /// <summary>
    /// Finds the sources in <paramref name="prg"/> as the set-up code's literal pool names them, not by
    /// fixed offsets: at the first even offset with <see cref="CellsVram"/>, and <see cref="MapsVram"/>
    /// 12 bytes on, the word after the first is the cells' pointer and the word after the second the
    /// zero-ended map list's, each of which must point into the file; the colour RAM literals
    /// 0x25F00C00 and 0x25F00E00, 0x3C and 0x30 bytes before, are each followed by its colours' pointer.
    /// Throws <see cref="DecodingException"/> when any of them is not there or points outside the file.
    /// </summary>
    public static PlaneSources FindSources(ReadOnlySpan<byte> prg)
    {
        int pool = -1;
        for (int o = 0; o + 20 <= prg.Length; o += 2)
        {
            if (Word(prg, o) == CellsVram && Word(prg, o + 12) == MapsVram)
            {
                pool = o;
                break;
            }
        }

        if (pool < 0)
        {
            throw new DecodingException($"{ColourBanks.ProgramFile} holds no literal pool with the cells' and maps' VRAM addresses");
        }

        if (ToFile(prg, Word(prg, pool + 4)) is not int cells)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                $"the cells pointer 0x{Word(prg, pool + 4):X8} at 0x{pool + 4:X} is outside {ColourBanks.ProgramFile}"));
        }

        if (ToFile(prg, Word(prg, pool + 16)) is not int table)
        {
            throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                $"the map list pointer 0x{Word(prg, pool + 16):X8} at 0x{pool + 16:X} is outside {ColourBanks.ProgramFile}"));
        }

        var maps = new List<int>();
        while (true)
        {
            if (table + 4 > prg.Length)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                    $"the map list runs off the end of {ColourBanks.ProgramFile} at 0x{table:X}"));
            }

            uint entry = Word(prg, table);
            if (entry == 0)
            {
                break;
            }

            if (ToFile(prg, entry) is not int at)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                    $"map list entry 0x{entry:X8} at 0x{table:X} is outside {ColourBanks.ProgramFile}"));
            }

            maps.Add(at);
            table += 4;
        }

        var colours = new List<int>();
        foreach ((uint destination, int count, int before) in ColourCopies)
        {
            int literal = pool - before;
            int? at = literal >= 0 && Word(prg, literal) == destination ? ToFile(prg, Word(prg, literal + 4)) : null;
            if (at == null || at.Value + count * 2 > prg.Length)
            {
                throw new DecodingException(string.Create(CultureInfo.InvariantCulture,
                    $"no colour RAM 0x{destination:X8} source inside {ColourBanks.ProgramFile} 0x{before:X} bytes before the literal pool at 0x{pool:X}"));
            }

            colours.Add(at.Value);
        }

        return new PlaneSources(cells, maps, colours);
    }

    /// <summary>
    /// The floor and canopy from <paramref name="prg"/>: the sources found (see <see cref="FindSources"/>),
    /// the cells and maps unpacked into VDP2 VRAM as the set-up code does, the colours copied into colour
    /// RAM, and both planes drawn (see <see cref="Render"/>). Or null and a one-line notice, with no full
    /// stop, saying why there are none: no program, no sources, fewer than two maps, or a stream that is
    /// malformed or does not fit VRAM.
    /// </summary>
    public static (PlaneImages? Images, string? Notice) FromProgram(byte[]? prg)
    {
        if (prg == null)
        {
            return (null, $"the disc has no {ColourBanks.ProgramFile}");
        }

        PlaneSources sources;
        try
        {
            sources = FindSources(prg);
        }
        catch (DecodingException e)
        {
            return (null, e.Message);
        }

        if (sources.Maps.Count < 2)
        {
            return (null, string.Create(CultureInfo.InvariantCulture,
                $"{ColourBanks.ProgramFile}'s map list holds {sources.Maps.Count} {(sources.Maps.Count == 1 ? "map" : "maps")}, not the 2 the floor and canopy need"));
        }

        var vram = new byte[VramSize];
        var cram = new byte[ColourBanks.ColourRamSize];
        string stream = "cells";
        try
        {
            int cellsAt = (int)(CellsVram - VramAddress);
            byte[] cells = Unpack(prg, sources.Cells, VramSize - cellsAt).Bytes;
            cells.CopyTo(vram, cellsAt);
            for (int i = 0; i < sources.Maps.Count; i++)
            {
                stream = string.Create(CultureInfo.InvariantCulture, $"map {i}");
                int mapAt = (int)(MapsVram - VramAddress) + i * MapPage;
                if (mapAt >= VramSize)
                {
                    return (null, string.Create(CultureInfo.InvariantCulture,
                        $"{ColourBanks.ProgramFile}'s map {i} at 0x{sources.Maps[i]:X} would go past the end of VRAM"));
                }

                Unpack(prg, sources.Maps[i], VramSize - mapAt).Bytes.CopyTo(vram, mapAt);
            }
        }
        catch (DecodingException e)
        {
            return (null, $"{ColourBanks.ProgramFile}'s {stream}: {e.Message}");
        }

        for (int i = 0; i < ColourCopies.Length; i++)
        {
            prg.AsSpan(sources.Colours[i], ColourCopies[i].Colours * 2).CopyTo(cram.AsSpan((int)(ColourCopies[i].Destination - ColourBanks.ColourRamAddress)));
        }

        return (new PlaneImages(sources, Render(vram, cram, 0), Render(vram, cram, 1)), null);
    }

    /// <summary>
    /// One page of RBG0 rotation parameter A (<paramref name="which"/> 0, the floor) or B (1, the canopy)
    /// as 8-bit RGBA, row-major, <see cref="Size"/> pixels square, drawn from <paramref name="vram"/>
    /// (0x80000 bytes of VDP2 VRAM) and <paramref name="cram"/> (0x1000 bytes of colour RAM) with the
    /// settings in the remarks above. Each pattern name is read as VDP2 reads it: the cell from its low
    /// 10 bits with the supplement, horizontal and vertical flips from bits 10 and 11, the palette from
    /// bits 12 to 14. Every pixel is opaque, as <c>tools/plane_probe.py</c> draws them.
    /// </summary>
    public static byte[] Render(ReadOnlySpan<byte> vram, ReadOnlySpan<byte> cram, int which)
    {
        int map = MapOffset << 6 | MapNumbers[which] & 0x3F;
        int baseAddress = map * PageCells * PageCells * 2;
        var rgba = new byte[Size * Size * 4];
        for (int cy = 0; cy < PageCells; cy++)
        {
            for (int cx = 0; cx < PageCells; cx++)
            {
                int name = BinaryPrimitives.ReadUInt16BigEndian(vram[((baseAddress + (cy * PageCells + cx) * 2) & 0x7FFFF)..]);
                int flip = (name >> 10) & 3;
                int character = (name & 0x3FF) << 2 | Supplement & 3 | (Supplement & 0x1C) << 10;
                int palette = (name >> 12) & 7;
                for (int sub = 0; sub < 4; sub++)
                {
                    int cell = (character + sub * 2) * 0x20;
                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < 8; x++)
                        {
                            int colour = palette << 8 | vram[(cell + y * 8 + x) & 0x7FFFF];
                            int px = (sub & 1) * 8 + x;
                            int py = (sub >> 1) * 8 + y;
                            if ((flip & 1) != 0)
                            {
                                px = CellPixels - 1 - px;
                            }

                            if ((flip & 2) != 0)
                            {
                                py = CellPixels - 1 - py;
                            }

                            int o = ((cy * CellPixels + py) * Size + cx * CellPixels + px) * 4;
                            ushort word = BinaryPrimitives.ReadUInt16BigEndian(cram[(((colour + ColourOffset) & 0x7FF) * 2)..]);
                            Textures.Rgb555(word, rgba.AsSpan(o, 3));
                            rgba[o + 3] = 255;
                        }
                    }
                }
            }
        }

        return rgba;
    }

    private static uint Word(ReadOnlySpan<byte> prg, int offset) => BinaryPrimitives.ReadUInt32BigEndian(prg[offset..]);

    /// <summary>A load address as a file offset of <paramref name="prg"/>, or null when it is outside the file.</summary>
    private static int? ToFile(ReadOnlySpan<byte> prg, uint address) =>
        address >= ColourBanks.LoadAddress && address - ColourBanks.LoadAddress < (uint)prg.Length ? (int)(address - ColourBanks.LoadAddress) : null;
}
