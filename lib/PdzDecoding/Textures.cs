using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PdzDecoding;

/// <summary>
/// One drawing record in a <c>.MDB</c>: the four big-endian VDP1 command words CMDPMOD, CMDCOLR,
/// CMDSRCA and CMDSIZE, found at <see cref="Offset"/>. CMDCOLR and CMDSRCA are VRAM addresses / 8.
/// </summary>
public readonly record struct TextureRecord(int Offset, ushort Pmod, ushort Colr, ushort Srca, ushort Size)
{
    /// <summary>Width in pixels: <c>(CMDSIZE &gt;&gt; 8) * 8</c>.</summary>
    public int Width => (Size >> 8) * 8;

    /// <summary>Height in pixels: <c>CMDSIZE &amp; 0xFF</c>.</summary>
    public int Height => Size & 0xFF;

    /// <summary>The colour mode, bits 3 to 5 of CMDPMOD: 1 is 16 colours through a lookup table.</summary>
    public int ColourMode => (Pmod >> 3) & 7;

    /// <summary>CMDPMOD bit 6 switches transparent pixels off, so index 0 is drawn.</summary>
    public bool IndexZeroTransparent => (Pmod & 0x40) == 0;

    /// <summary>The four words, which together say which texture this is.</summary>
    public (ushort, ushort, ushort, ushort) Words => (Pmod, Colr, Srca, Size);
}

/// <summary>A texture's place in its file, from <c>0</c>, and the first record that draws it.</summary>
public readonly record struct NumberedTexture(int Number, TextureRecord Record);

/// <summary>
/// Finding, numbering and decoding the textures of a <c>.MDB</c>/<c>.CGB</c> pair, ported from
/// <c>tools/cgb_textures.py</c>. The finding and numbering fix the asset contract's texture IDs, so
/// any change to them renumbers the textures and needs the author's agreement.
/// </summary>
public static class Textures
{
    /// <summary>
    /// Every plausible drawing record, in file order: at each even offset, four words whose CMDPMOD
    /// has 0x04 in its high byte, whose CMDSIZE has its top two bits clear, and whose width and
    /// height are not 0. As in the prototype, the last 8 bytes of the file are never a record start.
    /// </summary>
    public static List<TextureRecord> FindRecords(ReadOnlySpan<byte> mdb)
    {
        var found = new List<TextureRecord>();
        for (int o = 0; o + 8 < mdb.Length; o += 2)
        {
            ushort pmod = BinaryPrimitives.ReadUInt16BigEndian(mdb[o..]);
            ushort size = BinaryPrimitives.ReadUInt16BigEndian(mdb[(o + 6)..]);
            if ((pmod & 0xFF00) != 0x0400 || size >> 14 != 0)
            {
                continue;
            }

            var record = new TextureRecord(o, pmod, BinaryPrimitives.ReadUInt16BigEndian(mdb[(o + 2)..]),
                BinaryPrimitives.ReadUInt16BigEndian(mdb[(o + 4)..]), size);
            if (record.Width != 0 && record.Height != 0)
            {
                found.Add(record);
            }
        }

        return found;
    }

    /// <summary>Bytes of pixel data a record's texture takes in the <c>.CGB</c>, by colour mode.</summary>
    public static int BytesNeeded(TextureRecord r) => r.ColourMode switch
    {
        0 or 1 => r.Width * r.Height / 2,
        5 => r.Width * r.Height * 2,
        _ => r.Width * r.Height,
    };

    /// <summary>
    /// Whether the record's pixels, and for mode 1 its 32-byte lookup table, lie inside a <c>.CGB</c>
    /// of <paramref name="cgbSize"/> bytes loaded at <paramref name="baseAddress"/> (VRAM address / 8).
    /// </summary>
    public static bool Fits(TextureRecord r, int baseAddress, int cgbSize)
    {
        long offset = ((long)r.Srca - baseAddress) * 8;
        if (offset < 0 || offset + BytesNeeded(r) > cgbSize)
        {
            return false;
        }

        long lut = ((long)r.Colr - baseAddress) * 8;
        return r.ColourMode != 1 || (lut >= 0 && lut <= cgbSize - 32);
    }

    /// <summary>
    /// The address the <c>.CGB</c> is loaded at (VRAM address / 8), which the file does not hold: of
    /// the records' CMDSRCA values, the lowest that makes the most records fit. Null with no records.
    /// </summary>
    public static int? InferBase(IReadOnlyList<TextureRecord> records, int cgbSize)
    {
        int? best = null;
        int bestScore = -1;
        foreach (int candidate in records.Select(r => (int)r.Srca).Distinct().OrderBy(s => s))
        {
            int score = records.Count(r => Fits(r, candidate, cgbSize));
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// The file's textures, numbered as the asset contract says: in the order their first record
    /// appears in the <c>.MDB</c>, a record whose four words all equal an earlier one's being the same
    /// texture. Only records that fit the <c>.CGB</c> at the inferred base are textures; records in
    /// every colour mode are numbered, so decoding more modes later does not renumber.
    /// </summary>
    public static List<NumberedTexture> Number(IReadOnlyList<TextureRecord> records, int baseAddress, int cgbSize)
    {
        var seen = new HashSet<(ushort, ushort, ushort, ushort)>();
        var textures = new List<NumberedTexture>();
        foreach (TextureRecord r in records.OrderBy(r => r.Offset))
        {
            if (Fits(r, baseAddress, cgbSize) && seen.Add(r.Words))
            {
                textures.Add(new NumberedTexture(textures.Count, r));
            }
        }

        return textures;
    }

    /// <summary>
    /// A mode 1 texture as 8-bit RGBA, row-major: 4-bit indices, high nibble first, into 16 RGB555
    /// words read from the <c>.CGB</c> at <c>(CMDCOLR - base) * 8</c>. Index 0 has alpha 0 unless
    /// CMDPMOD bit 0x40 is set; every other pixel has alpha 255.
    /// </summary>
    public static byte[] DecodeMode1(ReadOnlySpan<byte> cgb, int baseAddress, TextureRecord r)
    {
        if (r.ColourMode != 1)
        {
            throw new ArgumentException($"the record at offset {r.Offset} is in colour mode {r.ColourMode}, not 1", nameof(r));
        }

        if (!Fits(r, baseAddress, cgb.Length))
        {
            throw new DecodingException($"the texture at .MDB offset {r.Offset} lies outside the .CGB");
        }

        int lut = (r.Colr - baseAddress) * 8;
        var palette = new byte[16 * 3];
        for (int i = 0; i < 16; i++)
        {
            Rgb555(BinaryPrimitives.ReadUInt16BigEndian(cgb[(lut + i * 2)..]), palette.AsSpan(i * 3, 3));
        }

        int offset = (r.Srca - baseAddress) * 8;
        bool transparent = r.IndexZeroTransparent;
        var rgba = new byte[r.Width * r.Height * 4];
        int p = 0;
        foreach (byte b in cgb.Slice(offset, r.Width * r.Height / 2))
        {
            foreach (int index in new[] { b >> 4, b & 15 })
            {
                rgba[p] = palette[index * 3];
                rgba[p + 1] = palette[index * 3 + 1];
                rgba[p + 2] = palette[index * 3 + 2];
                rgba[p + 3] = (byte)(transparent && index == 0 ? 0 : 255);
                p += 4;
            }
        }

        return rgba;
    }

    /// <summary>
    /// A mode 5 (true colour) texture as 8-bit RGBA, row-major: one big-endian 16-bit word per pixel,
    /// its low 15 bits RGB555 and alpha 255 when bit 15 is set, else 0, as <c>tools/cgb_textures.py</c>.
    /// </summary>
    public static byte[] DecodeMode5(ReadOnlySpan<byte> cgb, int baseAddress, TextureRecord r)
    {
        if (r.ColourMode != 5)
        {
            throw new ArgumentException($"the record at offset {r.Offset} is in colour mode {r.ColourMode}, not 5", nameof(r));
        }

        if (!Fits(r, baseAddress, cgb.Length))
        {
            throw new DecodingException($"the texture at .MDB offset {r.Offset} lies outside the .CGB");
        }

        int offset = (r.Srca - baseAddress) * 8;
        int pixels = r.Width * r.Height;
        var rgba = new byte[pixels * 4];
        for (int i = 0; i < pixels; i++)
        {
            ushort word = BinaryPrimitives.ReadUInt16BigEndian(cgb[(offset + i * 2)..]);
            Rgb555(word, rgba.AsSpan(i * 4, 3));
            rgba[i * 4 + 3] = (byte)((word & 0x8000) != 0 ? 255 : 0);
        }

        return rgba;
    }

    /// <summary>An RGB555 word (red in the low bits) as three 8-bit channels, each <c>c * 255 / 31</c>.</summary>
    public static void Rgb555(ushort word, Span<byte> rgb)
    {
        rgb[0] = (byte)((word & 31) * 255 / 31);
        rgb[1] = (byte)(((word >> 5) & 31) * 255 / 31);
        rgb[2] = (byte)(((word >> 10) & 31) * 255 / 31);
    }
}
