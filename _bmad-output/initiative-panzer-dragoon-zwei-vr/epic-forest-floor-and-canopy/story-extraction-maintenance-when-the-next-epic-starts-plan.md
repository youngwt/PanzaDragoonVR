---
title: 'Extraction code duplicates are pinned and Main.cs is split'
type: 'refactor'
ticket: '7'
created: '2026-10-22'
status: done
baseline_revision: '93f940789bcac0079b1ef0d6038897f7b3403303'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extraction epic's retrospective (A-dup, A-size; action A4) left three loose ends that this epic's stories would trip over: the game's `ExtractorPrefix` ("pdz-extract ") and the library's manifest generator are two strings nothing ties together; `game/Logic` makes a .NET message one line in two different ways; and `game/Main.cs` (444 lines) holds five jobs, which stories 3.1, 3.3 and 3.5 are all about to edit.

**Approach:** A test that fails if the prefix and the generator drift; one one-line helper in `game/Logic` used by both places there; and `Main` split by job into partial-class files beside `Main.cs`, moving code without changing it, so the scene, log and board behave as before.

## Boundaries & Constraints

**Always:**
- Code moves verbatim; only `using` lines, the class summary and file-level comments change in the split.
- Every existing test passes with its assertions unchanged.
- `game/Logic` stays free of Godot and of the decoding library; only the test project gains the library reference.
- The game builds with 0 warnings; the APK still holds `PdzDecoding.dll` and no disc data.

**Never:**
- No new behaviour, no change to the scene file, node paths, log lines or board text (except the whitespace note in Design Notes).
- No change to `DecodingException.OneLine` in the library, nor to `DiscExtraction.Reason`.
- No scrolling, tuning or plane work: that is stories 3.1, 3.3 and 3.5.

</frozen-after-approval>

## Code Map

- `game/Main.cs` (444 lines) -- `public partial class Main : Node3D`. Jobs: scene and assets (`_Ready` start, `ShowAssets`, `LoadAssets`, `FailedReport`, `Dress`, `TilingShader`, `GroundTileMetres`/`SkyTileMetres`); extraction watch (`_Process` lines 95-136, `StartExtraction`, `ShowProgress`); board (`Board`, `OnBoard`, `PlaceBoard`, `OnPoseRecentered`, `_boardPlaced`, `_report`); OpenXR session (`StartOpenXr`, OpenXR subscribe block in `_Ready` 61-80, `_ExitTree`, `SessionHasBegun`, `SessionIsShown`, `PauseMeasuring`, `ResumeMeasuring`); refresh rate (`RequestRefreshRateAgain`, `RequestRefreshRate`, `OnRefreshRateChanged`, `ShowRateInUse`, `_choice`, `_lastWarning`, `_askedAgain`).
- `game/Main.tscn` -- attaches `Main.cs`; the class with the script's name must stay in `Main.cs`. Do not edit.
- `game/Logic/AssetManifest.cs:178` -- private `OneLine`: `Regex.Replace(message, @"\s+", " ").Trim().TrimEnd('.')`.
- `game/Logic/ExtractionMode.cs:161` -- inline `e.Message.Replace('\n',' ').Replace('\r',' ').Trim().TrimEnd('.')` in the deletion lines; `ExtractorPrefix` at line 56.
- `lib/PdzDecoding/Manifest.cs:30` -- `public const string Generator = "pdz-extract " + Extraction.Version`.
- `tests/GameLogic.Tests/GameLogic.Tests.csproj` -- compiles `game/Logic/**` directly; no project references yet. `tests/Decoding.Tests/Decoding.Tests.csproj:17` shows the library reference to copy.
- `tests/GameLogic.Tests/ExtractionModesTests.cs` -- home for the prefix test; `ExtractionLinesTests.cs` has the existing one-line checks to mirror.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/OneLine.cs` (or a fitting name) -- one internal static helper with AssetManifest's rule (whitespace runs to one space, trimmed, closing full stop dropped), documented -- the single rule for `game/Logic`.
- [x] `game/Logic/AssetManifest.cs`, `game/Logic/ExtractionMode.cs` -- call the helper; remove the private copy and the inline chain.
- [x] `tests/GameLogic.Tests/GameLogic.Tests.csproj` -- reference `lib/PdzDecoding` -- so a test can read `Manifest.Generator`.
- [x] `tests/GameLogic.Tests/ExtractionModesTests.cs` -- a test that `Manifest.Generator` starts with `ExtractionModes.ExtractorPrefix` and has a version after it; tests for the helper (line breaks, `\r\n`, closing full stop, runs of spaces).
- [x] `game/Main.cs` -> `Main.cs` (scene and assets, `_Ready`, `_Process` dispatch), `Main.Extraction.cs`, `Main.Board.cs`, `Main.OpenXr.cs`, `Main.RefreshRate.cs` -- partial-class files, each with a summary naming its job; fields move to the file whose job owns them.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every test passes, the new tests among them, and no existing assertion changed.
- Given the generator changed to start with another name, when the tests run, then the prefix test fails.
- Given the game project, when it is built, then it has 0 warnings and `Main.cs` and each `Main.*.cs` holds one job and is under 200 lines.
- Given the headset, when `tools/build-quest.sh` launches the app in development mode, then the log shows the same lines as before: the asset lines, "board placed in front of the head", the refresh-rate line, and extraction's lines when a disc is there.

## Implementation Notes

- The helper is `OneLine.Of(string)` in `game/Logic/OneLine.cs`, an `internal static class`; `AssetManifest.cs` lost its private copy and its now-unused `using System.Text.RegularExpressions`.
- `ConnectOpenXr` takes the interface as a parameter, `ConnectOpenXr(OpenXRInterface openXr)`, called as `ConnectOpenXr(_openXr)` after the null check in `_Ready`: the 20 moved lines read `openXr` where they read `_openXr`, so nullable analysis needs no `!` on the field. This is the only change to moved code besides the two new method wrappers and their summaries. `_Process`'s summary moved to `WatchExtraction`, and `_Process` got a one-line summary of its own.
- Fields: `_readout`, `_tiling`, `_folder`, `_located` and the tile constants stay in `Main.cs`; `_extraction` in `Main.Extraction.cs`; `_boardPlaced`, `_report` in `Main.Board.cs`; `_openXr` in `Main.OpenXr.cs`; `_choice`, `_lastWarning`, `_askedAgain` in `Main.RefreshRate.cs`.
- Line counts: `Main.cs` 190, `Main.Board.cs` 59, `Main.Extraction.cs` 85, `Main.OpenXr.cs` 81, `Main.RefreshRate.cs` 96.
- Godot's headless import wrote the `.cs.uid` files for the five new scripts; they belong in the commit with them.
- **Checks:** `tools/test.sh` 97 Python, 256 GameLogic, 118 Decoding, all pass; the prefix test failed with the generator changed to `pdz-extractor ` and passed again once restored; `dotnet build game/PanzaDragoonVR.csproj --no-incremental` 0 warnings, 0 errors; the APK lists `PdzDecoding.dll` and no disc data. On the Quest 3, `tools/build-quest.sh` exited 0 and the development-mode log showed the mode and asset lines, "board placed in front of the head", the refresh-rate lines (72 Hz first, then target 90 Hz), every extraction step through "extracted 420 textures" and the folder read again. "No asset was supplied: running on placeholders" after extraction is expected: the extractor writes only `e03.tex.*` textures until story 3.4 adds the planes. Nobody wore the headset, so the scene is known from the log only.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-22: high 0, medium 0, low 1, false 2, maybe-false 0.

- false: the headset launch check has no recorded result. The implementer ran it and reported the log lines; they are now recorded under Implementation Notes, Checks. A missing record, not a missing check.
- false: `ConnectOpenXr(OpenXRInterface openXr)` renames `_openXr` to `openXr` in the moved lines, against "code moves verbatim". No bad outcome: it is called only as `ConnectOpenXr(_openXr)` after `_Ready`'s null check, the subscriptions keep their order, and the reviewer confirmed the behaviour is the same. Recorded in Implementation Notes and raised with the author at presentation.
- low, defer: `AGENTS.md`'s Tests convention says `Decoding.Tests` references the library and implies `GameLogic.Tests` does not; it now does, for the prefix test. The fix edits an agent-context file, so it goes to deferred work.

## Design Notes

- Partial classes, not new nodes: the scene, node paths and signal wiring stay as they are, and the split is a pure move. Godot's C# generators accept `Main` spread across files as long as `Main.cs` holds the declaration the script names.
- `_Process` stays in `Main.cs` and calls `PlaceBoard` and a `WatchExtraction()` holding today's lines 95-136 verbatim; `_Ready`'s OpenXR block becomes a `ConnectOpenXr()` in `Main.OpenXr.cs` with the same order of subscriptions.
- The deletion lines in `ExtractionMode.cs` now collapse runs of whitespace as the manifest messages do: a `\r\n` in an exception message gives one space instead of two. No test or contract depends on the two spaces.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `dotnet build game/PanzaDragoonVR.csproj` -- expected: 0 warnings, 0 errors.
- `wc -l game/Main*.cs` -- expected: each under 200.
- `tools/build-quest.sh`, then logcat -- expected: the launch lines as before.
