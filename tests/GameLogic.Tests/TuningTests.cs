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
              "canopy_speed_scale": 0.5, "floor_tile": 5, "canopy_tile": 30,
              "seat_forward": -1.5, "seat_height": 0.25, "dragon_scale": 2 }
            """);

        TuningRead read = Tuning.Load(_folder);

        Assert.Equal(new Tuning(10f, 60.5f, 8f, 0.5f, 5f, 30f, -1.5f, 0.25f, 2f), read.Tuning);
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
            "tuning.json: floor_depth must be a number from 3 to 50; using the built-in 8.999",
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
    [InlineData("seat_forward", -20, 20)]
    [InlineData("seat_height", -3, 3)]
    [InlineData("dragon_scale", 0.1, 5)]
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
        // The floor 10.599 m below the eye and the canopy 8.4 m above it.
        Assert.Equal(10.676f * 8.4f / 10.599f * 0.995f, Tuning.BuiltIn.CanopySpeed, 1e-3f);
    }

    [Fact]
    public void CanopySpeed_IsScaled()
    {
        Assert.Equal(0f, (Tuning.BuiltIn with { CanopySpeedScale = 0f }).CanopySpeed);
        Assert.Equal(2f * (Tuning.BuiltIn with { CanopySpeedScale = 1f }).CanopySpeed, (Tuning.BuiltIn with { CanopySpeedScale = 2f }).CanopySpeed, 1e-3f);
    }

    [Fact]
    public void Describe_NamesEverySettingAsTheFileDoes()
    {
        Assert.Equal(
            "floor_depth 8.999, canopy_height 10, flight_speed 10.676, canopy_speed_scale 0.995, floor_tile 24, canopy_tile 60.146, seat_forward -0.633, seat_height -0.146, dragon_scale 0.997",
            Tuning.BuiltIn.Describe());
    }

    // ------------------------------------------------------------ writing and saving

    [Fact]
    public void Write_ReadsBackAsTheSameValuesWithNoProblems()
    {
        var tuning = new Tuning(7.1234567f, 99.99999f, 0.1f, 1.9999999f, 4.0000005f, 123.45678f, -1.2E-05f, 2.9999998f, 0.33333334f);

        TuningRead read = Tuning.Parse(tuning.Write());

        Assert.Equal(tuning, read.Tuning);
        Assert.Empty(read.Problems);
    }

    [Fact]
    public void Write_RangeEndsReadBackWithNoProblems()
    {
        foreach (Tuning.Setting setting in Tuning.Settings)
        {
            foreach (double end in new[] { setting.Min, setting.Max })
            {
                Tuning tuning = setting.Set(Tuning.BuiltIn, (float)end);

                TuningRead read = Tuning.Parse(tuning.Write());

                Assert.Equal(tuning, read.Tuning);
                Assert.Empty(read.Problems);
            }
        }
    }

    [Fact]
    public void Write_ManyValuesReadBackExactly()
    {
        var random = new Random(21);
        for (int i = 0; i < 2000; i++)
        {
            var tuning = new Tuning(
                3 + (47 * random.NextSingle()), 4 + (96 * random.NextSingle()), 30 * random.NextSingle(),
                2 * random.NextSingle(), 4 + (496 * random.NextSingle()), 8 + (492 * random.NextSingle()),
                (40 * random.NextSingle()) - 20, (6 * random.NextSingle()) - 3, 0.1f + (4.9f * random.NextSingle()));

            TuningRead read = Tuning.Parse(tuning.Write());

            Assert.Empty(read.Problems);
            Assert.Equal(tuning, read.Tuning);
        }
    }

    [Fact]
    public void Write_OfTheBuiltInsIsTheExampleFile()
    {
        Assert.Equal(File.ReadAllText(FindExample()).ReplaceLineEndings("\n"), Tuning.BuiltIn.Write());
    }

    [Fact]
    public void Save_ReplacesTheFileWithTheFullText()
    {
        Write("""{ "flight_speed": 8, "not_a_setting": 1 }""");
        Tuning tuning = Tuning.BuiltIn with { SeatHeight = 0.5f };

        Assert.Null(tuning.Save(_folder));

        Assert.Equal(tuning.Write(), File.ReadAllText(Path.Combine(_folder, Tuning.FileName)));
        Assert.Equal(tuning, Tuning.Load(_folder).Tuning);
        Assert.Equal(new[] { Tuning.FileName }, Directory.GetFiles(_folder).Select(Path.GetFileName));
    }

    [Fact]
    public void Save_WhenTheWriteFails_KeepsTheOldFileAndGivesTheReason()
    {
        Write("""{ "flight_speed": 8 }""");
        Directory.CreateDirectory(Path.Combine(_folder, Tuning.FileName + ".tmp"));

        string? reason = Tuning.BuiltIn.Save(_folder);

        Assert.False(string.IsNullOrEmpty(reason));
        Assert.DoesNotContain('\n', reason);
        Assert.False(reason.EndsWith('.'));
        Assert.Equal("""{ "flight_speed": 8 }""", File.ReadAllText(Path.Combine(_folder, Tuning.FileName)));
    }

    [Fact]
    public void Save_WhenTheMoveFails_GivesTheReasonAndLeavesNoTemporaryFile()
    {
        Directory.CreateDirectory(Path.Combine(_folder, Tuning.FileName));

        string? reason = Tuning.BuiltIn.Save(_folder);

        Assert.False(string.IsNullOrEmpty(reason));
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Save_WhenTheFolderIsGone_GivesTheReason()
    {
        Assert.False(string.IsNullOrEmpty(Tuning.BuiltIn.Save(Path.Combine(_folder, "gone"))));
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
        Assert.Equal(9, json.RootElement.EnumerateObject().Select(field => field.Name).Distinct().Count());
    }

    /// <summary><c>tools/tuning.example.json</c>.</summary>
    private static string FindExample() => Path.Combine(Repository.Root(), "tools", "tuning.example.json");
}
