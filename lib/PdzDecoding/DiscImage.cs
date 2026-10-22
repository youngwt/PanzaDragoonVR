using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PdzDecoding;

/// <summary>A file on the disc: its path from the root, with no <c>;1</c> version, its first sector and its size.</summary>
public readonly record struct DiscFile(string Path, uint Sector, uint Size);

/// <summary>The Saturn header in the first sector of the data track.</summary>
public readonly record struct SaturnHeader(string Hardware, string Maker, string Product, string Version, string Date, string Title);

/// <summary>
/// The ISO9660 file system of a Saturn data track, in 2352-byte raw MODE1 sectors whose user data
/// is the 2048 bytes from byte 16 of each, or in 2048-byte sectors of user data only. The Saturn
/// header is at sector 0, the primary volume descriptor at sector 16, and every directory is walked
/// when the image is opened.
/// </summary>
public sealed class DiscImage : IDisposable
{
    public const int RawSectorSize = 2352;
    public const int SectorHeader = 16;
    public const int SectorSize = 2048;
    public const int VolumeDescriptorSector = 16;

    private readonly Stream _stream;
    private readonly long _startSector;
    private readonly int _sectorSize;
    private readonly int _dataOffset;
    private readonly string _name;
    private readonly List<DiscFile> _files = new();

    private DiscImage(Stream stream, long startSector, int sectorSize, string name)
    {
        _stream = stream;
        _startSector = startSector;
        _sectorSize = sectorSize;
        _dataOffset = sectorSize == RawSectorSize ? SectorHeader : 0;
        _name = name;
    }

    /// <summary>Every file on the disc, sorted by path in ordinal order.</summary>
    public IReadOnlyList<DiscFile> Files => _files;

    /// <summary>
    /// Opens the disc image at <paramref name="path"/>: a <c>.cue</c> sheet, through the file holding
    /// its track 1, or an <c>.iso</c> of 2048-byte sectors. Any other extension is an error.
    /// </summary>
    public static DiscImage OpenImage(string path) => IsCueSheet(path) ? OpenCue(path) : OpenIso(path);

    /// <summary>
    /// Whether the disc image at <paramref name="path"/> is a <c>.cue</c> sheet (true) or an <c>.iso</c>
    /// (false), by its extension, matched without regard to case. Throws <see cref="DecodingException"/>
    /// for any other extension.
    /// </summary>
    internal static bool IsCueSheet(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cue" => true,
            ".iso" => false,
            _ => throw new DecodingException($"{path}: not a disc image: only a .cue sheet or an .iso is read"),
        };

    /// <summary>Opens the data track that the cue sheet at <paramref name="cuePath"/> names.</summary>
    public static DiscImage OpenCue(string cuePath)
    {
        DataTrack track = CueSheet.Read(cuePath);
        return Open(OpenFile(track.BinPath), track.StartSector, Path.GetFileName(track.BinPath), track.SectorSize);
    }

    /// <summary>Opens a plain ISO image: the data track alone, in 2048-byte sectors from the file's start.</summary>
    public static DiscImage OpenIso(string isoPath) =>
        Open(OpenFile(isoPath), 0, Path.GetFileName(isoPath), SectorSize);

    /// <summary>
    /// Reads the file system of a data track held in <paramref name="stream"/> from sector
    /// <paramref name="startSector"/>, its sectors being <paramref name="sectorSize"/> bytes: 2352
    /// (raw MODE1) or 2048 (user data only). The image owns the stream from here on.
    /// </summary>
    public static DiscImage Open(Stream stream, long startSector, string name, int sectorSize = RawSectorSize)
    {
        if (sectorSize is not (RawSectorSize or SectorSize))
        {
            stream.Dispose();
            throw new ArgumentOutOfRangeException(nameof(sectorSize), sectorSize, "a sector is 2352 or 2048 bytes");
        }

        var image = new DiscImage(stream, startSector, sectorSize, name);
        try
        {
            image.ReadFileSystem();
            return image;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    private static FileStream OpenFile(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DecodingException($"{path}: cannot be opened: {DecodingException.OneLine(e.Message)}", e);
        }
    }

    public void Dispose() => _stream.Dispose();

    /// <summary>
    /// Reads only the Saturn header, sector 0 of the data track, of the disc image at
    /// <paramref name="path"/>, a <c>.cue</c> sheet or an <c>.iso</c>, without walking its file system.
    /// Throws <see cref="DecodingException"/> when the image or its sector 0 cannot be read, or for any other extension.
    /// </summary>
    public static SaturnHeader ReadHeader(string path)
    {
        DataTrack track = IsCueSheet(path) ? CueSheet.Read(path) : new DataTrack(path, 0, SectorSize);
        using var image = new DiscImage(OpenFile(track.BinPath), track.StartSector, track.SectorSize, Path.GetFileName(track.BinPath));
        return image.Header();
    }

    /// <summary>The Saturn header at sector 0. Any disc is read; nothing here is checked.</summary>
    public SaturnHeader Header()
    {
        byte[] h = ReadSectors(0, 1);
        string Field(int start, int length) => Encoding.ASCII.GetString(h, start, length).Trim();
        return new SaturnHeader(Field(0, 16), Field(16, 16), Field(32, 10), Field(42, 6), Field(48, 8), Field(96, 112));
    }

    /// <summary>The file at <paramref name="path"/>, matched without regard to case, or null.</summary>
    public DiscFile? Find(string path) =>
        _files.Where(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)).Select(f => (DiscFile?)f).FirstOrDefault();

    /// <summary>The whole of <paramref name="file"/>.</summary>
    public byte[] ReadFile(DiscFile file)
    {
        long sectors = (file.Size + (long)SectorSize - 1) / SectorSize;
        byte[] data = ReadSectors(file.Sector, sectors);
        return data.Length == file.Size ? data : data.AsSpan(0, (int)file.Size).ToArray();
    }

    /// <summary>The 2048-byte user data of <paramref name="count"/> sectors from <paramref name="sector"/>.</summary>
    public byte[] ReadSectors(long sector, long count)
    {
        if (count * SectorSize > int.MaxValue)
        {
            throw new DecodingException($"{_name}: {count} sectors from sector {sector} are too many to read at once");
        }

        byte[] data = new byte[count * SectorSize];
        if (_sectorSize == SectorSize)
        {
            // User data only: the sectors lie end to end.
            _stream.Seek((_startSector + sector) * SectorSize, SeekOrigin.Begin);
            ReadFully(data, sector);
            return data;
        }

        for (long i = 0; i < count; i++)
        {
            _stream.Seek((_startSector + sector + i) * _sectorSize + _dataOffset, SeekOrigin.Begin);
            ReadFully(data.AsSpan((int)(i * SectorSize), SectorSize), sector + i);
        }

        return data;
    }

    /// <summary>Fills <paramref name="target"/> from the stream, whose position is in sector <paramref name="first"/>.</summary>
    private void ReadFully(Span<byte> target, long first)
    {
        int read = 0;
        while (read < target.Length)
        {
            int got = _stream.Read(target[read..]);
            if (got == 0)
            {
                throw new DecodingException($"{_name}: the image ends inside sector {first + read / SectorSize}");
            }

            read += got;
        }
    }

    private void ReadFileSystem()
    {
        if (_stream.Length < (_startSector + VolumeDescriptorSector + 1) * _sectorSize)
        {
            throw new DecodingException($"{_name}: no ISO9660 volume at sector {VolumeDescriptorSector}: the image is too short");
        }

        byte[] pvd = ReadSectors(VolumeDescriptorSector, 1);
        if (pvd[0] != 1 || Encoding.ASCII.GetString(pvd, 1, 5) != "CD001")
        {
            throw new DecodingException($"{_name}: no ISO9660 volume at sector {VolumeDescriptorSector}");
        }

        ReadOnlySpan<byte> root = pvd.AsSpan(156, 34);
        var visited = new HashSet<uint>();
        Walk(BinaryPrimitives.ReadUInt32LittleEndian(root[2..]), BinaryPrimitives.ReadUInt32LittleEndian(root[10..]), "", visited);
        _files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
    }

    /// <summary>Lists the directory at <paramref name="sector"/> and every directory below it.</summary>
    private void Walk(uint sector, uint size, string prefix, HashSet<uint> visited)
    {
        // A directory that names itself or an ancestor would otherwise loop for ever.
        if (!visited.Add(sector))
        {
            throw new DecodingException($"{_name}: the directory at sector {sector} is listed twice");
        }

        byte[] data = ReadSectors(sector, (size + (long)SectorSize - 1) / SectorSize);
        int i = 0;
        while (i < data.Length)
        {
            int length = data[i];
            if (length == 0)
            {
                // Records never cross a sector; the rest of this one is padding.
                i = (i / SectorSize + 1) * SectorSize;
                continue;
            }

            if (length < 34 || i + length > data.Length || 33 + data[i + 32] > length)
            {
                throw new DecodingException($"{_name}: a directory record at sector {sector + i / SectorSize} is malformed");
            }

            ReadOnlySpan<byte> record = data.AsSpan(i, length);
            uint entrySector = BinaryPrimitives.ReadUInt32LittleEndian(record[2..]);
            uint entrySize = BinaryPrimitives.ReadUInt32LittleEndian(record[10..]);
            ReadOnlySpan<byte> name = record.Slice(33, record[32]);
            // Names 0 and 1 are the directory itself and its parent.
            if (!(name.Length == 1 && name[0] <= 1))
            {
                string text = Encoding.ASCII.GetString(name).Split(';')[0];
                if ((record[25] & 2) != 0)
                {
                    Walk(entrySector, entrySize, prefix + text + "/", visited);
                }
                else
                {
                    _files.Add(new DiscFile(prefix + text, entrySector, entrySize));
                }
            }

            i += length;
        }
    }
}
