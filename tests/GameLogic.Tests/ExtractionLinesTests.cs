using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The progress board and the extraction failure lines, one or more tests per case the game words:
/// disc present (progress by disc file), no disc (the launch report as before),
/// a damaged image (cannot be read, or failed on a disc file) and a failure part way, as the asset
/// contract's extraction rows word them.
/// </summary>
public sealed class ExtractionLinesTests
{
    [Fact]
    public void Progress_DiscFile_ShowsTheStepByDiscFile()
    {
        Assert.Equal(new[]
        {
            "Extracting the assets from your disc image",
            "Disc image: Game.cue",
            "Reading E03B0N0 (2 of 9)",
            "Until it finishes, the scene shows what the asset folder already holds.",
        }, ExtractionLines.Progress("Game.cue", ExtractionStep.Reading, "E03B0N0", 2, 9));
        Assert.Equal("Writing E03END (9 of 9)", ExtractionLines.Progress("Game.cue", ExtractionStep.Writing, "E03END", 9, 9)[2]);
        Assert.Equal("Moving the new files into place", ExtractionLines.Progress("Game.cue", ExtractionStep.MovingIntoPlace, null, 9, 9)[2]);
    }

    [Fact]
    public void Starting_NamesTheImageBeforeAnyDiscFile()
    {
        Assert.Equal("Opening the disc image", ExtractionLines.Starting("Game.iso")[2]);
        Assert.Equal("Disc image: Game.iso", ExtractionLines.Starting("Game.iso")[1]);
    }

    [Fact]
    public void LogLine_IsOneLineWithNoFullStop()
    {
        Assert.Equal("extracting: reading E03CMN (1 of 9)", ExtractionLines.LogLine(ExtractionStep.Reading, "E03CMN", 1, 9));
        Assert.Equal("extracting: writing E03BOSS (8 of 9)", ExtractionLines.LogLine(ExtractionStep.Writing, "E03BOSS", 8, 9));
        Assert.Equal("extracting: moving the new files into place", ExtractionLines.LogLine(ExtractionStep.MovingIntoPlace, null, 9, 9));
    }

    [Fact]
    public void SeveralImages_NamesThemAndSaysNothingIsExtracted()
    {
        Assert.Equal("More than one disc image in /files/disc (Game.cue, Game.iso). Nothing is extracted; leave only one.",
            ExtractionLines.SeveralImages("/files/disc", new[] { "Game.cue", "Game.iso" }));
    }

    [Fact]
    public void DamagedImage_CannotBeReadWithTheReason()
    {
        Assert.Equal("The disc image cannot be read: Game.iso: no ISO9660 volume at sector 16.",
            ExtractionLines.Unreadable("Game.iso: no ISO9660 volume at sector 16"));
    }

    [Fact]
    public void FailurePartWay_NamesTheDiscFileAndKeepsThePreviousContents()
    {
        Assert.Equal("Extraction failed on E03B0N0: Game.iso: the image ends inside sector 40. The previous contents are kept.",
            ExtractionLines.Failed("E03B0N0", "Game.iso: the image ends inside sector 40"));
        Assert.Equal("Extraction failed: No space left on device. The previous contents are kept.",
            ExtractionLines.Failed(null, "No space left on device"));
    }

    [Fact]
    public void FailureWhileMoving_SaysSomeFilesMayBeInPlaceAndTheManifestIsKept()
    {
        Assert.Equal("Extraction failed while moving files into place: IOException: Is a directory. " +
            "Some new files may already be in place; the previous manifest is kept.",
            ExtractionLines.FailedMoving("IOException: Is a directory"));
    }

    [Fact]
    public void WithExtractionProblem_NothingElseToSay_IsAProblemBoard()
    {
        LaunchReport report = LaunchReport.Nothing.WithExtractionProblem("The disc image cannot be read: bad.");

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal(new[]
        {
            "Nothing was extracted from the disc image",
            "The disc image cannot be read: bad.",
            "The scene shows what the asset folder already holds.",
        }, report.ShownLines);
    }

    [Fact]
    public void WithExtractionProblem_NoManifest_KeepsTheNoticeUnderIt()
    {
        string folder = Path.Combine(Path.GetTempPath(), "pdz-none-" + Guid.NewGuid().ToString("N"));
        LaunchReport notice = LaunchReport.Build(folder, AssetManifest.Load(folder), Array.Empty<AssetLookup>());

        LaunchReport report = notice.WithExtractionProblem("Extraction failed on E03CMN: x. The previous contents are kept.");

        Assert.Equal(new[]
        {
            "Nothing was extracted from the disc image",
            "Extraction failed on E03CMN: x. The previous contents are kept.",
            "The scene shows what the asset folder already holds.",
            "Running on placeholders",
            "There is no manifest.json in the asset folder. This is not an error.",
            $"Asset folder: {folder}",
        }, report.ShownLines);
    }

    [Fact]
    public void WithExtractionProblem_ManyFolderProblems_StillShowsTheExtractionProblem()
    {
        LaunchReport failed = LaunchReport.Failed("/files", "one", Enumerable.Range(0, 10).Select(i => $"problem {i}"));

        LaunchReport report = failed.WithExtractionProblem("The disc image cannot be read: bad.");

        Assert.Equal("The disc image cannot be read: bad.", report.ShownLines[1]);
        Assert.Equal(failed.Problems, report.Problems);
        Assert.Equal(report.AllLines.Count, 3 + 1 + failed.Details.Count + failed.Problems.Count);
        Assert.EndsWith("the full list is in the log under PDZ-ASSETS", report.ShownLines[^1]);
    }
}
