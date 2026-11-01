---
title: 'The author tweaks tuning values in the headset'
type: 'feature'
ticket: '21'
created: '2026-11-01'
status: 'built'
baseline_revision: '7408d7238ffabf709183c166360553719f5f4a69'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Trying a tuning value means editing a file on the PC, pushing it and relaunching, which is slow when judging things like the seat by eye.

**Approach:** In development mode (the asset folder's `development` file), a list of all nine settings, hidden until asked for, lets the author change them live with a controller and save `tuning.json`; a new tool pulls the file to the PC.

## Boundaries & Constraints

**Always:** The ticket's acceptance criteria. Decision, 2026-11-01: the author was told what "development build" was taken to mean (the marker file, whatever the APK; anyone who adds it gets the menu; development mode extracts on every launch; the list starts hidden) and started the build on that. Logic is engine-free in `game/Logic/` and tested. Nothing from the disc is committed.

**Never:** A menu in release mode. A change to the rules for reading `tuning.json` or to any range or built-in value. A change to the action map. Pointer or hand-tracking input. Writing the file except on the author's press of A.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Change | Stick held right on a setting | Value rises at its rate, scene follows | Stops at the range's end |
| Pick | Stick pushed up past 0.7 | Pick moves once, wrapping; re-arms below 0.3 | Value does not change while pick is held |
| Save | A pressed | Full file replaces `tuning.json`; list says saved | Failure: list and log give the reason; the old file stays |
| No folder | Flat desktop run with no asset folder | No menu | None needed |
| Release mode | No marker | No list, no controller nodes | None needed |
| Controllers asleep | No readings | Nothing moves | None needed |
| Re-dress | Extraction finishes after edits | Edited values kept | None needed |

</frozen-after-approval>

## Code Map

- `game/Logic/Tuning.cs` -- private `Settings` and `Setting`: make public, add a rate. Add writing as text and saving. `Parse` stays the one reader.
- `game/Main.Tuning.cs` -- `ReadTuning` reads once, so edits survive a re-dress. `PlacePlanes` is safe to call at any time.
- `game/Main.cs` -- `_Ready` and `_Process` (one line each); `Dress` is the only place `tiles` is set, so split out a helper. Tile also feeds `Main.Scroll` each frame.
- `game/Logic/ExtractionMode.cs` -- `ExtractionModes.ModeOf(folder)`; `_folder` can be null.
- `lib/PdzDecoding/Extraction.cs` -- settles the ticket's unknown: extraction only empties `extracting/` and the contract leaves files it did not write, so a saved `tuning.json` stays. `tools/push-assets.sh --clear` still removes it.
- `tools/push-tuning.sh`, `tools/quest-adb.sh` -- pattern for the pull tool: `host_adb`, `find_headset`, `die`, `say`. Stream with `exec-out cat`, because the host cannot see the sandbox's paths.
- Trial code to work from (uncommitted): `/tmp/claude-1000/-home-deck-projects-PanzaDragoonVR/15cd57c4-ba9b-4c37-8fab-8e06d673aa0f/scratchpad/Main.DebugTrial.cs.txt`.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/Tuning.cs` -- public setting table with `Rate` (trial's rates; untried `canopy_height` 3, `canopy_speed_scale` 0.25, `canopy_tile` 12); `Write()` giving the JSON text with every setting at full float precision; `Save(folder)` writing a temporary file then moving it into place -- one table, and a failed write keeps the old file.
- [x] `game/Logic/TuningMenu.cs` -- new, engine-free: shown state, pick with re-arm, value change with clamp, press edges for A and B, and the list's text -- so the behaviour is tested on the PC.
- [x] `game/Main.TuningMenu.cs` -- new: in development mode only, adds both controllers and the label, feeds the readings to `TuningMenu`, applies a changed tuning, saves, logs with the `PDZ-ASSETS` tag.
- [x] `game/Main.cs` -- start and drive the menu; `tiles` helper.
- [x] `tests/GameLogic.Tests/TuningTests.cs`, `TuningMenuTests.cs` -- round trip through `Parse` with no problems, save failure, the matrix's rows.
- [x] `tools/pull-tuning.sh` -- new: `pull-tuning.sh <file>`; fails if the headset has no `tuning.json`, and replaces `<file>` only after a full copy.
- [x] `tools/README.md`, `asset-contract.md`, `AGENTS.md`, the sibling comments in `Tuning.cs` and `Main.Tuning.cs` -- the file can change in the headset in development mode; the controls; the pull tool; the new writer. Both of the contract's "never changed" statements. The spec's no-HUD rule is unaffected: the list is on the controller, and for development only; say so in the contract.

**Acceptance Criteria:**
- Given development mode in the headset, when the author moves each of the nine settings, then the scene follows at once.
- Given a saved file, when the app restarts, then the log's tuning line gives the saved values and the board has no tuning problem; when pulled, the PC's copy is byte-for-byte the headset's.
- Given the marker removed, when the app starts, then B and the stick do nothing.

## Implementation Notes

- The list is driven only while the OpenXR session is focused, so a stale reading cannot move anything while the system menu is up.
- Presses and stick pushes begun while the list is hidden do not act when it appears. A change clears the list's `saved` note.
- The pull tool also compares sizes, because `exec-out` can end early with no error.
- Not yet run on a headset: none was connected. The pull tool was tried against a fake `adb`.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 2 medium and 2 low patched; 1 rejected.

- medium, patch: a sideways hold begun while hidden changed the value when the list appeared. Latched, with a test.
- medium, patch: a failed `test -f` in `pull-tuning.sh` claimed the file was missing, though adb could have failed. Reworded.
- low, patch: a missing destination folder gave two stderr lines and blamed adb. Checked first, with one line.
- low, patch: a successful save left the launch's tuning problems to come back on the board after extraction. Cleared on save.
- medium, rejected: the headset criteria are unverified. True, but the fix is the author's check, not a code change; it stays open below.

Open at the first commit: nothing had run on a headset, which was not connected. All three acceptance criteria, the real pull and the three canopy settings' rates and `SetTiles` branch need the author. `spec-panzer-dragoon-zwei-vr.md` still says the file is edited on the PC and pushed, which remains the usual route; left as it is.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `tools/build-quest.sh` -- expected: installs and runs; `unzip -l game/build/PanzaDragoonVR.apk` holds no disc data
- `tools/pull-tuning.sh <file>`, then compare with `adb exec-out cat` -- expected: identical

**Manual checks (if no CLI):**
- The three acceptance criteria above, by the author in the headset.
- Before that check, the trial's `tuning.json` is pulled or removed as the author chooses.

Author, 2026-11-01: in the headset said "all seems fine" (the saved file shows `canopy_speed_scale` and `canopy_tile` moved, `canopy_height` ending at its built-in) and asked for the saved values to be the built-ins. That goes beyond this plan's frozen "no change to a built-in value", on the author's word, and they are rounded to three places in all four places, tests following. Pull: byte-for-byte, checked with `cmp`. A saved file read at launch: seen in the log. With the marker removed and the disc image moved aside: the log has no tuning list line, in release mode; the controls were not tried, as the list's nodes are not made. The marker and image were put back.
