#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
/// Before it starts, the asset contract's mode is applied (see <see cref="ExtractionModes"/>): development
/// mode extracts and keeps the disc image; release mode skips extraction when the folder was already
/// extracted from the same disc, and deletes the disc image after a successful extraction or such a skip.
/// </summary>
public sealed class DiscExtraction
{
    private readonly ConcurrentQueue<ExtractionProgress> _progress = new();
    private readonly string _image;
    private readonly string _imagePath;
    private readonly ExtractionDecision _decision;
    private readonly Stopwatch _clock;
    private readonly Task<ExtractionResult> _task;
    private ExtractionProgress? _last;

    private DiscExtraction(string imagePath, string assetFolder, ExtractionDecision decision)
    {
        _image = System.IO.Path.GetFileName(imagePath);
        _imagePath = imagePath;
        _decision = decision;
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
    /// Looks in the folder's <c>disc/</c> and, when it holds one disc image, decides by the folder's mode
    /// whether to extract, logging the decision, and starts extraction. Returns null when there is nothing
    /// to run: no folder, no disc image, which is logged as a notice, more than one, whose problem line is
    /// put in <paramref name="problem"/> for the launch report, or a release-mode skip, after which the
    /// disc image is deleted.
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
            ExtractionMode mode = ExtractionModes.ModeOf(assetFolder);
            switch (search.State)
            {
                case DiscFolderState.None:
                    GD.Print($"{AssetFolder.LogTag} {ExtractionModes.NoImageLine(mode, search.DiscFolder)}");
                    return null;
                case DiscFolderState.Several:
                    Warn($"more than one disc image in {search.DiscFolder} ({string.Join(", ", search.Names)}): nothing extracted");
                    problem = ExtractionLines.SeveralImages(search.DiscFolder, search.Names);
                    return null;
            }

            string image = search.ImagePath!;
            // The header is needed only for release mode's skip test; one that cannot be read is never a
            // skip, and extraction then reports why.
            DiscSource? disc = mode == ExtractionMode.Release ? ReadSource(image) : null;
            ExtractionDecision decision = ExtractionModes.Decide(mode, ExtractionModes.LoadOrigin(assetFolder), disc);
            GD.Print($"{AssetFolder.LogTag} {ExtractionModes.DecisionLine(decision, image, assetFolder, disc)}");
            if (!decision.Extract)
            {
                DeleteImage(image);
                return null;
            }

            return new DiscExtraction(image, assetFolder, decision);
        }
        catch (Exception e)
        {
            string reason = Reason(e);
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
        Report(error, seconds);
        if (ExtractionModes.DeleteAfterExtraction(_decision, succeeded: error == null))
        {
            DeleteImage(_imagePath);
        }
    }

    private void Report(Exception? error, string seconds)
    {
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
                string reason = Reason(error);
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

        GD.Print($"{AssetFolder.LogTag} extracted {result.Files.Count} textures from {result.DiscFilesWritten} disc files " +
            $"of {result.Source.Product} {result.Source.Version} into {result.Folder}, listed in {result.ManifestPath}, " +
            $"in {seconds} s ({result.Details()})");
    }

    /// <summary>The disc image's product and version from its Saturn header, or null when it cannot be read.</summary>
    private static DiscSource? ReadSource(string imagePath)
    {
        try
        {
            SaturnHeader header = DiscImage.ReadHeader(imagePath);
            return new DiscSource(header.Product, header.Version);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes the disc image's own files, as the library lists them (the <c>.cue</c> and the <c>.bin</c>
    /// files it names, or the <c>.iso</c>), through <see cref="ExtractionModes.DeleteImage"/>; <c>disc/</c>
    /// and anything else in it stay. Each outcome is logged, a failure as one warning line, and the game carries on.
    /// </summary>
    private static void DeleteImage(string imagePath)
    {
        IReadOnlyList<string> files;
        try
        {
            files = DiscFolder.ImageFiles(imagePath);
        }
        catch (Exception e)
        {
            Warn(ExtractionModes.NotDeletedLine(imagePath, Reason(e)));
            return;
        }

        DeletionOutcome outcome = ExtractionModes.DeleteImage(imagePath, files);
        if (outcome.Line != null)
        {
            GD.Print($"{AssetFolder.LogTag} {outcome.Line}");
        }

        if (outcome.Warning != null)
        {
            Warn(outcome.Warning);
        }
    }

    /// <summary>An exception as a log or board line quotes it: its type and its message on one line, with no closing full stop.</summary>
    public static string Reason(Exception e) => $"{e.GetType().Name}: {DecodingException.OneLine(e.Message)}";

    private static ExtractionStep Step(ExtractionStage stage) => stage switch
    {
        ExtractionStage.Reading => ExtractionStep.Reading,
        ExtractionStage.Writing => ExtractionStep.Writing,
        _ => ExtractionStep.MovingIntoPlace,
    };

    private static void Warn(string line) => GD.PushWarning($"{AssetFolder.LogTag} {line}");
}
