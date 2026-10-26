using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The launch report built from a real folder, read as the game reads it: one or more tests for
/// each row of the story's matrix, worded as the asset contract's launch table.
/// </summary>
public sealed class LaunchReportTests : IDisposable
{
    private const string Floor = "e03.floor";
    private const string Canopy = "e03.canopy";

    private readonly string _folder;

    public LaunchReportTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pdz-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    /// <summary>The first bytes of an 8-bit RGBA PNG: all the lookup reads of an image.</summary>
    private static byte[] PngHead()
    {
        byte[] head = new byte[33];
        new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(head, 0);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(8), 13);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(head, 12);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(20), 1);
        head[24] = 8;
        head[25] = 6;
        return head;
    }

    private void Put(string path, byte[] contents)
    {
        string full = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, contents);
    }

    private void Manifest(string json) => Put(AssetManifest.FileName, Encoding.UTF8.GetBytes(json));

    private static string Entry(string id, string path, long bytes, string kind = "image") =>
        $$"""{ "id": "{{id}}", "kind": "{{kind}}", "path": "{{path}}", "bytes": {{bytes}} }""";

    private static string WithAssets(params string[] entries) =>
        $$"""{ "format": 1, "generator": "test", "source": null, "assets": [ {{string.Join(", ", entries)}} ] }""";

    private string Image(string id, string path)
    {
        byte[] png = PngHead();
        Put(path, png);
        return Entry(id, path, png.Length);
    }

    /// <summary>Reads the folder and looks up the two images, as the game does at launch.</summary>
    private LaunchReport Report()
    {
        ManifestRead read = AssetManifest.Load(_folder);
        return LaunchReport.Build(_folder, read, new[]
        {
            read.Find(Floor, AssetManifest.ImageKind),
            read.Find(Canopy, AssetManifest.ImageKind),
        });
    }

    // ------------------------------------------------------------ all good

    [Fact]
    public void BothImagesLoad_NoReport()
    {
        Manifest(WithAssets(Image(Floor, "images/e03.floor.png"), Image(Canopy, "images/e03.canopy.png")));

        LaunchReport report = Report();

        Assert.Equal(LaunchReportKind.None, report.Kind);
        Assert.Empty(report.ShownLines);
        Assert.Empty(report.AllLines);
    }

    [Fact]
    public void AnUnlistedId_IsNotAProblem()
    {
        Manifest(WithAssets(Image(Floor, "e03.floor.png")));

        Assert.Equal(LaunchReportKind.None, Report().Kind);
    }

    // ------------------------------------------------------------ empty folder

    [Fact]
    public void NoManifest_IsANoticeWithTheFolder_NotAProblem()
    {
        LaunchReport report = Report();

        Assert.Equal(LaunchReportKind.Notice, report.Kind);
        Assert.Equal("Running on placeholders", report.Heading);
        Assert.Empty(report.Problems);
        Assert.Contains(report.ShownLines, line => line.Contains(_folder));
        Assert.Contains(report.ShownLines, line => line.Contains("manifest.json") && line.Contains("not an error"));
    }

    [Fact]
    public void NoFolderGiven_IsANotice()
    {
        LaunchReport report = LaunchReport.Build(null, new ManifestRead(ManifestStatus.Absent, null, null), Array.Empty<AssetLookup>());

        Assert.Equal(LaunchReportKind.Notice, report.Kind);
        Assert.Contains(report.ShownLines, line => line.Contains("No asset folder was given"));
    }

    // ------------------------------------------------------------ one image missing

    [Fact]
    public void OneImageMissing_NamesItsIdAndPath_AndNotTheOther()
    {
        Manifest(WithAssets(Image(Floor, "images/e03.floor.png"), Image(Canopy, "images/e03.canopy.png")));
        File.Delete(Path.Combine(_folder, "images/e03.canopy.png"));

        LaunchReport report = Report();

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        string problem = Assert.Single(report.Problems);
        Assert.Contains(Canopy, problem);
        Assert.Contains("images/e03.canopy.png", problem);
        Assert.Contains("missing", problem);
        Assert.DoesNotContain(Floor, problem);
        Assert.Contains(report.ShownLines, line => line.Contains("placeholder"));
        Assert.Contains("- " + problem, report.ShownLines);
    }

    // ------------------------------------------------------------ truncated file

    [Fact]
    public void TruncatedFile_IsIncompleteOrChanged_WithIdAndPath()
    {
        byte[] png = PngHead();
        Put("images/e03.floor.png", png.Take(20).ToArray());
        Manifest(WithAssets(Entry(Floor, "images/e03.floor.png", png.Length), Image(Canopy, "images/e03.canopy.png")));

        string problem = Assert.Single(Report().Problems);

        Assert.Contains("incomplete or changed", problem);
        Assert.Contains(Floor, problem);
        Assert.Contains("images/e03.floor.png", problem);
    }

    // ------------------------------------------------------------ unreadable manifest

    [Theory]
    [InlineData("{ not json", "not valid JSON")]
    [InlineData("""{ "format": 1, "generator": "test", "source": null }""", "'assets' is absent")]
    public void UnreadableManifest_SaysSo_WithTheReason(string json, string reason)
    {
        Manifest(json);

        LaunchReport report = Report();

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal("The asset manifest cannot be read", report.Heading);
        Assert.Contains(reason, Assert.Single(report.Problems));
        Assert.Contains(report.ShownLines, line => line.Contains("placeholders for everything"));
    }

    [Fact]
    public void EmptyAssetList_CannotBeRead_ItListsNoAssets()
    {
        Manifest(WithAssets());

        LaunchReport report = Report();

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal("The asset manifest cannot be read", report.Heading);
        Assert.Equal("manifest.json: lists no assets", Assert.Single(report.Problems));
        Assert.Contains(report.ShownLines, line => line.Contains("placeholders for everything"));
        Assert.Contains("- manifest.json: lists no assets", report.AllLines);
    }

    [Fact]
    public void ManifestNotUtf8_CannotBeRead()
    {
        Put(AssetManifest.FileName, new byte[] { (byte)'{', 0xFF, (byte)'}' });

        LaunchReport report = Report();

        Assert.Equal("The asset manifest cannot be read", report.Heading);
        Assert.Contains("not UTF-8", Assert.Single(report.Problems));
    }

    // ------------------------------------------------------------ unsupported format

    [Fact]
    public void UnsupportedFormat_SaysANewerOrOlderToolMadeThem()
    {
        Manifest("""{ "format": 2, "generator": "test", "source": null, "assets": [] }""");

        LaunchReport report = Report();

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal("The asset manifest's format is not supported", report.Heading);
        string problem = Assert.Single(report.Problems);
        Assert.Contains("newer or older tool", problem);
        Assert.Contains("run the matching tool again", problem);
        // The contract's sentence is said once on the board, not in the heading as well.
        Assert.Single(report.ShownLines, line => line.Contains("newer or older tool"));
    }

    // ------------------------------------------------------------ many problems

    [Fact]
    public void ManyProblems_ShowTheFirstSeveral_ThenCountTheRest_AndLogThemAll()
    {
        const int total = LaunchReport.MaxShownProblems + 3;
        var entries = Enumerable.Range(1, total).Select(i => Entry($"e03.bad_{i}", $"bad/{i}..png", -1)).ToArray();
        Manifest(WithAssets(entries));

        LaunchReport report = Report();

        Assert.Equal(total, report.Problems.Count);
        Assert.Equal($"{total} problems with the assets", report.Heading);
        Assert.Equal(LaunchReport.MaxShownProblems, report.ShownLines.Count(line => line.StartsWith("- ")));
        Assert.Contains("and 3 more", report.ShownLines[^1]);
        Assert.Contains("- " + report.Problems[^1], report.AllLines);
        Assert.DoesNotContain("- " + report.Problems[^1], report.ShownLines);
    }

    [Fact]
    public void ExactlyTheMostShown_HasNoCountLine()
    {
        var entries = Enumerable.Range(1, LaunchReport.MaxShownProblems).Select(i => Entry($"e03.bad_{i}", "x.png", -1)).ToArray();
        Manifest(WithAssets(entries));

        LaunchReport report = Report();

        Assert.DoesNotContain(report.ShownLines, line => line.Contains("more"));
    }

    // ------------------------------------------------------------ problems are listed once each

    [Fact]
    public void DuplicateIdTheGameUses_IsListedOnce()
    {
        Manifest(WithAssets(Image(Floor, "a.png"), Image(Floor, "b.png")));

        string problem = Assert.Single(Report().Problems);

        Assert.Contains("duplicate ID", problem);
    }

    [Fact]
    public void EveryProblemIsListed_ManifestsAndEachIds()
    {
        Manifest(WithAssets(Entry("e03.unused", "/abs.png", 1), Entry(Floor, "f.png", 5), Entry(Canopy, "c.png", 5, kind: "model")));

        LaunchReport report = Report();

        Assert.Equal(3, report.Problems.Count);
        Assert.Contains(report.Problems, p => p.StartsWith("e03.unused: invalid entry"));
        Assert.Contains(report.Problems, p => p.StartsWith(Floor) && p.Contains("missing"));
        Assert.Contains(report.Problems, p => p.StartsWith(Canopy) && p.Contains("wrong kind"));
    }

    [Fact]
    public void LoadProblems_AreAddedAfterTheChecks()
    {
        Manifest(WithAssets(Image(Floor, "f.png")));
        ManifestRead read = AssetManifest.Load(_folder);

        LaunchReport report = LaunchReport.Build(_folder, read, new[] { read.Find(Floor, AssetManifest.ImageKind) },
            new[] { "e03.floor: f.png: unreadable image: the PNG could not be decoded" });

        Assert.Equal("1 problem with the assets", report.Heading);
        Assert.Contains("unreadable image", Assert.Single(report.Problems));
    }

    [Fact]
    public void LoadingFailed_IsAProblemWithTheReason()
    {
        LaunchReport report = LaunchReport.Failed(_folder, "IOException: boom");

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Contains(report.ShownLines, line => line.Contains("IOException: boom"));
        Assert.Contains(report.ShownLines, line => line.Contains(_folder));
    }

    [Fact]
    public void LoadingFailed_KeepsTheProblemsFoundBefore()
    {
        LaunchReport report = LaunchReport.Failed(_folder, "IOException: boom", new[] { "e03.canopy: c.png: missing" });

        Assert.Equal(new[] { "e03.canopy: c.png: missing", "IOException: boom" }, report.Problems);
    }

    // ------------------------------------------------------------ tuning problems

    private static readonly string[] TuningProblems =
    {
        "tuning.json: floor_depth must be a number from 3 to 1000; using the built-in 4",
    };

    [Fact]
    public void TuningProblems_None_LeaveTheReportAsItIs()
    {
        Assert.Same(LaunchReport.Nothing, LaunchReport.Nothing.WithTuningProblems(Array.Empty<string>()));
    }

    [Fact]
    public void TuningProblems_OnTheirOwn_ShowTheBoard()
    {
        LaunchReport report = LaunchReport.Nothing.WithTuningProblems(TuningProblems);

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal("1 problem with tuning.json", report.Heading);
        Assert.Equal(TuningProblems, report.Problems);
        Assert.Contains(report.ShownLines, line => line.Contains("floor_depth"));
    }

    [Fact]
    public void TuningProblems_KeepTheFoldersReport()
    {
        Manifest(WithAssets(Entry(Floor, "f.png", 33)));
        LaunchReport folder = Report();
        Assert.Equal("1 problem with the assets", folder.Heading);

        LaunchReport report = folder.WithTuningProblems(TuningProblems);

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal(folder.Heading, report.Heading);
        Assert.Equal(folder.Problems.Count + 1, report.Problems.Count);
        Assert.Equal(TuningProblems[0], report.Problems[0]);
        Assert.Contains(report.Details, line => line.StartsWith("1 problem with tuning.json"));
    }

    [Fact]
    public void TuningProblems_AreShownWhenTheFolderHasMoreThanTheBoardShows()
    {
        LaunchReport folder = LaunchReport.Failed(_folder, "IOException: boom",
            Enumerable.Range(1, LaunchReport.MaxShownProblems + 2).Select(i => $"e03.x{i}: missing"));

        LaunchReport report = folder.WithTuningProblems(TuningProblems);

        Assert.Contains(report.ShownLines, line => line.Contains("floor_depth"));
    }

    [Fact]
    public void TuningProblems_WithANotice_KeepTheNoticeAndShowAsProblems()
    {
        LaunchReport report = Report().WithTuningProblems(TuningProblems);

        Assert.Equal(LaunchReportKind.Problems, report.Kind);
        Assert.Equal("Running on placeholders", report.Heading);
        Assert.Equal(TuningProblems, report.Problems);
    }

    [Fact]
    public void TuningProblems_SurviveAnExtractionProblem()
    {
        LaunchReport report = LaunchReport.Nothing.WithTuningProblems(TuningProblems).WithExtractionProblem("disc.iso: boom");

        Assert.Equal(LaunchReport.NothingExtracted, report.Heading);
        Assert.Equal(TuningProblems, report.Problems);
        Assert.Contains("1 problem with tuning.json", report.Details);
    }
}
