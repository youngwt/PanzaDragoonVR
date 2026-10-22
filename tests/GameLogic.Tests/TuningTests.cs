using System;
using System.IO;
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
              "canopy_speed_scale": 0.5, "floor_tile": 3, "canopy_tile": 30 }
            """);

        TuningRead read = Tuning.Load(_folder);

        Assert.Equal(new Tuning(10f, 60.5f, 8f, 0.5f, 3f, 30f), read.Tuning);
        Assert.Empty(read.Problems);
        Assert.True(read.Found);
    }

    [Fact]
    public void SomeFields_UsesThoseAndTheBuiltInsElsewhere()
    {
        TuningRead read = Tuning.Parse("""{ "flight_speed": 6 }""");

        Assert.Equal(Tuning.BuiltIn with { FlightSpeed = 6f }, read.Tuning);
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
            "tuning.json: canopy_height must be a number from 2 to 1000; using the built-in 40",
            "tuning.json: floor_depth must be a number from 3 to 1000; using the built-in 4",
            "tuning.json: floor_tile must be a number from 0.1 to 1000; using the built-in 2",
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
    [InlineData("floor_depth", 3, 1000)]
    [InlineData("canopy_height", 2, 1000)]
    [InlineData("flight_speed", 0, 100)]
    [InlineData("canopy_speed_scale", 0, 10)]
    [InlineData("floor_tile", 0.1, 1000)]
    [InlineData("canopy_tile", 0.1, 1000)]
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
        // The floor 5.6 m below the eye and the canopy 38.4 m above it.
        Assert.Equal(4f * 38.4f / 5.6f, Tuning.BuiltIn.CanopySpeed, 1e-3f);
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
            "floor_depth 4, canopy_height 40, flight_speed 4, canopy_speed_scale 1, floor_tile 2, canopy_tile 20",
            Tuning.BuiltIn.Describe());
    }
}
