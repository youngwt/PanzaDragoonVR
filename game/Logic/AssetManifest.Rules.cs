#nullable enable
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PanzaDragoonVR.Logic;

// The contract's rules for an asset ID, a path and a PNG's first bytes, as tools/manifest.py has them.

public sealed partial class AssetManifest
{
    private static readonly Regex IdRule = new(@"\A[a-z0-9_]+(\.[a-z0-9_]+)*\z", RegexOptions.CultureInvariant);
    private static readonly Regex PathChars = new(@"\A[a-z0-9_.\-/]*\z", RegexOptions.CultureInvariant);
    private static readonly byte[] PngSignature = { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>Why <paramref name="id"/> is not a valid asset ID, or null.</summary>
    public static string? IdProblem(string id) =>
        IdRule.IsMatch(id) ? null : "an ID is lowercase letters, digits and underscores in levels separated by single dots";

    /// <summary>Why <paramref name="path"/> breaks the contract's path rules, or null. The same rules as <c>tools/manifest.py</c>.</summary>
    public static string? PathProblem(string path)
    {
        if (path.Length == 0)
        {
            return "a path must not be empty";
        }

        if (path.Contains('\\'))
        {
            return "a path must use forward slashes, not backslashes";
        }

        if (path.StartsWith('/'))
        {
            return "a path must be relative to the asset folder, with no leading slash";
        }

        string[] parts = path.Split('/');
        if (Array.IndexOf(parts, ".") >= 0 || Array.IndexOf(parts, "..") >= 0)
        {
            return "a path must not contain '.' or '..' parts";
        }

        if (Array.IndexOf(parts, "") >= 0)
        {
            return "a path must not contain empty parts";
        }

        if (!PathChars.IsMatch(path))
        {
            return "a path may hold only lowercase letters, digits, underscores, hyphens and dots";
        }

        return null;
    }

    /// <summary>Why the first bytes of a file are not an 8-bit RGB or RGBA PNG, or null.</summary>
    public static string? PngProblem(ReadOnlySpan<byte> head)
    {
        if (head.Length < 26 || !head[..8].SequenceEqual(PngSignature) || !head[12..16].SequenceEqual("IHDR"u8))
        {
            return "not a PNG file";
        }

        byte depth = head[24];
        byte colour = head[25];
        if (depth != 8 || (colour != 2 && colour != 6))
        {
            string name = colour switch
            {
                0 => "greyscale",
                2 => "RGB",
                3 => "palette",
                4 => "greyscale with alpha",
                6 => "RGBA",
                _ => $"colour type {colour}",
            };
            return $"a {depth}-bit {name} PNG; it must be 8-bit RGB or RGBA";
        }

        return null;
    }
}
