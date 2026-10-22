#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace PanzaDragoonVR.Logic;

/// <summary>What the launch report has to say.</summary>
public enum LaunchReportKind
{
    /// <summary>Every image the folder lists was loaded, or the game needs none it lists: nothing to show.</summary>
    None,

    /// <summary>Running on placeholders by design, as with no <c>manifest.json</c>. Not an error.</summary>
    Notice,

    /// <summary>Something in the asset folder is wrong; each affected ID shows its placeholder.</summary>
    Problems,
}

/// <summary>
/// The launch check's findings as the player reads them in the headset, worded as the asset
/// contract's launch table words them (<c>spec-panzer-dragoon-zwei-vr/asset-contract.md</c>,
/// "What the game does at launch"). Built from what <see cref="AssetManifest"/> found; nothing
/// here checks the folder again. Nothing here uses the engine, so it is tested on the PC.
/// </summary>
public sealed class LaunchReport
{
    /// <summary>The most problems shown in the headset; the rest are counted, and all are in the log.</summary>
    public const int MaxShownProblems = 6;

    private LaunchReport(LaunchReportKind kind, string heading, IReadOnlyList<string> details, IReadOnlyList<string> problems)
    {
        Kind = kind;
        Heading = heading;
        Details = details;
        Problems = problems;
    }

    public LaunchReportKind Kind { get; }

    /// <summary>The first line: what the report is about. Empty when <see cref="Kind"/> is <see cref="LaunchReportKind.None"/>.</summary>
    public string Heading { get; }

    /// <summary>Lines under the heading that are not problems: the folder, and what is shown instead.</summary>
    public IReadOnlyList<string> Details { get; }

    /// <summary>Every problem, one line each, in the order found. Not shortened.</summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>
    /// The lines to show in the headset: the heading, the details, then the first
    /// <see cref="MaxShownProblems"/> problems and, if there are more, a line counting the rest.
    /// Empty when there is nothing to say.
    /// </summary>
    public IReadOnlyList<string> ShownLines
    {
        get
        {
            if (Kind == LaunchReportKind.None)
            {
                return Array.Empty<string>();
            }

            var lines = new List<string> { Heading };
            lines.AddRange(Details);
            lines.AddRange(Problems.Take(MaxShownProblems).Select(problem => "- " + problem));
            int rest = Problems.Count - MaxShownProblems;
            if (rest > 0)
            {
                lines.Add($"and {rest} more; the full list is in the log under PDZ-ASSETS");
            }

            return lines;
        }
    }

    /// <summary>
    /// Every line, with no problem left out: what goes to the log. Empty when there is nothing to say.
    /// </summary>
    public IReadOnlyList<string> AllLines =>
        Kind == LaunchReportKind.None
            ? Array.Empty<string>()
            : new[] { Heading }.Concat(Details).Concat(Problems.Select(problem => "- " + problem)).ToList();

    /// <summary>The report has nothing to say.</summary>
    public static LaunchReport Nothing { get; } =
        new(LaunchReportKind.None, "", Array.Empty<string>(), Array.Empty<string>());

    /// <summary>
    /// The report for an asset folder.
    /// </summary>
    /// <param name="folder">The asset folder, or null when the game was given none.</param>
    /// <param name="read">What came of reading its manifest.</param>
    /// <param name="lookups">The lookup of every ID the game uses, in the order it asked for them.</param>
    /// <param name="loadProblems">
    /// Problems found after a lookup succeeded, such as an image that could not be decoded,
    /// one line each with its ID.
    /// </param>
    public static LaunchReport Build(string? folder, ManifestRead read, IEnumerable<AssetLookup> lookups, IEnumerable<string>? loadProblems = null)
    {
        string where = folder ?? "(none given)";
        switch (read.Status)
        {
            case ManifestStatus.Absent:
                string reason = folder == null
                    ? "No asset folder was given"
                    : $"There is no {AssetManifest.FileName} in the asset folder";
                return new LaunchReport(LaunchReportKind.Notice, "Running on placeholders",
                    new[] { $"{reason}. This is not an error.", $"Asset folder: {where}" },
                    Array.Empty<string>());

            case ManifestStatus.Unreadable:
                return AllPlaceholders("The asset manifest cannot be read", where, read.Problem);

            case ManifestStatus.UnsupportedFormat:
                // The problem line carries the contract's wording; the heading only names the cause.
                return AllPlaceholders("The asset manifest's format is not supported", where, read.Problem);
        }

        // The manifest's own problems first, whether or not the game uses those IDs; then each ID's,
        // leaving out those the manifest already gave (a duplicate or invalid entry the game asked for).
        var problems = new List<string>(read.Manifest?.Problems ?? Array.Empty<string>());
        foreach (AssetLookup lookup in lookups)
        {
            if (lookup.Status == AssetLookupStatus.Problem && lookup.Problem != null && !problems.Contains(lookup.Problem))
            {
                problems.Add(lookup.Problem);
            }
        }

        problems.AddRange(loadProblems ?? Array.Empty<string>());
        if (problems.Count == 0)
        {
            return Nothing;
        }

        string heading = problems.Count == 1 ? "1 problem with the assets" : $"{problems.Count} problems with the assets";
        return new LaunchReport(LaunchReportKind.Problems, heading,
            new[] { "Each affected asset shows its placeholder.", $"Asset folder: {where}" }, problems);
    }

    /// <summary>
    /// The report when loading the asset folder failed in a way the checks did not foresee:
    /// the game runs on placeholders and says why, after any problems found before it failed.
    /// </summary>
    public static LaunchReport Failed(string? folder, string reason, IEnumerable<string>? foundBefore = null) =>
        AllPlaceholders("The asset folder could not be loaded", folder ?? "(not found)", reason, foundBefore);

    /// <summary>The heading of a report that leads with an extraction problem.</summary>
    public const string NothingExtracted = "Nothing was extracted from the disc image";

    /// <summary>
    /// This report with an extraction problem (see <see cref="ExtractionLines"/>) at its head: the problem
    /// and that the folder's own contents are shown, then this report's heading and details, then its
    /// problems. The extraction problem is a detail, not a problem, so it is always on the board however
    /// many problems the folder has.
    /// </summary>
    public LaunchReport WithExtractionProblem(string problem)
    {
        var details = new List<string> { problem, "The scene shows what the asset folder already holds." };
        if (Kind != LaunchReportKind.None)
        {
            details.Add(Heading);
            details.AddRange(Details);
        }

        return new LaunchReport(LaunchReportKind.Problems, NothingExtracted, details, Problems);
    }

    /// <summary>
    /// This report with the tuning file's problems (see <see cref="Tuning"/>) ahead of its own, so the
    /// board names them however many problems the folder has. With
    /// none, this report as it is. A report with nothing to say becomes one about the tuning file alone,
    /// so the board shows; any other keeps its heading and details, gains a line saying what the tuning
    /// problems mean and is shown as problems.
    /// </summary>
    public LaunchReport WithTuningProblems(IReadOnlyList<string> problems)
    {
        if (problems.Count == 0)
        {
            return this;
        }

        string count = problems.Count == 1 ? "1 problem" : $"{problems.Count} problems";
        if (Kind == LaunchReportKind.None)
        {
            return new LaunchReport(LaunchReportKind.Problems, $"{count} with {Tuning.FileName}",
                new[] { TuningMeaning }, problems.ToList());
        }

        return new LaunchReport(LaunchReportKind.Problems, Heading,
            Details.Append($"{count} with {Tuning.FileName}: {TuningMeaning.ToLowerInvariant()}").ToList(),
            problems.Concat(Problems).ToList());
    }

    private const string TuningMeaning = "Each affected setting uses its built-in value.";

    private static LaunchReport AllPlaceholders(string heading, string where, string? reason, IEnumerable<string>? foundBefore = null) =>
        new(LaunchReportKind.Problems, heading,
            new[] { "Running on placeholders for everything.", $"Asset folder: {where}" },
            (foundBefore ?? Array.Empty<string>()).Append(reason ?? "no reason was given").ToList());
}
