---
title: 'Extraction runs behind a progress board and falls back on failure'
type: 'feature'
ticket: '9'
created: '2026-10-18'
status: 'built'
baseline_revision: 'a9587f3aa5e01dfb9ea7ca474e8cb0673a9532b5'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game extracts on the main thread before the scene starts, so the headset shows nothing until extraction ends, and a failure part way can leave a half-written folder. The asset contract says extraction runs while the scene is shown on what the folder already holds, with a progress board, writes into a staging folder that replaces the previous extraction only on success, and reports a failure on the launch report board while the game carries on.

**Approach:** Start the scene and OpenXR first, on what the folder holds. Run extraction on a background thread, with a board in the world showing progress by disc file. The library writes into `extracting/` and moves the new files into place, the manifest last, only when everything is written. When extraction finishes the game reads the folder again; on failure the launch report board says why and the previous contents stay.

## Boundaries & Constraints

**Always:**
- Staging exactly as the contract's "What extraction writes": `extracting/` inside the asset folder, emptied first; files moved into place only after every file is written, `manifest.json` last; the staging folder removed afterwards; a leftover one emptied by the next run. The PC command uses the same library path.
- Godot objects are touched only on the main thread; the background work is the engine-free library alone.
- The scene, OpenXR and the frame-rate readout start without waiting for extraction.
- Failure wording follows the contract's launch rows: more than one disc image; the disc image cannot be read, with the reason; extraction failed, with the disc file and the reason, the previous contents kept.
- Outcomes are logged with `PDZ-ASSETS`, one line each.

**Never:**
- No development or release modes, no deleting the disc image, no skip rule (story 10).
- No change to what is extracted, to texture numbering or to the asset contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Disc present | A good disc image in `disc/` | Scene up at once; progress board by disc file; textures and manifest in place; folder read again; the board then shows the launch report | No error expected |
| No disc | `disc/` empty | Scene and launch report as today; no progress board | Notice line |
| Damaged image | A truncated disc image | Board reports "the disc image cannot be read" or "extraction failed" with the reason; previous files and manifest untouched | Shown and logged |
| Failure part way | An error after some files are written to staging | Nothing moved into place; previous contents kept | Shown and logged |
| Leftover staging | `extracting/` left by an interrupted run | Emptied, then used | Not an error |
| Previous extraction | An earlier extraction in the folder | Replaced only when the new one has fully written | — |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Extraction.cs` -- `Run(imagePath, outputFolder)` decodes all files, then creates folders, writes PNGs to `images/e03/<file>/`, then `manifest.json`. Add the staging, and a progress callback per disc file (engine-free, e.g. `IProgress<...>` or an `Action`).
- `game/DiscExtraction.cs` -- `Run(folder)` calls the library and logs one line; never throws.
- `game/Main.cs` -- `_Ready`: `LoadAssets()` (which calls `AssetFolder.Locate`, `DiscExtraction.Run`, `AssetFolder.Open(folder)`, image and model loads, the report), then `LaunchReportPanel.Display`, `Dress` ground and sky, `FlyingCube.Carry`, then `StartOpenXr()`. Split so the scene starts first and a reload after extraction re-dresses, re-carries and re-displays.
- `game/LaunchReportPanel.cs` -- `Display(LaunchReport)` sizes a board to its text; reuse it, or a sibling, for progress text.
- `game/Logic/LaunchReport.cs` -- `Build`, `Failed`; the place for engine-free progress and failure lines, tested on the PC.
- `tools/pdz-extract/Program.cs` -- unchanged except where the library's signature changes.
- Device checks: `tools/build-quest.sh`; a damaged image made on the headset by moving `files/disc/` aside and writing a truncated copy of the disc image there with `adb shell head -c`, then restoring it; captures with `adb exec-out screencap -p` and the proximity sensor held. Extraction of the full disc took 0.3 s on the headset, so the progress board may only flash.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Extraction.cs` -- staging into `extracting/` and the move into place; a per-file progress callback.
- [x] `tests/Decoding.Tests/` -- one or more tests per matrix row the library covers: staging, failure part way, leftover staging, replacement.
- [x] `game/Logic/` and `tests/GameLogic.Tests/` -- engine-free progress and failure lines, with tests.
- [x] `game/Main.cs`, `game/DiscExtraction.cs`, `game/LaunchReportPanel.cs` -- scene first, extraction on a background thread, progress board, reload on finish, failure on the board.
- [x] `tools/README.md` -- the progress board, staging and failure behaviour.

**Acceptance Criteria:**
- Given the author's disc image in `disc/` on the headset, when the app starts, then the scene is up before extraction finishes, progress shows on a board (seen in the log by disc file, and on screen if it lasts long enough to capture), and the textures and manifest end up in place.
- Given a truncated disc image on the headset, when the app starts, then the board reports the failure with its reason, the scene runs, and the previous files and manifest are unchanged.
- Given `tools/test.sh`, when it runs, then every test passes, and the PC command still writes the same 420 files and manifest, byte for byte.

## Implementation Notes

- Library: `Extraction.Run(imagePath, outputFolder, Action<ExtractionProgress>? progress = null)`; the PC command is unchanged. Progress is reported as each step starts: `Reading` per disc file (9), `Writing` per disc file with PNGs, then `MovingIntoPlace`. Staging (`Extraction.StagingFolder` = `extracting`) is emptied after decoding, before the first write; on any failure it is removed best-effort and rethrown; after the move it is removed best-effort (a leftover is emptied by the next run).
- `DecodingException.DiscFile` (init-only, message unchanged) names the disc file when the error is one file's; null for the image as a whole. The game uses it to choose "cannot be read" vs "extraction failed on <file>"; any other exception names no disc file. When the last progress step was `MovingIntoPlace`, the board says "Extraction failed while moving files into place: <reason>. Some new files may already be in place; the previous manifest is kept." (the manifest moves last), tested by blocking a PNG's destination with a folder.
- Game: `DiscExtraction.Start` looks in `disc/` on the main thread, then `Task.Run`s the library alone; `_Ready` does not wait for it before dressing the scene and starting OpenXR. Progress comes back through a `ConcurrentQueue` that `Main._Process` drains, logging each step and updating the board. On finish the folder is read again and `ShowAssets` re-dresses, re-carries (`FlyingCube.Carry(null)` restores the cube) and re-displays, the report led by `LaunchReport.WithExtractionProblem` on failure. If following the extraction throws, the last report is shown with the failure added. The board's words are in `game/Logic/ExtractionLines.cs`.

### Evidence

- `tools/test.sh`: all pass (Python 97, GameLogic 209, Decoding 107 after the review fixes).
- PC: `pdz-extract` on `rom/JP/...cue` into `extracted/pdz-extract-9` writes 420 PNGs and `manifest.json`, `diff -r` identical to the earlier `extracted/pdz-extract-7`; a second run into the same folder is identical too; no `extracting/` left.
- Desktop Godot, headless, `PDZ_ASSETS` set: with the JP disc, the first `PDZ-FPS` line comes before the progress lines and the output is byte-identical to the PC's; a track 1 cut to 30 MB, with an earlier extraction and a leftover `extracting/`, gives "Extraction failed on E03CMN: ... the image ends inside sector 50310. The previous contents are kept." with the manifest and images unchanged; a second `.iso` gives the more-than-one-image line; the good disc again empties and removes the leftover `extracting/`.
- Quest 3 (`tools/build-quest.sh`): OpenXR at 90 Hz at 43.34 s, before extraction ended at 43.69 s (log: 0.2 s); the 19 progress lines (9 reading, 9 writing, 1 moving) in logcat; the pulled `images/` and `manifest.json` match the PC output; no `extracting/` left; the APK holds `PdzDecoding.dll` and no disc data.
- Still to do: the damaged-image check on the headset. The truncated copy was staged, but the relaunch was blocked by a Guardian dialog on top (needs a person in the headset); `disc/` was restored to the original. No screen capture of the progress board was taken.

- **Damaged-image check on the headset, 2026-10-18, with the author.** `files/disc/` was moved to `files/disc-good/`, and a copy with track 1 cut to 100 MB (`head -c` on the headset) and the other tracks put in `disc/`. The author launched the app. Logcat: `extracting: reading E03CMN (1 of 9)`, then `extraction failed on E03CMN: ... (Track 1).bin: the image ends inside sector 50310; nothing moved into place, the previous contents are kept`, then the folder read again. A capture (`extracted/headset-9-caps/cap-21.png`, git-ignored) shows the board: "Nothing was extracted from the disc image / Extraction failed on E03CMN: ... the image ends inside sector 50310. The previous contents are kept. / The scene shows what the asset folder already holds", with the scene and the readout at 90 Hz. `manifest.json`'s md5 (b247830991a3e79f549b68212d795ef2) was the same before and after. The real disc image was put back in `disc/` and the proximity sensor restored.
- **Seen by the author:** the board stands behind them when the app starts facing another way, because it is placed in the world at a fixed spot ahead of the play area's origin, not ahead of where the head faces. Recorded in `deferred-work.md`.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-18: high 0, medium 0, low 4, false 0, maybe-false 0. All four patched by the implementer, one carried.

- low, patched: the README said extraction starts after the scene and OpenXR; it starts first on a background thread and the main thread does not wait. Reworded.
- low, patched: a failure part way through moving files into place was reported as "nothing moved into place". Now its own wording ("some new files may already be in place; the previous manifest is kept"), with a test that blocks a move part way.
- low, patched: errors not belonging to a disc file (emptying staging, writing the manifest) named the last progress step's file. A disc file is now named only when the exception carries one.
- low, patched: an exception while following the extraction left the board on its progress text. The catch now puts a failure report on the board.
- carried: acceptance criterion 2 (damaged image on the headset) was blocked by a Guardian dialog at review. Met on 2026-10-18 with the author: see the damaged-image check in Implementation Notes.

Re-verified after the patches: `tools/test.sh` (97 Python, 209 GameLogic, 107 Decoding) passes; `pdz-extract` on the Japanese disc gives output identical to story 7's, with no `extracting/` left.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `pdz-extract` on `rom/JP/...cue` -- expected: the same files and manifest as before, no `extracting/` left behind.
- `tools/build-quest.sh`, logcat while running -- expected: as the criteria say.
