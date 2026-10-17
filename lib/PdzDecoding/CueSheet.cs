using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace PdzDecoding;

/// <summary>
/// Where a cue sheet puts the data track: the file holding track 1 and the sector in that file
/// where the track starts. Only a MODE1/2352 track 1 is read; other layouts come with story 4.
/// </summary>
/// <param name="BinPath">The full path of the file holding track 1.</param>
/// <param name="StartSector">The 2352-byte sector in that file where track 1's INDEX 01 is.</param>
public readonly record struct DataTrack(string BinPath, long StartSector);

/// <summary>Reads a <c>.cue</c> sheet far enough to find track 1.</summary>
public static class CueSheet
{
    /// <summary>The track mode the library reads: 2352-byte raw sectors with 2048 bytes of user data.</summary>
    public const string DataMode = "MODE1/2352";

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
                    if (!string.Equals(words[2], DataMode, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DecodingException($"{cuePath}: track 1 is {words[2]}, and only {DataMode} is read");
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

                return new DataTrack(bin, start);
            }
        }

        throw new DecodingException($"{cuePath}: no track 1 with an INDEX 01 was found");
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
