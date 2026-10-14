#nullable enable
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The launch report as a sign standing in the world in front of the starting view: a dark board
/// with the report's lines on it. It is placed in the scene, not on the camera, so it does not follow
/// the head; the spec allows only the radar as a HUD element. Hidden when there is nothing to say,
/// and otherwise shown for as long as the app runs (dismissing it comes with controller input).
/// </summary>
public partial class LaunchReportPanel : Node3D
{
    private static readonly Color NoticeColour = new(0.85f, 0.92f, 1.0f);
    private static readonly Color ProblemColour = new(1.0f, 0.85f, 0.55f);

    /// <summary>Metres of board above and below the text.</summary>
    private const float Margin = 0.08f;

    public override void _Ready() => Visible = false;

    /// <summary>Shows <paramref name="report"/>, or hides the panel when it has nothing to say.</summary>
    public void Display(LaunchReport report)
    {
        if (report.Kind == LaunchReportKind.None)
        {
            Visible = false;
            return;
        }

        var text = GetNode<Label3D>("Text");
        text.Text = string.Join("\n", report.ShownLines);
        text.Modulate = report.Kind == LaunchReportKind.Notice ? NoticeColour : ProblemColour;

        // The board grows with the lines on it: a short notice gets a short board.
        var board = GetNode<MeshInstance3D>("Board");
        var quad = (QuadMesh)board.Mesh;
        Font font = text.Font ?? ThemeDB.FallbackFont;
        // Measured with the label's own breaking: autowrap_mode 3 (smart) also breaks a word wider than
        // the label, such as a long path, so the measure must too or the text runs past the board.
        TextServer.LineBreakFlag breaks = TextServer.LineBreakFlag.Mandatory | TextServer.LineBreakFlag.WordBound;
        if (text.AutowrapMode == TextServer.AutowrapMode.WordSmart)
        {
            breaks |= TextServer.LineBreakFlag.Adaptive;
        }

        Vector2 pixels = font.GetMultilineStringSize(text.Text, HorizontalAlignment.Left, text.Width, text.FontSize, -1, breaks);
        quad.Size = new Vector2(quad.Size.X, pixels.Y * text.PixelSize + Margin * 2);
        Visible = true;
    }
}
