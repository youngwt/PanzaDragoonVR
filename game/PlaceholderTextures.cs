#nullable enable
using Godot;

namespace PanzaDragoonVR;

/// <summary>
/// The built-in placeholders for the ground and the sky, drawn in code.
/// They are the author's own work: no image file, and nothing from the disc.
/// </summary>
public static class PlaceholderTextures
{
    /// <summary>The asset ID the ground placeholder stands in for.</summary>
    public const string GroundAssetId = "e03.floor";

    /// <summary>The asset ID the sky placeholder stands in for.</summary>
    public const string SkyAssetId = "e03.canopy";

    private const int Size = 64;

    /// <summary>A tile of two greens in a chequer pattern, with a dark line round each square.</summary>
    public static ImageTexture Ground()
    {
        var light = new Color(0.33f, 0.47f, 0.24f);
        var dark = new Color(0.26f, 0.39f, 0.20f);
        var line = new Color(0.17f, 0.27f, 0.14f);
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

    /// <summary>A pale blue tile with a lighter diamond in the middle, so the sky's tiling shows.</summary>
    public static ImageTexture Sky()
    {
        var blue = new Color(0.47f, 0.66f, 0.88f);
        var pale = new Color(0.80f, 0.88f, 0.96f);
        const int centre = Size / 2;
        const int reach = Size / 3;

        Image image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgb8);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int distance = Mathf.Abs(x - centre) + Mathf.Abs(y - centre);
                image.SetPixel(x, y, distance <= reach ? pale : blue);
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
