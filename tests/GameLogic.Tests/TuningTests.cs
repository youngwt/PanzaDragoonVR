using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The tuning file: one test for each row of the story's matrix, then each setting's range ends.
/// </summary>
public sealed class TuningTests : IDisposable
{
    private readonly string _folder;

    public TuningTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "pdz-tuning-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        // Give the folder back its permissions in case a test took them away.
        string file = Path.Combine(_folder, Tuning.FileName);
        if (File.Exists(file) && !OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        Directory.Delete(_folder, recursive: true);
    }

    private void Write(string json) => File.WriteAllText(Path.Combine(_folder, Tuning.FileName), json);

    // ------------------------------------------------------------ the matrix

    [Fact]
    public void NoFile_GivesTheBuiltInsAndNoProblems()
    {
        TuningRead read = Tuning.Load(_folder);

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        Assert.Empty(read.Problems);
        Assert.False(read.Found);
    }

    [Fact]
    public void NoFolder_GivesTheBuiltInsAndNoProblems()
    {
        TuningRead read = Tuning.Load(null);

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        Assert.Empty(read.Problems);
    }

    [Fact]
    public void AllGood_UsesEveryValue()
    {
        Write("""
            { "floor_depth": 10, "canopy_height": 60.5, "flight_speed": 8,
              "canopy_speed_scale": 0.5, "floor_tile": 5, "canopy_tile": 30 }
            """);

        TuningRead read = Tuning.Load(_folder);

        Assert.Equal(new Tuning(10f, 60.5f, 8f, 0.5f, 5f, 30f), read.Tuning);
        Assert.Empty(read.Problems);
        Assert.True(read.Found);
    }

    [Fact]
    public void SomeFields_UsesThoseAndTheBuiltInsElsewhere()
    {
        TuningRead read = Tuning.Parse("""{ "flight_speed": 8 }""");

        Assert.Equal(Tuning.BuiltIn with { FlightSpeed = 8f }, read.Tuning);
        Assert.Empty(read.Problems);
    }

    [Theory]
    [InlineData("{flight", "is not valid JSON: ")]
    [InlineData("[1]", "must be one JSON object")]
    [InlineData("", "is not valid JSON: ")]
    public void NotAnObject_GivesTheBuiltInsAndOneProblem(string text, string reason)
    {
        TuningRead read = Tuning.Parse(text);

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        string problem = Assert.Single(read.Problems);
        Assert.StartsWith("tuning.json: cannot be read: " + reason, problem);
        Assert.DoesNotContain('\n', problem);
        Assert.False(problem.EndsWith('.'));
    }

    [Fact]
    public void OutOfRangeOrWrongType_KeepsTheBuiltInsForThoseAndUsesTheRest()
    {
        TuningRead read = Tuning.Parse("""
            { "canopy_height": 1, "floor_depth": 2, "floor_tile": "2", "flight_speed": 7 }
            """);

        Assert.Equal(Tuning.BuiltIn with { FlightSpeed = 7f }, read.Tuning);
        Assert.Equal(new[]
        {
            "tuning.json: canopy_height must be a number from 4 to 100; using the built-in 10",
            "tuning.json: floor_depth must be a number from 3 to 50; using the built-in 6",
            "tuning.json: floor_tile must be a number from 4 to 500; using the built-in 24",
        }, read.Problems);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("true")]
    [InlineData("[4]")]
    [InlineData("{}")]
    [InlineData("1e400")]
    public void NotANumber_IsAProblem(string value)
    {
        TuningRead read = Tuning.Parse($$"""{ "floor_depth": {{value}} }""");

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        Assert.Single(read.Problems);
    }

    [Fact]
    public void UnknownField_IsIgnoredAndNamed()
    {
        TuningRead read = Tuning.Parse("""{ "flight_sped": 6 }""");

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        Assert.Equal("tuning.json: 'flight_sped' is not a setting; it is ignored", Assert.Single(read.Problems));
    }

    [Fact]
    public void UnreadableFile_GivesTheBuiltInsAndOneProblemWithTheReason()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
        {
            return;
        }

        Write("""{ "flight_speed": 6 }""");
        File.SetUnixFileMode(Path.Combine(_folder, Tuning.FileName), UnixFileMode.None);

        TuningRead read = Tuning.Load(_folder);

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        string problem = Assert.Single(read.Problems);
        Assert.StartsWith("tuning.json: cannot be read: ", problem);
        Assert.EndsWith("; using the built-in values", problem);
        Assert.DoesNotContain('\n', problem);
    }

    // ------------------------------------------------------------ range ends

    [Theory]
    [InlineData("floor_depth", 3, 50)]
    [InlineData("canopy_height", 4, 100)]
    [InlineData("flight_speed", 0, 30)]
    [InlineData("canopy_speed_scale", 0, 2)]
    [InlineData("floor_tile", 4, 500)]
    [InlineData("canopy_tile", 8, 500)]
    public void RangeEnds_AreAllowedAndJustBeyondAreNot(string name, double min, double max)
    {
        string Shown(double v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);

        foreach (double inside in new[] { min, max })
        {
            TuningRead read = Tuning.Parse($$"""{ "{{name}}": {{Shown(inside)}} }""");
            Assert.Empty(read.Problems);
            Assert.NotEqual(Tuning.BuiltIn, read.Tuning);
        }

        foreach (double outside in new[] { min - 0.001, max + 0.001 })
        {
            TuningRead read = Tuning.Parse($$"""{ "{{name}}": {{Shown(outside)}} }""");
            Assert.Equal(Tuning.BuiltIn, read.Tuning);
            Assert.Contains($"{name} must be a number from ", Assert.Single(read.Problems));
        }
    }

    // ------------------------------------------------------------ the canopy's speed

    [Fact]
    public void CanopySpeed_WithTheBuiltIns_CrossesTheFloorsAngleFromTheEye()
    {
        // The floor 7.6 m below the eye and the canopy 8.4 m above it.
        Assert.Equal(6f * 8.4f / 7.6f, Tuning.BuiltIn.CanopySpeed, 1e-3f);
    }

    [Fact]
    public void CanopySpeed_IsScaled()
    {
        Assert.Equal(0f, (Tuning.BuiltIn with { CanopySpeedScale = 0f }).CanopySpeed);
        Assert.Equal(2f * Tuning.BuiltIn.CanopySpeed, (Tuning.BuiltIn with { CanopySpeedScale = 2f }).CanopySpeed, 1e-3f);
    }

    [Fact]
    public void Describe_NamesEverySettingAsTheFileDoes()
    {
        Assert.Equal(
            "floor_depth 6, canopy_height 10, flight_speed 6, canopy_speed_scale 1, floor_tile 24, canopy_tile 48",
            Tuning.BuiltIn.Describe());
    }

    // ------------------------------------------------------------ the example file

    [Fact]
    public void ExampleFile_GivesEverySettingAtItsBuiltInValue()
    {
        string text = File.ReadAllText(FindExample());

        TuningRead read = Tuning.Parse(text);

        Assert.Equal(Tuning.BuiltIn, read.Tuning);
        Assert.Empty(read.Problems);
        using JsonDocument json = JsonDocument.Parse(text);
        Assert.Equal(6, json.RootElement.EnumerateObject().Select(field => field.Name).Distinct().Count());
    }

    /// <summary><c>tools/tuning.example.json</c>, found by walking up from the test build's folder to the repository.</summary>
    private static string FindExample()
    {
        for (DirectoryInfo? here = new(AppContext.BaseDirectory); here != null; here = here.Parent)
        {
            string candidate = Path.Combine(here.FullName, "tools", "tuning.example.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"tools/tuning.example.json was not found above {AppContext.BaseDirectory}");
    }
}
