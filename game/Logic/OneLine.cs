#nullable enable
using System.Text.RegularExpressions;

namespace PanzaDragoonVR.Logic;

/// <summary>
/// The one rule in <c>game/Logic</c> for putting a message from .NET, or one quoting a file, on a log line
/// or the board: each run of whitespace, line breaks among it, becomes one space, the ends are trimmed
/// and a closing full stop is dropped.
/// </summary>
internal static class OneLine
{
    /// <summary><paramref name="message"/> on one line with no closing full stop.</summary>
    public static string Of(string message) =>
        Regex.Replace(message, @"\s+", " ").Trim().TrimEnd('.');
}
