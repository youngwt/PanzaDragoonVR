---
title: 'Refactor sweep'
type: 'refactor'
ticket: '12'
created: '2026-10-21'
status: 'built'
baseline_revision: '8cc9fa121800466205beeacc8410b0467381f3e6'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Twelve stories grew the extraction code and its docs one at a time. The library's `Extraction.cs` holds four jobs and some dead API; small pieces of logic are written two or three times; `Main.cs` repeats one try/catch three times; some warnings can break the one-line log rule; comments and the README still speak of later stories, say "not yet" about done work, contradict themselves in one place, and bury the headset instructions out of order.

**Approach:** Cleanup only, scoped from the epic's plans, their review logs and a survey of the code (2026-10-21): move, merge and delete without changing what extraction writes or what the game shows, then check the epic's Done when still holds.

## Boundaries & Constraints

**Always:**
- Behaviour stays: the same files, manifest and log lines, except warnings whose .NET message spanned lines or ended in a full stop, which become one line with no full stop as `AGENTS.md` requires.
- Every existing test passes unchanged, apart from renamed comments; the PC output stays byte-identical to today's.
- Story and ticket references stay only as provenance (e.g. "seen on the Quest 3, ticket 2.8"); "story N will", "not yet" and "so far" about done work go.

**Never:**
- No new behaviour: the deferred cue-names-an-`.iso` question, the `DecodedFile` collections and other rejected review items stay as they are.
- No renaming of public types tests rely on (`ExtractionModes`, `BoardPlacement.InFrontOf`).
- No change to the asset contract or the tickets.

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Extraction.cs` (459 lines) -- `DiscFolder`/`DiscSearch`/`DiscFolderState` (folder lookup, image file list), result and progress types, `Run` with staging, `Decode`. `Decode(DiscImage, string)` and its private `Read` have no callers. `FolderFor` and `Run`'s return both build `images/e03`. `Version`'s doc says "will give"; `DiscFileResult.LeftOut`'s doc says "not decoded yet".
- `lib/PdzDecoding/DiscImage.cs` `OpenImage`, `ReadHeader`, and `Extraction.cs` `DiscFolder.ImageFiles` -- three copies of the `.cue`/`.iso` extension switch and its message.
- `lib/PdzDecoding/CueSheet.cs` -- `Read` and `ReadFiles` repeat the read-and-catch; `Parse` and `Files` repeat the folder computation.
- `game/DiscExtraction.cs` -- `Reason(e)` (line ~246) used once while lines ~106 and ~173 build the same text; `Succeeded` counts disc files that wrote something, as `tools/pdz-extract/Program.cs` does.
- `game/Main.cs` (446 lines) -- the "show on the board, warn on failure" try/catch at ~134-139, in `ShowProgress`, in `ShowAssets`; warnings at ~128, 138, 167, 186, 199, 243 print raw `e.Message`.
- `game/AssetFolder.cs:50` -- parameterless `Open()` unused.
- `tools/README.md` -- the extraction section ("What it extracts so far", one long paragraph restating the contract); "On the headset" (~457-531): everything sits under the modes heading, "The scene does not wait for it" has no subject, push instructions come after the failure table; the log table row "extracting ... after the scene" contradicts the code and prose; "story 9's" / "story 10's" wording (~205-207); the manual `adb pull` block duplicates `compare-headset.sh`.
- Tests with "story N's matrix" in comments: `tests/Decoding.Tests/ExtractionTests.cs:~780`, `tests/GameLogic.Tests/ExtractionLinesTests.cs:9`, `ExtractionModesTests.cs:10`.
- `_bmad-output/initiative-panzer-dragoon-zwei-vr/deferred-work.md` -- the board-behind-the-player item (2.9) is done by story 2.13; append a closing entry, as earlier sweeps did.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/` -- move `DiscFolder`, `DiscSearch`, `DiscFolderState` into `DiscFolder.cs`; remove the unused `Decode(DiscImage, string)` and `Read`; one helper for the image-type switch; one read helper and one folder computation in `CueSheet`; one `images/e03` constant; an `ExtractionResult` property for disc files that wrote something; fix the stale doc comments.
- [x] `game/DiscExtraction.cs`, `tools/pdz-extract/Program.cs` -- use `Reason` everywhere and the new result property.
- [x] `game/Main.cs`, `game/AssetFolder.cs` -- one try/catch helper for the board; warnings through `DecodingException.OneLine`; remove the unused `Open()`.
- [x] `tools/README.md` -- reorder "On the headset" (setup and push, modes, progress and failure board, log table, with a heading for the board); fix the contradicting row; split "What it extracts" by topic without "so far"; name behaviours instead of story numbers; point the manual pull at `compare-headset.sh`.
- [x] Test comments -- name the behaviour instead of "story N's matrix".
- [x] `deferred-work.md` -- a closing entry for the board item.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every test passes and no test's assertions changed.
- Given the Japanese disc, when `pdz-extract` runs, then its output is byte-identical to the output before the sweep; and `tools/compare-headset.sh` after a launch on the headset reports identical.
- Given the headset, when the app is launched in development mode, then it extracts, shows the scene and the report as before (the epic's Done when 1, 3, 4 and 5 still hold).
- Given `git diff --stat`, when read, then `Extraction.cs` is under 400 lines and nothing outside the Code Map changed.

## Implementation Notes

- Library: `DiscFolder`, `DiscSearch`, `DiscFolderState` moved to `lib/PdzDecoding/DiscFolder.cs`; `Extraction.cs` 459 to 363 lines. Unused `Decode(DiscImage, string)` and `Read` removed. One internal `DiscImage.IsCueSheet` for the `.cue`/`.iso` switch and its message (used by `OpenImage`, `ReadHeader`, `DiscFolder.ImageFiles`). `CueSheet` has one `ReadText` and one `FolderOf`. One `ImageFolder = "images/e03"` constant. New `ExtractionResult.DiscFilesWritten`. Stale docs on `Version` and `LeftOut` fixed.
- Game: `DiscExtraction.Reason` (now public) used at all three places and by `Main`'s six warnings, so a multi-line or full-stopped .NET message is one line; one `OnBoard(what, Action)` helper replaces three try/catch copies in `Main.cs`; unused `AssetFolder.Open()` removed. `pdz-extract` and the game use `DiscFilesWritten`.
- README: "What is extracted" and "What is written" in place of one long paragraph; "On the headset" reordered (putting the disc on, modes, a new heading for the board while extracting and on failure, then reading the log); the contradicting log row fixed; behaviours named instead of story numbers; the manual pull points at `compare-headset.sh`. Three test comments name the behaviour; no test code changed. `deferred-work.md` closes the board-behind item.
- Left as they are (outside the Code Map): "The story's bar" in the frame-rate section, "(epic 2)" in the README, "the story's matrix" in `LaunchReportTests.cs`.
- **Checks:** `tools/test.sh` 97 Python, 251 GameLogic, 118 Decoding, all pass; builds with 0 warnings. `pdz-extract` on the Japanese cue and the European ISO, before and after: identical `images/` and `manifest.json` (also re-checked by the orchestrator against a run at the baseline into `extracted/sweep-before`). On the Quest 3, `build-quest.sh` built, installed and launched in development mode: the log shows the mode line, "board placed in front of the head", every progress step and "extracted 420 textures from 9 disc files ... in 0.4 s"; the image was kept. `compare-headset.sh` then reported "identical: 421 files, the manifest included". The APK holds `PdzDecoding.dll` and no disc data. Nobody wore the headset during that run, so the scene and board are known from the log only.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-21: high 0, medium 0, low 0, false 0, maybe-false 0. No findings. The reviewer re-ran `tools/test.sh` and the game build, and checked by reading that the moved and merged code behaves as before, removed code had no callers, the error text is unchanged, links resolve, and only comments changed in tests. The disc and headset criteria rest on the checks in Implementation Notes; the PC output was also compared by the orchestrator against a baseline run.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `pdz-extract` on `rom/JP/...cue` into a scratch folder, `diff -r` against a run made before the change -- expected: no difference.
- `tools/compare-headset.sh "rom/JP/Panzer Dragoon II Zwei (Japan).cue"` after a headset launch -- expected: identical.
