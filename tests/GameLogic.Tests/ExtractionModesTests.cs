using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The development and release modes of the asset contract, one or more tests per case the decision
/// covers: development (twice), release on a first launch, already extracted,
/// a different disc, release failure (the image kept), no disc, deletion failing, and a hand-written manifest;
/// with the extractor's prefix held to the library's generator, and the one-line rule the deletion lines use.
/// </summary>
public sealed class ExtractionModesTests : IDisposable
{
    private static readonly DiscSource Japanese = new("GS-9049", "V1.001");
    private static readonly ManifestOrigin Extracted = new("pdz-extract 0.1", Japanese);

    private readonly string _folder;

    public ExtractionModesTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pdz-modes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void ModeOf_Marker_IsDevelopment()
    {
        File.WriteAllBytes(Path.Combine(_folder, "development"), Array.Empty<byte>());

        Assert.Equal(ExtractionMode.Development, ExtractionModes.ModeOf(_folder));
    }

    [Fact]
    public void ModeOf_NoMarker_IsRelease()
    {
        Assert.Equal(ExtractionMode.Release, ExtractionModes.ModeOf(_folder));
    }

    [Fact]
    public void ModeOf_AFolderNamedDevelopment_IsRelease()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "development"));

        Assert.Equal(ExtractionMode.Release, ExtractionModes.ModeOf(_folder));
    }

    [Fact]
    public void Decide_Development_ExtractsAndKeepsTheImage()
    {
        Assert.Equal(new ExtractionDecision(ExtractionMode.Development, true, false),
            ExtractionModes.Decide(ExtractionMode.Development, null, null));
    }

    [Fact]
    public void Decide_DevelopmentAgain_ExtractsEvenFromTheSameDisc()
    {
        Assert.Equal(new ExtractionDecision(ExtractionMode.Development, true, false),
            ExtractionModes.Decide(ExtractionMode.Development, Extracted, Japanese));
    }

    [Fact]
    public void Decide_ReleaseFirstLaunch_ExtractsAndDeletes()
    {
        Assert.Equal(new ExtractionDecision(ExtractionMode.Release, true, true),
            ExtractionModes.Decide(ExtractionMode.Release, null, Japanese));
    }

    [Fact]
    public void Decide_ReleaseAlreadyExtracted_SkipsAndDeletes()
    {
        Assert.Equal(new ExtractionDecision(ExtractionMode.Release, false, true),
            ExtractionModes.Decide(ExtractionMode.Release, Extracted, Japanese));
    }

    [Theory]
    [InlineData("MK-81022", "V1.001")]
    [InlineData("GS-9049", "V1.000")]
    public void Decide_ReleaseDifferentDisc_Extracts(string product, string version)
    {
        ExtractionDecision decision = ExtractionModes.Decide(ExtractionMode.Release, Extracted, new DiscSource(product, version));

        Assert.True(decision.Extract);
        Assert.True(decision.DeleteImage);
    }

    [Theory]
    [InlineData("hand-written")]
    [InlineData("pdz-extractor 1.0")]
    [InlineData("pdz-extract")]
    [InlineData("PDZ-EXTRACT 0.1")]
    public void Decide_ReleaseManifestNotByTheExtractor_Extracts(string generator)
    {
        Assert.True(ExtractionModes.Decide(ExtractionMode.Release, new ManifestOrigin(generator, Japanese), Japanese).Extract);
    }

    [Fact]
    public void Decide_ReleaseManifestWithNoSource_Extracts()
    {
        Assert.True(ExtractionModes.Decide(ExtractionMode.Release, new ManifestOrigin("pdz-extract 0.1", null), Japanese).Extract);
    }

    [Fact]
    public void Decide_ReleaseUnreadableHeader_Extracts()
    {
        Assert.True(ExtractionModes.Decide(ExtractionMode.Release, Extracted, null).Extract);
    }

    [Fact]
    public void Decide_ReleaseAnotherExtractorVersion_SkipsOnTheSameDisc()
    {
        Assert.False(ExtractionModes.Decide(ExtractionMode.Release, new ManifestOrigin("pdz-extract 0.2", Japanese), Japanese).Extract);
    }

    [Fact]
    public void DeleteAfterExtraction_ReleaseSuccess_Deletes()
    {
        Assert.True(ExtractionModes.DeleteAfterExtraction(new ExtractionDecision(ExtractionMode.Release, true, true), succeeded: true));
    }

    [Fact]
    public void DeleteAfterExtraction_ReleaseFailure_KeepsTheImage()
    {
        Assert.False(ExtractionModes.DeleteAfterExtraction(new ExtractionDecision(ExtractionMode.Release, true, true), succeeded: false));
    }

    [Fact]
    public void DeleteAfterExtraction_Development_KeepsTheImage()
    {
        Assert.False(ExtractionModes.DeleteAfterExtraction(new ExtractionDecision(ExtractionMode.Development, true, false), succeeded: true));
    }

    [Fact]
    public void DeleteImage_AllFiles_DeletesThemAndNothingElse()
    {
        string[] files = Image("a.cue", "a (Track 1).bin", "a (Track 2).bin");
        File.WriteAllText(Path.Combine(_folder, "other.txt"), "");

        DeletionOutcome outcome = ExtractionModes.DeleteImage(files[0], files);

        Assert.Equal($"release mode: deleted the disc image {files[0]} (a.cue, a (Track 1).bin, a (Track 2).bin)", outcome.Line);
        Assert.Null(outcome.Warning);
        Assert.All(files, f => Assert.False(File.Exists(f)));
        Assert.True(File.Exists(Path.Combine(_folder, "other.txt")));
    }

    [Fact]
    public void DeleteImage_ImageItselfCannotGo_KeepsTheWholeImageInOneLine()
    {
        string[] files = Image("a.cue", "a (Track 1).bin", "a (Track 2).bin");

        DeletionOutcome outcome = ExtractionModes.DeleteImage(files[0], files, f =>
        {
            if (f == files[0])
            {
                throw new IOException("busy.");
            }

            File.Delete(f);
        });

        Assert.Null(outcome.Line);
        Assert.Equal($"release mode: the disc image {files[0]} could not be deleted (a.cue: IOException: busy); carrying on", outcome.Warning);
        Assert.All(files, f => Assert.True(File.Exists(f)));
    }

    [Fact]
    public void DeleteImage_ABinCannotGo_GivesOneLineAndDeletesTheRest()
    {
        string[] files = Image("a.cue", "a (Track 1).bin", "a (Track 2).bin", "a (Track 3).bin");

        DeletionOutcome outcome = ExtractionModes.DeleteImage(files[0], files, f =>
        {
            if (f != files[0])
            {
                throw new UnauthorizedAccessException("denied " + Path.GetFileName(f));
            }

            File.Delete(f);
        });

        Assert.Equal($"release mode: deleted the disc image {files[0]} (a.cue)", outcome.Line);
        Assert.Equal($"release mode: the disc image {files[0]} could not be deleted (" +
            "a (Track 1).bin: UnauthorizedAccessException: denied a (Track 1).bin; " +
            "a (Track 2).bin: UnauthorizedAccessException: denied a (Track 2).bin; " +
            "a (Track 3).bin: UnauthorizedAccessException: denied a (Track 3).bin); carrying on", outcome.Warning);
        Assert.False(File.Exists(files[0]));
        Assert.All(files.Skip(1), f => Assert.True(File.Exists(f)));
    }

    /// <summary>Writes empty files of these names in the test folder, returning their paths in order.</summary>
    private string[] Image(params string[] names)
    {
        string[] paths = names.Select(n => Path.Combine(_folder, n)).ToArray();
        foreach (string p in paths)
        {
            File.WriteAllBytes(p, Array.Empty<byte>());
        }

        return paths;
    }

    [Fact]
    public void ParseOrigin_ExtractorManifest_GivesGeneratorAndSource()
    {
        const string json = "{\n  \"format\": 1,\n  \"generator\": \"pdz-extract 0.1\",\n  \"source\": {\n    \"product\": \"GS-9049\",\n    \"version\": \"V1.001\"\n  },\n  \"assets\": []\n}\n";

        Assert.Equal(Extracted, ExtractionModes.ParseOrigin(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("{\"generator\": \"hand-written\", \"source\": null}")]
    [InlineData("{\"generator\": \"hand-written\"}")]
    [InlineData("{\"generator\": \"hand-written\", \"source\": {\"product\": \"GS-9049\"}}")]
    [InlineData("{\"generator\": \"hand-written\", \"source\": {\"product\": 1, \"version\": \"V1.001\"}}")]
    public void ParseOrigin_NoUsableSource_GivesNoSource(string json)
    {
        Assert.Equal(new ManifestOrigin("hand-written", null), ExtractionModes.ParseOrigin(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"source\": null}")]
    [InlineData("{\"generator\": 1}")]
    public void ParseOrigin_NoStringGenerator_GivesNull(string json)
    {
        Assert.Null(ExtractionModes.ParseOrigin(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void LoadOrigin_NoManifest_GivesNull()
    {
        Assert.Null(ExtractionModes.LoadOrigin(_folder));
    }

    [Fact]
    public void LoadOrigin_Manifest_ReadsIt()
    {
        File.WriteAllText(Path.Combine(_folder, "manifest.json"),
            "{\"generator\": \"pdz-extract 0.1\", \"source\": {\"product\": \"GS-9049\", \"version\": \"V1.001\"}}");

        Assert.Equal(Extracted, ExtractionModes.LoadOrigin(_folder));
    }

    [Fact]
    public void DecisionLine_EachDecision_NamesTheModeAndWhatIsDone()
    {
        Assert.Equal("development mode (development is in /f): extracting from /f/disc/a.cue in the background into /f; the disc image is kept",
            ExtractionModes.DecisionLine(new ExtractionDecision(ExtractionMode.Development, true, false), "/f/disc/a.cue", "/f", null));
        Assert.Equal("release mode: extracting from /f/disc/a.cue in the background into /f; the disc image is deleted after a successful extraction",
            ExtractionModes.DecisionLine(new ExtractionDecision(ExtractionMode.Release, true, true), "/f/disc/a.cue", "/f", Japanese));
        Assert.Equal("release mode: /f is already extracted from GS-9049 V1.001: not extracting again; deleting the disc image /f/disc/a.cue",
            ExtractionModes.DecisionLine(new ExtractionDecision(ExtractionMode.Release, false, true), "/f/disc/a.cue", "/f", Japanese));
    }

    [Fact]
    public void NoImageLine_EitherMode_NamesTheMode()
    {
        Assert.Equal("development mode: no disc image in /f/disc: nothing to extract", ExtractionModes.NoImageLine(ExtractionMode.Development, "/f/disc"));
        Assert.Equal("release mode: no disc image in /f/disc: nothing to extract", ExtractionModes.NoImageLine(ExtractionMode.Release, "/f/disc"));
    }

    [Fact]
    public void DeletionLines_AreOneLineWithNoFullStop()
    {
        Assert.Equal("release mode: deleted the disc image /f/disc/a.cue (a.cue, a.bin)", ExtractionModes.DeletedLine("/f/disc/a.cue", new[] { "a.cue", "a.bin" }));
        Assert.Equal("release mode: the disc image /f/disc/a.cue could not be deleted (a.bin: IOException: busy); carrying on",
            ExtractionModes.NotDeletedLine("/f/disc/a.cue", "a.bin: IOException: busy"));
    }

    [Fact]
    public void ExtractorPrefix_StartsTheLibrarysGenerator()
    {
        string generator = PdzDecoding.Manifest.Generator;

        Assert.StartsWith(ExtractionModes.ExtractorPrefix, generator, StringComparison.Ordinal);
        string version = generator.Substring(ExtractionModes.ExtractorPrefix.Length);
        Assert.NotEmpty(version);
        Assert.DoesNotContain(' ', version);
    }

    [Fact]
    public void OneLine_LineBreaks_BecomeOneSpace()
    {
        Assert.Equal("first second third", OneLine.Of("first\nsecond\rthird"));
    }

    [Fact]
    public void OneLine_WindowsLineBreak_BecomesOneSpace()
    {
        Assert.Equal("first second", OneLine.Of("first\r\nsecond"));
    }

    [Fact]
    public void OneLine_ClosingFullStop_IsDropped()
    {
        Assert.Equal("busy", OneLine.Of("busy."));
        Assert.Equal("busy", OneLine.Of("  busy. \n"));
    }

    [Fact]
    public void OneLine_RunsOfSpaces_BecomeOne()
    {
        Assert.Equal("a b c", OneLine.Of("a   b \t c"));
    }
}
