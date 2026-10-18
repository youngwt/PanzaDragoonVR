using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PdzDecoding;

/// <summary>
/// Where a cue sheet puts the data track: the file holding track 1, the sector in that file where
/// the track starts, and the size of its sectors. Track 1 may have a file of its own or share one
/// with the tracks after it; either way it comes first. Only a MODE1/2352 or MODE1/2048 track 1 is read.
/// </summary>
/// <param name="BinPath">The full path of the file holding track 1.</param>
/// <param name="StartSector">The sector in that file where track 1's INDEX 01 is.</param>
/// <param name="SectorSize">The size of the track's sectors in the file: 2352 (raw) or 2048 (user data only).</param>
public readonly record struct DataTrack(string BinPath, long StartSector, int SectorSize);

/// <summary>Reads a <c>.cue</c> sheet far enough to find track 1, or to list the files it names.</summary>
public static class CueSheet
{
    /// <summary>The track 1 mode of a raw image: 2352-byte sectors with 2048 bytes of user data from byte 16.</summary>
    public const string RawMode = "MODE1/2352";

    /// <summary>The track 1 mode of an image of user data only: 2048-byte sectors.</summary>
    public const string CookedMode = "MODE1/2048";

    /// <summary>Finds track 1 in the cue sheet at <paramref name="cuePath"/>; file names are relative to its folder.</summary>
    public static DataTrack Read(string cuePath)
    {
        string text;
        try
        {
            text = File.ReadAllText(cuePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DecodingException($"{cuePath}: the cue sheet cannot be read: {DecodingException.OneLine(e.Message)}", e);
        }

        return Parse(text, cuePath);
    }

    /// <summary>Finds track 1 in cue sheet text; <paramref name="cuePath"/> names it in errors and anchors its file names.</summary>
    public static DataTrack Parse(string text, string cuePath)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(cuePath)) ?? ".";
        string? file = null;
        bool inTrack1 = false;
        int sectorSize = 0;
        foreach (string raw in text.Split('\n'))
        {
            List<string> words = Words(raw.Trim());
            if (words.Count == 0)
            {
                continue;
            }

            string command = words[0].ToUpperInvariant();
            if (command == "FILE" && words.Count >= 2)
            {
                file = words[1];
                inTrack1 = false;
            }
            else if (command == "TRACK" && words.Count >= 3)
            {
                if (inTrack1)
                {
                    // Track 1 ended without an INDEX 01.
                    break;
                }

                if (int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int number) && number == 1)
                {
                    if (string.Equals(words[2], RawMode, StringComparison.OrdinalIgnoreCase))
                    {
                        sectorSize = DiscImage.RawSectorSize;
                    }
                    else if (string.Equals(words[2], CookedMode, StringComparison.OrdinalIgnoreCase))
                    {
                        sectorSize = DiscImage.SectorSize;
                    }
                    else
                    {
                        throw new DecodingException($"{cuePath}: track 1 is {words[2]}, and only {RawMode} or {CookedMode} is read");
                    }

                    if (file == null)
                    {
                        throw new DecodingException($"{cuePath}: track 1 comes before any FILE line");
                    }

                    inTrack1 = true;
                }
            }
            else if (command == "INDEX" && inTrack1 && words.Count >= 3 && words[1] is "01" or "1")
            {
                long start = Frames(words[2], cuePath);
                string bin = Path.Combine(folder, file!);
                if (!File.Exists(bin))
                {
                    throw new DecodingException($"{cuePath}: names the file '{file}', which is missing");
                }

                return new DataTrack(bin, start, sectorSize);
            }
        }

        throw new DecodingException($"{cuePath}: no track 1 with an INDEX 01 was found");
    }

    /// <summary>
    /// Every file the cue sheet at <paramref name="cuePath"/> names in its <c>FILE</c> lines, in full,
    /// in the order named and each once, whether or not it exists; file names are relative to its folder.
    /// </summary>
    public static IReadOnlyList<string> ReadFiles(string cuePath)
    {
        string text;
        try
        {
            text = File.ReadAllText(cuePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new DecodingException($"{cuePath}: the cue sheet cannot be read: {DecodingException.OneLine(e.Message)}", e);
        }

        return Files(text, cuePath);
    }

    /// <summary>Every file cue sheet text names in its <c>FILE</c> lines, in full, in order and each once; <paramref name="cuePath"/> anchors the names.</summary>
    public static IReadOnlyList<string> Files(string text, string cuePath)
    {
        string folder = Path.GetDirectoryName(Path.GetFullPath(cuePath)) ?? ".";
        var files = new List<string>();
        foreach (string raw in text.Split('\n'))
        {
            List<string> words = Words(raw.Trim());
            if (words.Count >= 2 && string.Equals(words[0], "FILE", StringComparison.OrdinalIgnoreCase))
            {
                string file = Path.GetFullPath(Path.Combine(folder, words[1]));
                if (!files.Contains(file, StringComparer.Ordinal))
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }

    /// <summary>A cue time <c>mm:ss:ff</c> as a count of 75-a-second frames, which are sectors.</summary>
    private static long Frames(string time, string cuePath)
    {
        string[] parts = time.Split(':');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) &&
            int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int frames) &&
            seconds < 60 && frames < 75)
        {
            return ((long)minutes * 60 + seconds) * 75 + frames;
        }

        throw new DecodingException($"{cuePath}: '{time}' is not a cue time of the form mm:ss:ff");
    }

    /// <summary>Splits a line into words, a double-quoted word kept whole without its quotes.</summary>
    private static List<string> Words(string line)
    {
        var words = new List<string>();
        int i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i]))
            {
                i++;
                continue;
            }

            if (line[i] == '"')
            {
                int end = line.IndexOf('"', i + 1);
                if (end < 0)
                {
                    end = line.Length;
                }

                words.Add(line.Substring(i + 1, end - i - 1));
                i = end + 1;
                continue;
            }

            int start = i;
            while (i < line.Length && !char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            words.Add(line.Substring(start, i - start));
        }

        return words;
    }
}
