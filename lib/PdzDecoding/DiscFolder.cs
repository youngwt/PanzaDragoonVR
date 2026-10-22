using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PdzDecoding;

/// <summary>What <see cref="DiscFolder.Find"/> found in an asset folder's <c>disc/</c>.</summary>
public enum DiscFolderState
{
    /// <summary><c>disc/</c> is absent or holds no disc image: no <c>.cue</c> sheet and no <c>.iso</c>.</summary>
    None,

    /// <summary>One disc image, a <c>.cue</c> sheet or an <c>.iso</c>, in <see cref="DiscSearch.ImagePath"/>.</summary>
    One,

    /// <summary>More than one, named in <see cref="DiscSearch.Names"/>: nothing is extracted.</summary>
    Several,
}

/// <summary>The disc image found in <c>disc/</c>, if any: the <c>.cue</c> sheet or the <c>.iso</c>.</summary>
public readonly record struct DiscSearch(DiscFolderState State, string DiscFolder, string? ImagePath, IReadOnlyList<string> Names);

/// <summary>The <c>disc/</c> folder inside the asset folder, where the asset contract puts the player's disc image.</summary>
public static class DiscFolder
{
    /// <summary>The folder's name inside the asset folder, fixed by the asset contract.</summary>
    public const string Name = "disc";

    /// <summary>The extensions of a disc image: a cue sheet, which names its <c>.bin</c> files, or a plain ISO.</summary>
    public static readonly IReadOnlyList<string> ImageExtensions = new[] { ".cue", ".iso" };

    /// <summary>
    /// Looks for a disc image in <c>disc/</c> inside <paramref name="assetFolder"/>: a <c>.cue</c> sheet
    /// (its <c>.bin</c> files are part of it, not images of their own) or an <c>.iso</c>, matched without
    /// regard to case.
    /// </summary>
    public static DiscSearch Find(string assetFolder)
    {
        string folder = Path.Combine(assetFolder, Name);
        if (!Directory.Exists(folder))
        {
            return new DiscSearch(DiscFolderState.None, folder, null, Array.Empty<string>());
        }

        string[] images = Directory.EnumerateFiles(folder)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        string[] names = images.Select(f => Path.GetFileName(f)).ToArray();
        return images.Length switch
        {
            0 => new DiscSearch(DiscFolderState.None, folder, null, names),
            1 => new DiscSearch(DiscFolderState.One, folder, images[0], names),
            _ => new DiscSearch(DiscFolderState.Several, folder, null, names),
        };
    }

    /// <summary>
    /// The files the disc image at <paramref name="imagePath"/> consists of, in full, the image itself
    /// first: for a <c>.cue</c> sheet, the sheet and every file it names that exists inside the sheet's
    /// folder, at any depth (a file named outside it is not the image's own and is left out); for an
    /// <c>.iso</c>, the ISO alone. What release mode deletes. Throws <see cref="DecodingException"/>
    /// when the cue sheet cannot be read, or for any other extension.
    /// </summary>
    public static IReadOnlyList<string> ImageFiles(string imagePath)
    {
        string image = Path.GetFullPath(imagePath);
        if (!DiscImage.IsCueSheet(imagePath))
        {
            return new[] { image };
        }

        string folder = Path.GetDirectoryName(image) ?? ".";
        var files = new List<string> { image };
        files.AddRange(CueSheet.ReadFiles(image).Where(f =>
            !string.Equals(f, image, StringComparison.Ordinal) &&
            IsInside(f, folder) &&
            File.Exists(f)));
        return files;
    }

    /// <summary>Whether the full path <paramref name="file"/> lies inside <paramref name="folder"/>, at any depth.</summary>
    private static bool IsInside(string file, string folder)
    {
        string relative = Path.GetRelativePath(folder, file);
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
