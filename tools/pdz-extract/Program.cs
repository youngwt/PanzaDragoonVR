using System;
using System.IO;
using System.Linq;
using PdzDecoding;

// pdz-extract <disc.cue> <output folder>: writes the textures the game extracts on the headset,
// with the same names and bytes, into a folder on the PC. See tools/README.md.

const string Tool = "pdz-extract";
const string Usage = "usage: pdz-extract <disc.cue> <output folder>";

if (args.Length == 1 && args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    Console.WriteLine();
    Console.WriteLine("Reads the disc image the cue sheet names and writes the textures in colour modes");
    Console.WriteLine($"1 and 5 of {string.Join(", ", Extraction.TextureFiles)}");
    Console.WriteLine("as images/e03/<file>/<n>.png inside the output folder, as the game does in its");
    Console.WriteLine("asset folder on the headset.");
    return 0;
}

if (args.Length != 2)
{
    Console.Error.WriteLine($"{Tool}: error: expected a cue sheet and an output folder ({Usage})");
    return 1;
}

string cue = args[0];
string output = args[1];
if (!File.Exists(cue))
{
    Console.Error.WriteLine($"{Tool}: error: {cue}: no such file");
    return 1;
}

try
{
    ExtractionResult result = Extraction.Run(cue, output);
    Console.WriteLine($"{Tool}: disc {result.Source.Product} {result.Source.Version}");
    if (result.Files.Count == 0)
    {
        Console.WriteLine($"{Tool}: found no textures in colour modes 1 or 5: nothing written");
    }
    else
    {
        Console.WriteLine($"{Tool}: wrote {result.Files.Count} textures from {result.DiscFiles.Count(f => f.Files.Count > 0)} disc files to {result.Folder}");
    }

    foreach (DiscFileResult file in result.DiscFiles)
    {
        string leftOut = file.Skipped == 0 ? "" :
            " (" + string.Join(", ", file.LeftOut.Select(m => $"{m.Value} in mode {m.Key}")) + " not decoded yet, left out)";
        string what = file.Textures == 0 ? "no textures" : $"{file.Files.Count} written{leftOut}";
        Console.WriteLine($"{Tool}:   {file.Name}: {what}");
    }

    foreach (MissingDiscFile missing in result.Missing)
    {
        Console.WriteLine($"{Tool}:   {missing.Name}: not on the disc (no {missing.Absent})");
    }

    return 0;
}
catch (DecodingException e)
{
    Console.Error.WriteLine($"{Tool}: error: {e.Message}");
    return 1;
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"{Tool}: error: {DecodingException.OneLine(e.Message)}");
    return 1;
}
