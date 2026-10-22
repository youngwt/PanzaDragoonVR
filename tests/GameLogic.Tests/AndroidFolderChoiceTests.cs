using PanzaDragoonVR.Logic;
using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

public class AndroidFolderChoiceTests
{
    private const string Fixed = "/storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files";

    [Fact]
    public void AndroidAnswer_IsUsedAndNamed()
    {
        AndroidFolder chosen = AndroidFolderChoice.Choose("/storage/emulated/0/Android/data/x/files", null, Fixed);

        Assert.Equal("/storage/emulated/0/Android/data/x/files", chosen.Path);
        Assert.Equal("asset folder from Android: /storage/emulated/0/Android/data/x/files", chosen.Line);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoAnswer_FallsBackToTheFixedPath(string? asked)
    {
        AndroidFolder chosen = AndroidFolderChoice.Choose(asked, null, Fixed);

        Assert.Equal(Fixed, chosen.Path);
        Assert.Equal($"Android gave no external files folder; using {Fixed}", chosen.Line);
    }

    [Fact]
    public void Failure_FallsBackWithTheReason()
    {
        AndroidFolder chosen = AndroidFolderChoice.Choose(null, "getExternalFilesDir threw java.lang.SecurityException", Fixed);

        Assert.Equal(Fixed, chosen.Path);
        Assert.Equal($"Android could not be asked for the external files folder (getExternalFilesDir threw java.lang.SecurityException); using {Fixed}", chosen.Line);
    }
}
