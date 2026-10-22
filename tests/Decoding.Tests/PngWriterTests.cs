using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace PdzDecoding.Tests;

/// <summary>The deterministic RGBA PNG writer.</summary>
public sealed class PngWriterTests
{
    private static byte[] Pixels(int width, int height) =>
        Enumerable.Range(0, width * height * 4).Select(i => (byte)(i * 31 + i / 7)).ToArray();

    /// <summary>The file's chunks as (tag, data), each CRC checked.</summary>
    private static List<(string Tag, byte[] Data)> Chunks(byte[] png)
    {
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, png[..8]);
        var chunks = new List<(string, byte[])>();
        int i = 8;
        while (i < png.Length)
        {
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(i));
            byte[] tagAndData = png[(i + 4)..(i + 8 + length)];
            Assert.Equal(PngWriter.Crc32(tagAndData), BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(i + 8 + length)));
            chunks.Add((Encoding.ASCII.GetString(tagAndData, 0, 4), tagAndData[4..]));
            i += 12 + length;
        }

        return chunks;
    }

    private static byte[] Inflate(byte[] zlib)
    {
        using var output = new MemoryStream();
        using (var stream = new ZLibStream(new MemoryStream(zlib), CompressionMode.Decompress))
        {
            stream.CopyTo(output);
        }

        return output.ToArray();
    }

    [Fact]
    public void Crc32_GivesTheStandardCheckValue()
    {
        Assert.Equal(0xCBF43926u, PngWriter.Crc32(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Encode_WritesHeaderOneDataChunkAndEnd()
    {
        byte[] png = PngWriter.Encode(3, 2, Pixels(3, 2));

        var chunks = Chunks(png);
        Assert.Equal(new[] { "IHDR", "IDAT", "IEND" }, chunks.Select(c => c.Tag));
        Assert.Equal(new byte[] { 0, 0, 0, 3, 0, 0, 0, 2, 8, 6, 0, 0, 0 }, chunks[0].Data);
        Assert.Empty(chunks[2].Data);
        // The IEND chunk's CRC is the same in every PNG.
        Assert.Equal(new byte[] { 0xAE, 0x42, 0x60, 0x82 }, png[^4..]);
    }

    [Fact]
    public void Encode_StoresEveryRowWithFilterNone()
    {
        byte[] pixels = Pixels(3, 2);

        byte[] raw = Inflate(Chunks(PngWriter.Encode(3, 2, pixels))[1].Data);

        Assert.Equal(new byte[] { 0 }.Concat(pixels[..12]).Concat(new byte[] { 0 }).Concat(pixels[12..]), raw);
    }

    [Fact]
    public void Encode_GivesTheSameBytesEveryTime()
    {
        byte[] pixels = Pixels(40, 24);

        Assert.Equal(PngWriter.Encode(40, 24, pixels), PngWriter.Encode(40, 24, pixels.ToArray()));
    }

    [Fact]
    public void Encode_RejectsPixelsOfTheWrongLength()
    {
        Assert.Throws<ArgumentException>(() => PngWriter.Encode(2, 2, new byte[15]));
    }

    [Fact]
    public void Compress_SplitsLargeDataIntoStoredBlocks()
    {
        byte[] raw = Enumerable.Range(0, 200_000).Select(i => (byte)(i % 251)).ToArray();

        byte[] zlib = PngWriter.Compress(raw);

        Assert.Equal(raw, Inflate(zlib));
        // Header, four 5-byte block headers, the data and the Adler-32.
        Assert.Equal(2 + 4 * 5 + raw.Length + 4, zlib.Length);
    }
}
