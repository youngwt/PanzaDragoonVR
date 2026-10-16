#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// The built-in placeholders for the ground and the sky, drawn in code: they stand in for
/// <see cref="AssetIds.E03Floor"/> and <see cref="AssetIds.E03Canopy"/>.
/// They are the author's own work: no image file, and nothing from the disc.
/// </summary>
public static class PlaceholderTextures
{
    private const int Size = 64;

    /// <summary>A tile of two greens in a chequer pattern, with a dark line round each square.</summary>
    public static ImageTexture Ground() => Chequer(
        light: new Color(0.33f, 0.47f, 0.24f),
        dark: new Color(0.26f, 0.39f, 0.20f),
        line: new Color(0.17f, 0.27f, 0.14f));

    /// <summary>The same pattern in a pale and a deeper blue, so the sky's tiling shows.</summary>
    public static ImageTexture Sky() => Chequer(
        light: new Color(0.80f, 0.88f, 0.96f),
        dark: new Color(0.47f, 0.66f, 0.88f),
        line: new Color(0.30f, 0.47f, 0.74f));

    /// <summary>Two by two squares of alternating colours, each with a line along two of its sides.</summary>
    private static ImageTexture Chequer(Color light, Color dark, Color line)
    {
        const int square = Size / 2;

        Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                bool onLine = x % square == 0 || y % square == 0;
                bool lightSquare = (x / square + y / square) % 2 == 0;
                image.SetPixel(x, y, onLine ? line : lightSquare ? light : dark);
            }
        }

        return Finish(image);
    }

    private static ImageTexture Finish(Image image)
    {
        // Smaller copies for the distance, so the far tiles do not shimmer in the headset.
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
