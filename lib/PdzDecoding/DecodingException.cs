using System;

namespace PdzDecoding;

/// <summary>
/// A disc image or disc file that cannot be read as the library expects. The message is one line,
/// with no full stop, ready to follow <c>&lt;tool&gt;: error: </c> or a <c>PDZ-ASSETS</c> tag.
/// </summary>
public sealed class DecodingException : Exception
{
    public DecodingException(string message) : base(message)
    {
    }

    public DecodingException(string message, Exception inner) : base(message, inner)
    {
    }

    /// <summary>
    /// A message from .NET made fit to quote in one of these lines: one line, with any closing full
    /// stop removed, as in "Access to the path '...' is denied".
    /// </summary>
    public static string OneLine(string message)
    {
        string line = string.Join(" ", message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.TrimEnd('.').TrimEnd();
    }
}
