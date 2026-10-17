using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace PdzDecoding;

/// <summary>
/// Writes 8-bit RGBA pixels as a PNG laid out as <c>tools/png.py</c> lays it out: the signature,
/// IHDR, one IDAT holding every row with filter 0, and IEND. The same pixels always give the same bytes.
/// </summary>
public static class PngWriter
{
    private static readonly byte[] Signature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

    private static readonly uint[] CrcTable = MakeCrcTable();

    /// <summary>The PNG file for a <paramref name="width"/> x <paramref name="height"/> image of row-major RGBA bytes.</summary>
    public static byte[] Encode(int width, int height, ReadOnlySpan<byte> rgba)
    {
        if (width <= 0 || height <= 0 || rgba.Length != (long)width * height * 4)
        {
            throw new ArgumentException($"{rgba.Length} bytes are not a {width} x {height} RGBA image", nameof(rgba));
        }

        int stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        for (int y = 0; y < height; y++)
        {
            // Each row starts with its filter type, 0 (none).
            rgba.Slice(y * stride, stride).CopyTo(raw.AsSpan(y * (stride + 1) + 1));
        }

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8;  // bits per channel
        header[9] = 6;  // RGBA
        // Compression, filter and interlace methods are all 0.

        using var png = new MemoryStream();
        png.Write(Signature);
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", Compress(raw));
        Chunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    /// <summary>The CRC-32 PNG chunks carry (ISO 3309, as zlib's <c>crc32</c>).</summary>
    public static uint Crc32(ReadOnlySpan<byte> data, uint crc = 0)
    {
        crc ^= 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFF;
    }

    /// <summary>
    /// A zlib stream, as PNG's IDAT holds, of stored (uncompressed) deflate blocks. .NET's own
    /// compressor gives different bytes on different runtimes: zlib-ng 2.2.1 in .NET 9 and 2.2.5 in
    /// .NET 10 on the PC, and Android's system zlib in the APK. Stored blocks are the same everywhere,
    /// at the cost of PNGs about the size of their raw pixels.
    /// </summary>
    public static byte[] Compress(ReadOnlySpan<byte> raw)
    {
        const int MaxBlock = 65535;
        int blocks = Math.Max(1, (raw.Length + MaxBlock - 1) / MaxBlock);
        var output = new byte[2 + blocks * 5 + raw.Length + 4];
        // CMF: deflate with a 32K window; FLG: no dictionary, fastest level, check bits making 0x7801 a multiple of 31.
        output[0] = 0x78;
        output[1] = 0x01;
        int o = 2;
        for (int b = 0; b < blocks; b++)
        {
            int start = b * MaxBlock;
            int length = Math.Min(MaxBlock, raw.Length - start);
            output[o] = (byte)(b == blocks - 1 ? 1 : 0); // BFINAL on the last block; BTYPE 00, stored
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(o + 1), (ushort)length);
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(o + 3), (ushort)~length);
            raw.Slice(start, length).CopyTo(output.AsSpan(o + 5));
            o += 5 + length;
        }

        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(o), Adler32(raw));
        return output;
    }

    /// <summary>The Adler-32 checksum that ends a zlib stream.</summary>
    public static uint Adler32(ReadOnlySpan<byte> data)
    {
        uint a = 1, b = 0;
        foreach (byte x in data)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static void Chunk(Stream png, string tag, byte[] data)
    {
        Span<byte> word = stackalloc byte[4];
        byte[] tagBytes = Encoding.ASCII.GetBytes(tag);
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        png.Write(word);
        png.Write(tagBytes);
        png.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32(data, Crc32(tagBytes)));
        png.Write(word);
    }

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
