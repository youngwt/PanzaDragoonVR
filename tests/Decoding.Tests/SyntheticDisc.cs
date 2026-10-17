using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PdzDecoding.Tests;

/// <summary>
/// Builds small Saturn data tracks in 2352-byte MODE1 sectors, with a Saturn header at sector 0, an
/// ISO9660 primary volume descriptor at sector 16 and the given files, and the cue sheets naming them.
/// Every byte is made here; nothing comes from the disc.
/// </summary>
internal sealed class SyntheticDisc
{
    private const int RootSector = 18;

    private readonly List<(string Folder, string Name, byte[] Data)> _files = new();

    public string Product { get; init; } = "T-00000";

    public string Version { get; init; } = "V9.999";

    /// <summary>Ends the root directory's first sector after "." and "..", so its files are in its second.</summary>
    public bool PadRootDirectory { get; init; }

    /// <summary>Adds a file; <paramref name="folder"/> is "" for the root or one folder name.</summary>
    public SyntheticDisc With(string name, byte[] data, string folder = "")
    {
        _files.Add((folder, name, data));
        return this;
    }

    /// <summary>The track's raw bytes.</summary>
    public byte[] Build()
    {
        var sectors = new Dictionary<int, byte[]>();
        sectors[0] = Header();
        string[] folders = _files.Select(f => f.Folder).Where(f => f != "").Distinct().ToArray();
        int rootSectors = PadRootDirectory ? 2 : 1;
        int next = RootSector + rootSectors;
        var folderSectors = new Dictionary<string, int>();
        foreach (string folder in folders)
        {
            folderSectors[folder] = next++;
        }

        var fileSectors = new List<int>();
        foreach ((string _, string _, byte[] data) in _files)
        {
            fileSectors.Add(next);
            int count = Math.Max(1, (data.Length + 2047) / 2048);
            for (int i = 0; i < count; i++)
            {
                var sector = new byte[2048];
                data.AsSpan(i * 2048, Math.Min(2048, data.Length - i * 2048)).CopyTo(sector);
                sectors[next + i] = sector;
            }

            next += count;
        }

        // The root: ".", "..", its folders and its files.
        var root = new List<byte[]> { Record("\0", RootSector, 2048 * rootSectors, true), Record("\x01", RootSector, 2048 * rootSectors, true) };
        var rootEntries = new List<byte[]>();
        foreach (string folder in folders)
        {
            rootEntries.Add(Record(folder, folderSectors[folder], 2048, true));
        }

        for (int i = 0; i < _files.Count; i++)
        {
            if (_files[i].Folder == "")
            {
                rootEntries.Add(Record(_files[i].Name + ";1", fileSectors[i], _files[i].Data.Length, false));
            }
        }

        if (PadRootDirectory)
        {
            sectors[RootSector] = Directory(root);
            sectors[RootSector + 1] = Directory(rootEntries);
        }
        else
        {
            sectors[RootSector] = Directory(root.Concat(rootEntries));
        }

        foreach (string folder in folders)
        {
            var entries = new List<byte[]> { Record("\0", folderSectors[folder], 2048, true), Record("\x01", RootSector, 2048 * rootSectors, true) };
            for (int i = 0; i < _files.Count; i++)
            {
                if (_files[i].Folder == folder)
                {
                    entries.Add(Record(_files[i].Name + ";1", fileSectors[i], _files[i].Data.Length, false));
                }
            }

            sectors[folderSectors[folder]] = Directory(entries);
        }

        var pvd = new byte[2048];
        pvd[0] = 1;
        Encoding.ASCII.GetBytes("CD001").CopyTo(pvd, 1);
        pvd[6] = 1;
        Record("\0", RootSector, 2048 * rootSectors, true).CopyTo(pvd, 156);
        sectors[16] = pvd;

        int total = sectors.Keys.Max() + 1;
        var track = new byte[total * DiscImage.RawSectorSize];
        for (int s = 0; s < total; s++)
        {
            Span<byte> raw = track.AsSpan(s * DiscImage.RawSectorSize, DiscImage.RawSectorSize);
            // Sync, address and mode bytes, and the error correction after the data, must be skipped.
            raw[..16].Fill(0xEE);
            raw[(16 + 2048)..].Fill(0xDD);
            if (sectors.TryGetValue(s, out byte[]? data))
            {
                data.CopyTo(raw[16..]);
            }
        }

        return track;
    }

    /// <summary>Writes the track as <paramref name="binName"/> and a cue sheet naming it, returning the cue's path.</summary>
    public string WriteCue(string folder, string binName = "Game (Track 1).bin", string cueName = "Game.cue")
    {
        File.WriteAllBytes(Path.Combine(folder, binName), Build());
        string cue = Path.Combine(folder, cueName);
        File.WriteAllText(cue, Cue(binName));
        return cue;
    }

    /// <summary>A cue sheet for a per-track image whose track 1 is <paramref name="binName"/>, plus one audio track.</summary>
    public static string Cue(string binName, string mode = "MODE1/2352") =>
        $"CATALOG 0000000000000\r\nFILE \"{binName}\" BINARY\r\n  TRACK 01 {mode}\r\n    INDEX 01 00:00:00\r\n" +
        "FILE \"Game (Track 2).bin\" BINARY\r\n  TRACK 02 AUDIO\r\n    INDEX 00 00:00:00\r\n    INDEX 01 00:02:00\r\n";

    private byte[] Header()
    {
        var h = new byte[2048];
        void Put(int at, int length, string text) => Encoding.ASCII.GetBytes(text.PadRight(length)).CopyTo(h, at);
        Put(0, 16, "SEGA SEGASATURN");
        Put(16, 16, "SEGA TP T-000");
        Put(32, 10, Product);
        Put(42, 6, Version);
        Put(48, 8, "20261017");
        Put(96, 112, "SYNTHETIC TEST DISC");
        return h;
    }

    /// <summary>One directory record, padded to an even length.</summary>
    private static byte[] Record(string name, int sector, int size, bool isFolder)
    {
        byte[] nameBytes = Encoding.ASCII.GetBytes(name);
        int length = 33 + nameBytes.Length;
        length += length % 2;
        var r = new byte[length];
        r[0] = (byte)length;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(2), (uint)sector);
        BinaryPrimitives.WriteUInt32BigEndian(r.AsSpan(6), (uint)sector);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(10), (uint)size);
        BinaryPrimitives.WriteUInt32BigEndian(r.AsSpan(14), (uint)size);
        r[25] = (byte)(isFolder ? 2 : 0);
        r[32] = (byte)nameBytes.Length;
        nameBytes.CopyTo(r, 33);
        return r;
    }

    private static byte[] Directory(IEnumerable<byte[]> records)
    {
        var sector = new byte[2048];
        int at = 0;
        foreach (byte[] r in records)
        {
            r.CopyTo(sector, at);
            at += r.Length;
        }

        return sector;
    }
}

/// <summary>Builds <c>.MDB</c>/<c>.CGB</c> pairs: a lookup table and pixels in the <c>.CGB</c>, records pointing at them in the <c>.MDB</c>.</summary>
internal static class SyntheticTextures
{
    /// <summary>The load address used here, as VRAM address / 8 (0x8000 bytes).</summary>
    public const ushort Base = 0x1000;

    /// <summary>CMDPMOD for colour mode 1 with transparent pixels on.</summary>
    public const ushort Mode1 = 0x0408;

    /// <summary>
    /// A <c>.CGB</c> of 128 bytes: from offset 0, pixels counting 0, 1, ... 15, 0, 1 ... in 4-bit
    /// indices; then at offset 96 a 16-entry lookup table whose entry i has red i, green 31 - i and blue 1.
    /// </summary>
    public static byte[] Cgb()
    {
        var cgb = new byte[128];
        for (int i = 0; i < 96; i++)
        {
            cgb[i] = (byte)(((i * 2 % 16) << 4) | ((i * 2 + 1) % 16));
        }

        for (int i = 0; i < 16; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(cgb.AsSpan(96 + i * 2), (ushort)(i | ((31 - i) << 5) | (1 << 10)));
        }

        return cgb;
    }

    /// <summary>A mode 1 record for an 8 x <paramref name="height"/> texture at <c>.CGB</c> offset <paramref name="pixels"/>, table at <paramref name="lut"/>.</summary>
    public static (ushort Pmod, ushort Colr, ushort Srca, ushort Size) Record(int pixels = 0, int lut = 96, int height = 2, ushort pmod = Mode1) =>
        (pmod, (ushort)(Base + lut / 8), (ushort)(Base + pixels / 8), (ushort)((1 << 8) | height));

    /// <summary>An <c>.MDB</c> holding <paramref name="records"/> one after another, after 6 bytes of zeros and with 10 after.</summary>
    public static byte[] Mdb(params (ushort Pmod, ushort Colr, ushort Srca, ushort Size)[] records)
    {
        var mdb = new byte[6 + records.Length * 8 + 10];
        for (int i = 0; i < records.Length; i++)
        {
            Span<byte> at = mdb.AsSpan(6 + i * 8);
            BinaryPrimitives.WriteUInt16BigEndian(at, records[i].Pmod);
            BinaryPrimitives.WriteUInt16BigEndian(at[2..], records[i].Colr);
            BinaryPrimitives.WriteUInt16BigEndian(at[4..], records[i].Srca);
            BinaryPrimitives.WriteUInt16BigEndian(at[6..], records[i].Size);
        }

        return mdb;
    }
}
