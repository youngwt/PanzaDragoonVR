#nullable enable
namespace PanzaDragoonVR.Logic;

/// <summary>The asset folder chosen on Android, and the one log line that says how.</summary>
/// <param name="Path">The folder to use.</param>
/// <param name="Line">The log line, without the <c>PDZ-ASSETS</c> tag.</param>
public readonly record struct AndroidFolder(string Path, string Line);

/// <summary>
/// Chooses between the folder Android gives and the fixed path: Android's answer when it gave one,
/// else the fixed path with the reason. Engine-free, so it is tested on the PC.
/// </summary>
public static class AndroidFolderChoice
{
    /// <summary>
    /// <paramref name="asked"/> is Android's answer, null or empty when it gave none;
    /// <paramref name="failure"/> is why Android could not be asked at all, or null.
    /// </summary>
    public static AndroidFolder Choose(string? asked, string? failure, string fixedPath)
    {
        if (failure != null)
        {
            return new AndroidFolder(fixedPath, $"Android could not be asked for the external files folder ({failure}); using {fixedPath}");
        }

        return string.IsNullOrEmpty(asked)
            ? new AndroidFolder(fixedPath, $"Android gave no external files folder; using {fixedPath}")
            : new AndroidFolder(asked, $"asset folder from Android: {asked}");
    }
}
