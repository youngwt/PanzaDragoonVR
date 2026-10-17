using System;
using System.IO;
using PdzDecoding;

// pdz-extract <disc.cue> <output folder>: writes the textures the game extracts on the headset,
// with the same names and bytes, into a folder on the PC. See tools/README.md.

const string Tool = "pdz-extract";
const string Usage = "usage: pdz-extract <disc.cue> <output folder>";

if (args.Length == 1 && args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    Console.WriteLine();
    Console.WriteLine("Reads the disc image the cue sheet names and writes the mode 1 textures of");
    Console.WriteLine($"{Extraction.TextureFile} as images/e03/{Extraction.TextureFile.ToLowerInvariant()}/<n>.png inside the output folder,");
    Console.WriteLine("as the game does in its asset folder on the headset.");
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
        Console.WriteLine($"{Tool}: found no mode 1 textures in {result.DiscFileName}: nothing written");
        return 0;
    }

    Console.WriteLine($"{Tool}: wrote {result.Files.Count} textures from {result.DiscFileName} to {result.Folder}" +
        (result.Skipped == 0 ? "" : $" ({result.Skipped} in colour modes not decoded yet were left out)"));
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
