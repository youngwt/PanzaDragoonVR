#nullable enable
using System;
using Godot;
using PanzaDragoonVR.Logic;

namespace PanzaDragoonVR;

/// <summary>
/// The scene's extraction job: starts extraction on a disc image in the asset folder, puts its progress
/// on the board and dresses the scene again when it ends.
/// </summary>
public partial class Main
{
    private DiscExtraction? _extraction;

    /// <summary>
    /// While extraction runs, logs its steps and keeps the board up to date; when it ends, reads the
    /// asset folder again and dresses the scene in what it now holds, with the launch report on the
    /// board, led by the extraction problem when it failed.
    /// </summary>
    private void WatchExtraction()
    {
        if (_extraction == null)
        {
            return;
        }

        try
        {
            if (!_extraction.Update())
            {
                return;
            }

            if (!_extraction.IsFinished)
            {
                ShowProgress(_extraction.Board);
                return;
            }

            string? problem = _extraction.Problem;
            _extraction = null;
            (ImageTexture? floor, ImageTexture? canopy, Node3D? flyer, Node3D? dragon, LaunchReport report) = LoadAssets();
            if (problem != null)
            {
                report = report.WithExtractionProblem(problem);
            }

            AssetFolder.LogReport(report);
            ShowAssets(floor, canopy, flyer, dragon, report);
        }
        catch (Exception e)
        {
            // The scene carries on with what it shows; the extraction is not watched any further.
            _extraction = null;
            string reason = $"following it failed ({DiscExtraction.Reason(e)})";
            GD.PushWarning($"{AssetFolder.LogTag} extraction {reason}; carrying on with what the scene shows");
            OnBoard("the launch report", () =>
            {
                LaunchReport report = _report.WithExtractionProblem(ExtractionLines.Failed(null, reason));
                AssetFolder.LogReport(report);
                Board.Display(report);
            });
        }
    }

    /// <summary>
    /// Starts extraction on the asset folder when its <c>disc/</c> holds one disc image; null when there is
    /// nothing to run. A problem found before it can start, such as more than one disc image, leads <paramref name="report"/>.
    /// </summary>
    private DiscExtraction? StartExtraction(ref LaunchReport report)
    {
        DiscExtraction? extraction = DiscExtraction.Start(_folder, out string? problem);
        if (problem != null)
        {
            report = report.WithExtractionProblem(problem);
        }

        return extraction;
    }

    /// <summary>Puts extraction's progress on the board.</summary>
    private void ShowProgress(System.Collections.Generic.IReadOnlyList<string> lines) =>
        OnBoard("extraction's progress", () => Board.DisplayProgress(lines));
}
