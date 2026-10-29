using System;
using System.Collections.Generic;

namespace PdzDecoding;

/// <summary>Where an atlas entry's own pixels are: which atlas, and the rectangle inside its border.</summary>
internal readonly record struct AtlasRect(int Atlas, int X, int Y, int Width, int Height);

/// <summary>One packed atlas: a <see cref="Size"/> x <see cref="Size"/> image of row-major RGBA bytes.</summary>
internal sealed record AtlasImage(int Size, byte[] Rgba);

/// <summary>
/// Packs a model's textures and flat colours into atlases as the asset contract's Models section
/// says: each entry with a 2-pixel border repeating its own edge pixels, pixels copied unchanged,
/// each atlas the smallest power-of-two square from 16 to 2048 that holds its entries. Entries are
/// stored in the order they are first added, textures de-duplicated by size and pixels and colours by
/// colour (a colour is a 1 x 1 block). Packing is by shelves, left to right and then down, in that
/// order with no sorting, and an entry that does not fit in a 2048 x 2048 atlas starts the next one.
/// The same entries in the same order always give the same atlases.
/// </summary>
internal sealed class Atlas
{
    public const int MaxSize = 2048;
    public const int MinSize = 16;
    public const int Border = 2;

    /// <summary>The widest or tallest texture an atlas can hold with its border.</summary>
    public const int MaxTexture = MaxSize - 2 * Border;

    private readonly List<(int Width, int Height, byte[] Rgba)> _entries = new();
    private readonly Dictionary<ModelTexture, int> _textures = new(new TextureContent());
    private readonly Dictionary<ModelColour, int> _colours = new();

    /// <summary>The entry for <paramref name="texture"/>, added if no entry has its size and pixels yet.</summary>
    public int Add(ModelTexture texture)
    {
        if (!_textures.TryGetValue(texture, out int entry))
        {
            entry = _entries.Count;
            _entries.Add((texture.Width, texture.Height, texture.Rgba));
            _textures.Add(texture, entry);
        }

        return entry;
    }

    /// <summary>The entry for a 1 x 1 block of <paramref name="colour"/>, added if it is new.</summary>
    public int Add(ModelColour colour)
    {
        if (!_colours.TryGetValue(colour, out int entry))
        {
            entry = _entries.Count;
            _entries.Add((1, 1, new[] { colour.R, colour.G, colour.B, (byte)255 }));
            _colours.Add(colour, entry);
        }

        return entry;
    }

    /// <summary>Packs every entry: the atlases, and each entry's rectangle by entry number.</summary>
    public (List<AtlasImage> Images, AtlasRect[] Rects) Pack()
    {
        // Split the entries into atlases, each as many as fit at the largest size, in order.
        var groups = new List<List<int>>();
        var shelf = new Shelf(MaxSize);
        for (int e = 0; e < _entries.Count; e++)
        {
            (int w, int h) = Padded(e);
            if (groups.Count == 0 || !shelf.Place(w, h, out _, out _))
            {
                groups.Add(new List<int>());
                shelf = new Shelf(MaxSize);
                if (!shelf.Place(w, h, out _, out _))
                {
                    // The writer checks texture sizes first, so this is a bug, not a bad model.
                    throw new InvalidOperationException($"atlas entry {e} is {w} x {h} with its border, larger than {MaxSize} x {MaxSize}");
                }
            }

            groups[^1].Add(e);
        }

        var images = new List<AtlasImage>();
        var rects = new AtlasRect[_entries.Count];
        foreach (List<int> group in groups)
        {
            int size = MinSize;
            (int X, int Y)[] places;
            while (!TryPack(group, size, out places))
            {
                size *= 2;
            }

            byte[] rgba = new byte[size * size * 4];
            for (int i = 0; i < group.Count; i++)
            {
                (int width, int height, byte[] pixels) = _entries[group[i]];
                Draw(rgba, size, places[i].X, places[i].Y, width, height, pixels);
                rects[group[i]] = new AtlasRect(images.Count, places[i].X + Border, places[i].Y + Border, width, height);
            }

            images.Add(new AtlasImage(size, rgba));
        }

        return (images, rects);
    }

    private (int Width, int Height) Padded(int entry) =>
        (_entries[entry].Width + 2 * Border, _entries[entry].Height + 2 * Border);

    private bool TryPack(List<int> group, int size, out (int X, int Y)[] places)
    {
        places = new (int, int)[group.Count];
        var shelf = new Shelf(size);
        for (int i = 0; i < group.Count; i++)
        {
            (int w, int h) = Padded(group[i]);
            if (!shelf.Place(w, h, out places[i].X, out places[i].Y))
            {
                return size >= MaxSize ? throw new InvalidOperationException("an atlas group does not fit at the largest size") : false;
            }
        }

        return true;
    }

    /// <summary>Copies a texture to (<paramref name="x"/> + 2, <paramref name="y"/> + 2) and fills its border with the nearest edge pixel.</summary>
    private static void Draw(byte[] atlas, int size, int x, int y, int width, int height, byte[] pixels)
    {
        for (int ty = -Border; ty < height + Border; ty++)
        {
            int sy = Math.Clamp(ty, 0, height - 1);
            for (int tx = -Border; tx < width + Border; tx++)
            {
                int sx = Math.Clamp(tx, 0, width - 1);
                Array.Copy(pixels, (sy * width + sx) * 4, atlas, ((y + Border + ty) * size + x + Border + tx) * 4, 4);
            }
        }
    }

    /// <summary>Shelf packing in a square of <c>size</c>: left to right, a new shelf below the tallest entry when a row is full.</summary>
    private sealed class Shelf
    {
        private readonly int _size;
        private int _x;
        private int _y;
        private int _height;

        public Shelf(int size) => _size = size;

        public bool Place(int width, int height, out int x, out int y)
        {
            x = y = 0;
            int nx = _x, ny = _y, nh = _height;
            if (nx + width > _size)
            {
                nx = 0;
                ny += nh;
                nh = 0;
            }

            if (nx + width > _size || ny + height > _size)
            {
                return false;
            }

            x = nx;
            y = ny;
            _x = nx + width;
            _y = ny;
            _height = Math.Max(nh, height);
            return true;
        }
    }

    /// <summary>Textures are the same entry when their size and pixels are the same.</summary>
    private sealed class TextureContent : IEqualityComparer<ModelTexture>
    {
        public bool Equals(ModelTexture? a, ModelTexture? b) =>
            ReferenceEquals(a, b) ||
            (a != null && b != null && a.Width == b.Width && a.Height == b.Height && a.Rgba.AsSpan().SequenceEqual(b.Rgba));

        public int GetHashCode(ModelTexture texture)
        {
            // Used only to find an equal texture, never to order anything written.
            var hash = new HashCode();
            hash.Add(texture.Width);
            hash.Add(texture.Height);
            hash.AddBytes(texture.Rgba);
            return hash.ToHashCode();
        }
    }
}
