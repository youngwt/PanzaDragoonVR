using System.Collections.Generic;
using System.Numerics;

namespace PdzDecoding;

/// <summary>How the Saturn draws a quad, which picks its material in the asset contract's Transparency table.</summary>
public enum DrawingMode
{
    /// <summary>No transparent pixels: material <c>opaque</c>.</summary>
    Opaque,

    /// <summary>Some pixels fully transparent: material <c>cutout</c>.</summary>
    Cutout,

    /// <summary>Half-transparent: material <c>half</c>.</summary>
    Half,

    /// <summary>A mesh pattern, every other pixel: material <c>mesh</c>.</summary>
    Mesh,
}

/// <summary>
/// A decoded texture: <paramref name="Width"/> x <paramref name="Height"/> row-major RGBA bytes,
/// as <see cref="Textures"/> decodes them (alpha 0 where the Saturn draws nothing, else 255). It is
/// copied into the atlas unchanged; two textures with the same size and pixels are stored once.
/// </summary>
public sealed record ModelTexture(int Width, int Height, byte[] Rgba);

/// <summary>A flat colour, drawn with alpha 255.</summary>
public readonly record struct ModelColour(byte R, byte G, byte B);

/// <summary>
/// One Saturn quad: corners <paramref name="A"/> to <paramref name="D"/> in the Saturn's corner
/// order, in glTF metres and axes relative to the part's origin. A polygon with three distinct
/// corners repeats one. With a <paramref name="Texture"/>, A takes its top-left corner, B its
/// top-right, C its bottom-right and D its bottom-left, after <paramref name="FlipH"/> swaps left
/// and right and <paramref name="FlipV"/> top and bottom; with none, the quad is
/// <paramref name="Colour"/> all over, and the flips change nothing that shows.
/// </summary>
public sealed record ModelQuad(
    Vector3 A,
    Vector3 B,
    Vector3 C,
    Vector3 D,
    ModelTexture? Texture,
    ModelColour Colour,
    DrawingMode Mode,
    bool FlipH = false,
    bool FlipV = false);

/// <summary>
/// One part of a model that moves on its own, turning about <paramref name="Origin"/> (in glTF
/// metres and axes, relative to the model's root). Its name is lowercase letters, digits and
/// underscores, unique within the model.
/// </summary>
public sealed record ModelPart(string Name, Vector3 Origin, IReadOnlyList<ModelQuad> Quads);

/// <summary>
/// A model as the asset contract's Models section describes it, already in glTF terms (metres,
/// +Y up, front facing +Z): the geometry decoder that fills it owns the conversion from the
/// Saturn's units and axes. <paramref name="Name"/> names the root node, the last level of the
/// asset ID (<c>flyer</c> for <c>e03.flyer</c>). <see cref="GlbWriter"/> writes it as a <c>.glb</c>.
/// </summary>
public sealed record Model(string Name, IReadOnlyList<ModelPart> Parts);
