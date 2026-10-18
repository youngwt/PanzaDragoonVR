#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using PanzaDragoonVR.Logic;
using PdzDecoding;

namespace PanzaDragoonVR;

/// <summary>
/// Extraction at launch, behind the scene: when the asset folder's <c>disc/</c> holds one disc image, a
/// <c>.cue</c> sheet or an <c>.iso</c>, the decoding library extracts from it on a background thread,
/// through its staging folder, while the scene shows what the folder already holds. The background
/// thread runs the engine-free library alone; progress comes back through a queue that
/// <see cref="Update"/> drains on the main thread, where every log line and board line is made. Every
/// outcome is one <c>PDZ-ASSETS</c> line; nothing here stops the game, which carries on with what the folder holds.
/// </summary>
public sealed class DiscExtraction
{
    private readonly ConcurrentQueue<ExtractionProgress> _progress = new();
    private readonly string _image;
    private readonly Stopwatch _clock;
    private readonly Task<ExtractionResult> _task;
    private ExtractionProgress? _last;

    private DiscExtraction(string imagePath, string assetFolder)
    {
        _image = System.IO.Path.GetFileName(imagePath);
        _clock = Stopwatch.StartNew();
        // Engine-free work only: the library, and a queue the main thread reads.
        _task = Task.Run(() => Extraction.Run(imagePath, assetFolder, _progress.Enqueue));
    }

    /// <summary>The board's lines while extraction runs, starting with the disc image's name.</summary>
    public IReadOnlyList<string> Board => _last is ExtractionProgress p
        ? ExtractionLines.Progress(_image, Step(p.Stage), p.DiscFile, p.Number, p.Count)
        : ExtractionLines.Starting(_image);

    /// <summary>Whether the background work has ended; <see cref="Problem"/> is then set.</summary>
    public bool IsFinished { get; private set; }

    /// <summary>
    /// Once finished, the line for the launch report when extraction failed (see <see cref="ExtractionLines"/>),
    /// or null when it succeeded or had nothing to write.
    /// </summary>
    public string? Problem { get; private set; }

    /// <summary>
    /// Looks in the folder's <c>disc/</c> and starts extraction when it holds one disc image. Returns
    /// null when there is nothing to run: no folder, no disc image, which is logged as a notice, or more
    /// than one, whose problem line is put in <paramref name="problem"/> for the launch report.
    /// </summary>
    public static DiscExtraction? Start(string? assetFolder, out string? problem)
    {
        problem = null;
        if (assetFolder == null)
        {
            return null;
        }

        try
        {
            DiscSearch search = DiscFolder.Find(assetFolder);
            switch (search.State)
            {
                case DiscFolderState.None:
                    GD.Print($"{AssetFolder.LogTag} no disc image in {search.DiscFolder}: nothing to extract");
                    return null;
                case DiscFolderState.Several:
                    Warn($"more than one disc image in {search.DiscFolder} ({string.Join(", ", search.Names)}): nothing extracted");
                    problem = ExtractionLines.SeveralImages(search.DiscFolder, search.Names);
                    return null;
            }

            GD.Print($"{AssetFolder.LogTag} extracting from {search.ImagePath} in the background into {assetFolder}");
            return new DiscExtraction(search.ImagePath!, assetFolder);
        }
        catch (Exception e)
        {
            string reason = $"{e.GetType().Name}: {DecodingException.OneLine(e.Message)}";
            Warn($"extraction could not be started ({reason}); carrying on with what the folder holds");
            problem = ExtractionLines.Failed(null, reason);
            return null;
        }
    }

    /// <summary>
    /// On the main thread, once a frame: logs each step the background thread has reported since the last
    /// call, and, when the work has ended, logs its outcome and sets <see cref="IsFinished"/> and
    /// <see cref="Problem"/>. Returns whether anything changed, so the board needs showing again.
    /// </summary>
    public bool Update()
    {
        if (IsFinished)
        {
            return false;
        }

        // Read before draining, so every step reported before the end is drained with it.
        bool ended = _task.IsCompleted;
        bool changed = false;
        while (_progress.TryDequeue(out ExtractionProgress p))
        {
            _last = p;
            changed = true;
            GD.Print($"{AssetFolder.LogTag} {ExtractionLines.LogLine(Step(p.Stage), p.DiscFile, p.Number, p.Count)}");
        }

        if (ended)
        {
            Finish();
            changed = true;
        }

        return changed;
    }

    private void Finish()
    {
        IsFinished = true;
        string seconds = _clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        Exception? error = _task.Exception?.InnerException ?? (_task.IsCanceled ? new OperationCanceledException() : null);
        switch (error)
        {
            case null:
                Succeeded(_task.Result, seconds);
                return;
            case DecodingException { DiscFile: null } e:
                Warn($"the disc image cannot be read: {e.Message}; nothing extracted, the previous contents are kept");
                Problem = ExtractionLines.Unreadable(e.Message);
                return;
            case DecodingException e:
                Warn($"extraction failed on {e.DiscFile}: {e.Message}; nothing moved into place, the previous contents are kept");
                Problem = ExtractionLines.Failed(e.DiscFile, e.Message);
                return;
            default:
                // Something the checks did not foresee, such as a full disk. It names no disc file: the error is not one file's.
                string reason = $"{error.GetType().Name}: {DecodingException.OneLine(error.Message)}";
                if (_last?.Stage == ExtractionStage.MovingIntoPlace)
                {
                    // Some new files may already have been moved; the manifest goes last, so the previous one stays.
                    Warn($"extraction failed while moving files into place ({reason}); some new files may already be in place, the previous manifest is kept");
                    Problem = ExtractionLines.FailedMoving(reason);
                    return;
                }

                Warn($"extraction failed ({reason}); nothing moved into place, the previous contents are kept");
                Problem = ExtractionLines.Failed(null, reason);
                return;
        }
    }

    private static void Succeeded(ExtractionResult result, string seconds)
    {
        if (result.Files.Count == 0)
        {
            GD.Print($"{AssetFolder.LogTag} found no textures to write on " +
                $"{result.Source.Product} {result.Source.Version}: nothing written ({result.Details()})");
            return;
        }

        GD.Print($"{AssetFolder.LogTag} extracted {result.Files.Count} textures from {result.DiscFiles.Count(f => f.Files.Count > 0)} disc files " +
            $"of {result.Source.Product} {result.Source.Version} into {result.Folder}, listed in {result.ManifestPath}, " +
            $"in {seconds} s ({result.Details()})");
    }

    private static ExtractionStep Step(ExtractionStage stage) => stage switch
    {
        ExtractionStage.Reading => ExtractionStep.Reading,
        ExtractionStage.Writing => ExtractionStep.Writing,
        _ => ExtractionStep.MovingIntoPlace,
    };

    private static void Warn(string line) => GD.PushWarning($"{AssetFolder.LogTag} {line}");
}
