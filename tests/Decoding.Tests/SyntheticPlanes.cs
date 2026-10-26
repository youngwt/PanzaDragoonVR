using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PdzDecoding.Tests;

/// <summary>
/// Builds the floor and canopy's part of <c>EPISODE3.PRG</c>: packed cells and maps, their colours and
/// the literal pool the set-up code finds them by. Every byte is made here; nothing comes from the disc.
/// </summary>
internal static class SyntheticPlanes
{
    /// <summary>
    /// <paramref name="bytes"/> packed as the planes' LZ77 stream with literals only, then the end word:
    /// a flag byte goes in each time the unpacker needs eight more flags, where it reads it.
    /// </summary>
    public static byte[] PackLiterals(ReadOnlySpan<byte> bytes)
    {
        var packed = new List<byte> { 0 };
        int flagAt = 0, used = 0;
        void Flag(int bit)
        {
            if (used == 8)
            {
                flagAt = packed.Count;
                packed.Add(0);
                used = 0;
            }

            packed[flagAt] |= (byte)(bit << used++);
        }

        foreach (byte b in bytes)
        {
            Flag(1);
            packed.Add(b);
        }

        Flag(0);
        Flag(1);
        packed.Add(0);
        packed.Add(0);
        return packed.ToArray();
    }

    /// <summary>The colour the planes' block entry <paramref name="i"/> holds in <see cref="PlaneProgram"/>: block 0 is colour RAM 0xC00's 256, block 1 0xE00's 128.</summary>
    public static ushort PlaneColour(int block, int i) => (ushort)((block * 0x1234 + i * 0x0421 + 1) & 0x7FFF);

    /// <summary>The planes' unpacked cells in <see cref="PlaneProgram"/>: cell 0 blank, cells 1 and 2 of 256 bytes whose byte i is i and 255 - i.</summary>
    public static byte[] PlaneCells() =>
        new byte[0x100].Concat(Enumerable.Range(0, 256).Select(i => (byte)i)).Concat(Enumerable.Range(0, 256).Select(i => (byte)(255 - i))).ToArray();

    /// <summary>A page of 32x32 one-word pattern names, every one <paramref name="name"/> but (0, 0), which is <paramref name="first"/>.</summary>
    public static byte[] PlaneMap(ushort name, ushort first)
    {
        var map = new byte[0x800];
        for (int i = 0; i < 0x400; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(map.AsSpan(i * 2), i == 0 ? first : name);
        }

        return map;
    }

    /// <summary>The floor's map in <see cref="PlaneProgram"/>: cell 1 in palette 6, with cell 2 at (0, 0) flipped both ways.</summary>
    public static byte[] FloorMap() => PlaneMap(0x6002, 0x6C04);

    /// <summary>The canopy's map in <see cref="PlaneProgram"/>: cell 2 in palette 7.</summary>
    public static byte[] CanopyMap() => PlaneMap(0x7004, 0x7004);

    /// <summary>
    /// An <c>EPISODE3.PRG</c> holding the floor and canopy as the set-up code finds them: the colour RAM
    /// literals 0x3C and 0x30 bytes before a literal pool at <paramref name="pool"/>, the pool's cells
    /// pointer and map list pointer, a map list of <paramref name="maps"/> (the floor's and canopy's by
    /// default, or the load addresses given), the colours at 0x300 and 0x500, and the packed cells and
    /// maps from 0x600 (see <see cref="PackLiterals"/>), the file cut to <paramref name="size"/> bytes when given.
    /// </summary>
    public static byte[] PlaneProgram(int pool = 0x100, uint[]? maps = null, int? size = null)
    {
        const uint load = ColourBanks.LoadAddress;
        var prg = new List<byte>(new byte[0x600]);
        byte[] cells = PackLiterals(PlaneCells());
        byte[] floor = PackLiterals(FloorMap());
        byte[] canopy = PackLiterals(CanopyMap());
        int cellsAt = 0x600, floorAt = cellsAt + cells.Length, canopyAt = floorAt + floor.Length;
        prg.AddRange(cells);
        prg.AddRange(floor);
        prg.AddRange(canopy);
        byte[] bytes = prg.ToArray();
        void Put(int at, uint value) => BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(at), value);
        Put(pool - 0x3C, 0x25F00C00);
        Put(pool - 0x38, load + 0x300);
        Put(pool - 0x30, 0x25F00E00);
        Put(pool - 0x2C, load + 0x500);
        Put(pool, Planes.CellsVram);
        Put(pool + 4, load + (uint)cellsAt);
        Put(pool + 12, Planes.MapsVram);
        Put(pool + 16, load + 0x200);
        uint[] list = maps ?? new[] { load + (uint)floorAt, load + (uint)canopyAt };
        for (int i = 0; i < list.Length; i++)
        {
            Put(0x200 + i * 4, list[i]);
        }

        for (int i = 0; i < 256; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x300 + i * 2), PlaneColour(0, i));
        }

        for (int i = 0; i < 128; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(0x500 + i * 2), PlaneColour(1, i));
        }

        return size is int n ? bytes.AsSpan(0, n).ToArray() : bytes;
    }
}
