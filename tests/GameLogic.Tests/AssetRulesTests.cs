using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The shared asset-rule cases in <c>tests/fixtures/asset-rules/</c>, read as the game reads them.
/// <c>tests/tools/test_asset_rules.py</c> runs the same folders through <c>tools/manifest.py</c>, so
/// the PC check and the game cannot drift apart unseen. The cases are written by <c>make_cases.py</c>
/// there; each folder's <c>expected.json</c> holds the problem lines.
/// </summary>
public sealed class AssetRulesTests
{
    private const string NotValidJsonCase = "unreadable_manifest";
    private const string WholeLineCase = "whole_line";

    public static string CasesFolder { get; } = FindCases();

    public static IEnumerable<object[]> Cases() =>
        Directory.GetDirectories(CasesFolder)
            .Where(folder => File.Exists(Path.Combine(folder, "expected.json")))
            .Select(folder => new object[] { Path.GetFileName(folder) })
            .OrderBy(name => (string)name[0], StringComparer.Ordinal);

    [Fact]
    public void CasesAreFound()
    {
        // A wrong path would otherwise make the theory below pass with no cases at all.
        Assert.True(Cases().Count() >= 30, $"too few cases found in {CasesFolder}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Case_GivesTheExpectedProblems(string name)
    {
        string folder = Path.Combine(CasesFolder, name);
        using JsonDocument expected = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "expected.json")));
        string[] lines = expected.RootElement.GetProperty("problems").EnumerateArray().Select(line => line.GetString()!).ToArray();
        bool unreadable = expected.RootElement.TryGetProperty(NotValidJsonCase, out JsonElement flag) && flag.GetBoolean();
        bool wholeLine = expected.RootElement.TryGetProperty(WholeLineCase, out JsonElement whole) && whole.GetBoolean();

        ManifestRead read = AssetManifest.Load(folder);
        LaunchReport report = LaunchReport.Build(folder, read, LookUpEveryEntry(folder, read));

        if (unreadable)
        {
            Assert.Equal(ManifestStatus.Unreadable, read.Status);
            string problem = Assert.Single(report.Problems);
            if (wholeLine)
            {
                Assert.Equal(Assert.Single(lines), problem);
            }
            else
            {
                Assert.StartsWith(Assert.Single(lines), problem, StringComparison.Ordinal);
            }
        }
        else
        {
            Assert.Equal(ManifestStatus.Read, read.Status);
            Assert.Equal(lines, report.Problems);
        }
    }

    /// <summary>
    /// Every entry looked up as the game would ask for it, with its own kind, in manifest order.
    /// Entries of a kind the game does not know, or whose ID or kind cannot be read, are not asked for.
    /// </summary>
    private static IEnumerable<AssetLookup> LookUpEveryEntry(string folder, ManifestRead read)
    {
        if (read.Status != ManifestStatus.Read)
        {
            return Array.Empty<AssetLookup>();
        }

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, AssetManifest.FileName)));
        var lookups = new List<AssetLookup>();
        foreach (JsonElement item in manifest.RootElement.GetProperty("assets").EnumerateArray())
        {
            if (Text(item, "id") is string id && Text(item, "kind") is string kind &&
                kind is AssetManifest.ImageKind or AssetManifest.ModelKind)
            {
                lookups.Add(read.Find(id, kind));
            }
        }

        return lookups;
    }

    private static string? Text(JsonElement item, string field)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty(field, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return value.GetString();
        }
        catch (InvalidOperationException)
        {
            return null; // a lone surrogate escape
        }
    }

    /// <summary>The cases folder, found by walking up from the test build's folder to the repository.</summary>
    private static string FindCases()
    {
        for (DirectoryInfo? here = new(AppContext.BaseDirectory); here != null; here = here.Parent)
        {
            string candidate = Path.Combine(here.FullName, "tests", "fixtures", "asset-rules");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"tests/fixtures/asset-rules was not found above {AppContext.BaseDirectory}");
    }
}
