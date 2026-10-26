#nullable enable
using PdzDecoding;

namespace PanzaDragoonVR;

/// <summary>
/// The asset IDs the game asks the asset folder for, grouped by the prefixes of the asset contract's
/// ID table. An ID is a promise: once the game asks for one, the extraction tool keeps writing it.
/// </summary>
public static class AssetIds
{
    /// <summary>Episode 3's floor; its placeholder is the green chequer.</summary>
    public const string E03Floor = Planes.FloorId;

    /// <summary>Episode 3's canopy; its placeholder is the blue chequer.</summary>
    public const string E03Canopy = Planes.CanopyId;

    /// <summary>Episode 3's flying enemy; its placeholder is the grey cube.</summary>
    public const string E03Flyer = "e03.flyer";
}
