using Xunit;

namespace PanzaDragoonVR.Logic.Tests;

/// <summary>
/// The one-line rule (<see cref="OneLine"/>) the log lines and the board use for a message from .NET or one
/// quoting a file.
/// </summary>
public sealed class OneLineTests
{
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
