using System;
using System.IO;
using System.Linq;
using PdzDecoding;

// pdz-extract <disc.cue|disc.iso> <output folder>: writes the textures the game extracts on the headset,
// and the manifest listing them, with the same names and bytes, into a folder on the PC. See tools/README.md.

const string Tool = "pdz-extract";
const string Usage = "usage: pdz-extract <disc.cue|disc.iso> <output folder>";

if (args.Length == 1 && args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    Console.WriteLine();
    Console.WriteLine("Reads the disc image, a cue sheet with the .bin files it names or an ISO of");
    Console.WriteLine("2048-byte sectors, and writes the textures in colour modes 0, 1, 4 and 5");
    Console.WriteLine($"of {string.Join(", ", Extraction.TextureFiles)}");
    Console.WriteLine("as images/e03/<file>/<n>.png inside the output folder, as the game does in its");
    Console.WriteLine("asset folder on the headset, then manifest.json at the top of the output folder,");
    Console.WriteLine("listing them and replacing any manifest there. Modes 0 and 4 are coloured from");
    Console.WriteLine("a colour upload table in EPISODE3.PRG; with none, they are left out and a notice says why.");
    return 0;
}

if (args.Length != 2)
{
    Console.Error.WriteLine($"{Tool}: error: expected a disc image (.cue or .iso) and an output folder ({Usage})");
    return 1;
}

string image = args[0];
string output = args[1];
if (!File.Exists(image))
{
    Console.Error.WriteLine($"{Tool}: error: {image}: no such file");
    return 1;
}

try
{
    ExtractionResult result = Extraction.Run(image, output);
    Console.WriteLine($"{Tool}: disc {result.Source.Product} {result.Source.Version}");
    Console.WriteLine(result.Colours != null
        ? $"{Tool}: modes 0 and 4 coloured from the {result.ColourLine()}"
        : $"{Tool}: {result.ColourLine()}; those textures are left out");
    if (result.Files.Count == 0)
    {
        Console.WriteLine($"{Tool}: found no textures to write: nothing written");
    }
    else
    {
        Console.WriteLine($"{Tool}: wrote {result.Files.Count} textures from {result.DiscFilesWritten} disc files to {result.Folder}");
        Console.WriteLine($"{Tool}: wrote {result.ManifestPath} listing {result.Files.Count} assets");
    }

    foreach (DiscFileResult file in result.DiscFiles)
    {
        string leftOut = file.Skipped == 0 ? "" :
            " (" + string.Join(", ", file.LeftOut.Select(m => $"{m.Value} in mode {m.Key}")) + " left out)";
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
