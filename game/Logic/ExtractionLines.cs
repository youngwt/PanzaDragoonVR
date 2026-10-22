#nullable enable
using System.Collections.Generic;

namespace PanzaDragoonVR.Logic;

/// <summary>What extraction is doing, as the progress board and the log name it; the decoding library's stages, without its types.</summary>
public enum ExtractionStep
{
    /// <summary>Reading one disc file.</summary>
    Reading,

    /// <summary>Writing one disc file's images into the staging folder.</summary>
    Writing,

    /// <summary>Moving the new files into place, the manifest last.</summary>
    MovingIntoPlace,
}

/// <summary>
/// The words of extraction in the headset and the log: the progress board shown while it runs, and the
/// line the launch report gives when it fails, worded as the asset contract's extraction rows of
/// "What the game does at launch". Nothing here uses the engine or the decoding library, so it is tested on the PC.
/// </summary>
public static class ExtractionLines
{
    /// <summary>The progress board's first line.</summary>
    public const string Heading = "Extracting the assets from your disc image";

    /// <summary>
    /// The progress board: the heading, the disc image, the step under way and what the scene shows meanwhile.
    /// </summary>
    /// <param name="image">The disc image's file name.</param>
    /// <param name="step">The step under way.</param>
    /// <param name="discFile">The disc file it is on; ignored for <see cref="ExtractionStep.MovingIntoPlace"/>.</param>
    /// <param name="number">The disc file's place in the list, from 1.</param>
    /// <param name="count">How many disc files there are.</param>
    public static IReadOnlyList<string> Progress(string image, ExtractionStep step, string? discFile, int number, int count) => new[]
    {
        Heading,
        $"Disc image: {image}",
        Step(step, discFile, number, count),
        "Until it finishes, the scene shows what the asset folder already holds.",
    };

    /// <summary>The board's starting text, before the first disc file is reached.</summary>
    public static IReadOnlyList<string> Starting(string image) => new[]
    {
        Heading,
        $"Disc image: {image}",
        "Opening the disc image",
        "Until it finishes, the scene shows what the asset folder already holds.",
    };

    /// <summary>The step as the board shows it, with a capital: <c>Reading E03B0N0 (2 of 9)</c>.</summary>
    public static string Step(ExtractionStep step, string? discFile, int number, int count) => step switch
    {
        ExtractionStep.Reading => $"Reading {discFile} ({number} of {count})",
        ExtractionStep.Writing => $"Writing {discFile} ({number} of {count})",
        _ => "Moving the new files into place",
    };

    /// <summary>The step as the log gives it, after the tag, with no full stop: <c>extracting: reading E03B0N0 (2 of 9)</c>.</summary>
    public static string LogLine(ExtractionStep step, string? discFile, int number, int count)
    {
        string line = Step(step, discFile, number, count);
        return "extracting: " + char.ToLowerInvariant(line[0]) + line.Substring(1);
    }

    /// <summary>More than one disc image in <c>disc/</c>, with their names: nothing is extracted.</summary>
    public static string SeveralImages(string discFolder, IEnumerable<string> names) =>
        $"More than one disc image in {discFolder} ({string.Join(", ", names)}). Nothing is extracted; leave only one.";

    /// <summary>The disc image cannot be read, or is not a Saturn disc, with the reason.</summary>
    public static string Unreadable(string reason) => $"The disc image cannot be read: {reason}.";

    /// <summary>
    /// Extraction failed part way, with the disc file it was on, when it was on one, and the reason.
    /// Nothing was moved into place, so the previous contents are kept.
    /// </summary>
    public static string Failed(string? discFile, string reason) => discFile == null
        ? $"Extraction failed: {reason}. The previous contents are kept."
        : $"Extraction failed on {discFile}: {reason}. The previous contents are kept.";

    /// <summary>
    /// Extraction failed while moving the new files into place: some may already be in place, but the
    /// manifest is moved last, so the previous one is kept.
    /// </summary>
    public static string FailedMoving(string reason) =>
        $"Extraction failed while moving files into place: {reason}. Some new files may already be in place; the previous manifest is kept.";
}
