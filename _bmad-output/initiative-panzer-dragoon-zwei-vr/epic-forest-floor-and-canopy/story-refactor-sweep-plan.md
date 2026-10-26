---
title: 'Refactor sweep'
type: 'refactor'
ticket: '6'
created: '2026-10-26'
status: done
baseline_revision: '5ccc30eb552a3d0064fef9a0109ed630f320ab09'
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

**Problem:** Five stories and a spike grew the scroll, the tuning file and plane extraction one at a time. A survey of `b131013..HEAD` and the epic's plans (2026-10-26) found dead code, names that mix ground/sky with floor/canopy, constants and paths repeated across files, a test helper doing three jobs, docs naming story numbers as future work, and AGENTS.md out of date with what was built.

**Approach:** Cleanup only: move, merge, rename private names and delete, without changing what extraction writes or what the game shows or logs; bring AGENTS.md and the README up to date with the code; then check the epic's Done when still holds on the headset.

## Boundaries & Constraints

**Always:**
- Behaviour stays: the same files, manifest, log lines and scene. `pdz-extract` output is byte-identical to a run at the baseline on the JP cue and the PAL ISO.
- Every existing test passes with its assertions unchanged; moved tests keep their names.
- Story and ticket numbers stay only as provenance ("found in spike 3.2"); "story N will" about done work goes.

**Never:**
- No renaming of public library types or members the PC command, the game or tests rely on (`ColourBanks.ProgramFile`, `Planes`, `Extraction` members stay); no `Episode3Program` split.
- No changes to the review items rejected earlier: the "nothing written" line when only the planes are written, duplicate keys in `tuning.json`, `Locate()` throwing.
- No change to `plane_probe.py`, the reference the C# port is tested against, nor to the camera's 1.6 m in `Main.tscn`.

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/ColourBanks.cs:195-201` -- `FromDisc` has no callers now (`Extraction.Run` calls `ReadProgram` and `FromProgram`); delete it, moving its "modes 1 and 5 are still written" note to `ReadProgram`.
- `lib/PdzDecoding/Planes.cs:341,352` -- `& 0x7FFFF` repeats `VramSize - 1`: name it. `:47-50` `FloorId`/`CanopyId` repeat `game/AssetIds.cs:11,14`: have `AssetIds` use the library's.
- `game/Main.Scroll.cs`, `game/Main.cs`, `game/Logic/PlaneScroll.cs` -- private names and parameters `_groundMaterial`, `_skyMaterial`, `skyHeight`, `SkySpeed` mix ground/sky with the floor/canopy of the tuning and IDs: rename to floor/canopy (scene node names `Ground`/`Sky` stay).
- `game/Main.Tuning.cs:15` -- a `using` for `IReadOnlyList`. `game/Main.Board.cs:54` -- reuse the `Board` node lookup at :17 rather than repeating "LaunchReport".
- `game/Main.tscn:63` -- the `Sky` node's y of 40 is overwritten at launch by `PlacePlanes`; set it to the built-in 10 so the editor matches.
- `game/Logic/Tuning.cs:37` -- doc says each `Setting` has "its built-in value"; it has none: fix. `game/Logic/LaunchReport.cs:171-176` -- odd line break in a doc comment.
- `tools/push-tuning.sh:21`, `push-assets.sh:24`, `compare-headset.sh:22` -- the same `REMOTE=` in three scripts: move it into the sourced `quest-adb.sh`.
- `tests/Decoding.Tests/SyntheticDisc.cs:324-425` -- plane program builders: move to `PlaneProgram.cs` (or similar). `tests/Decoding.Tests/ExtractionTests.cs:977-979` -- use the library's floor/canopy path constants. `tests/GameLogic.Tests/ExtractionModesTests.cs:287+` -- the `OneLine_*` tests to their own `OneLineTests.cs`, and the class summary at :13 back to its own job.
- New test: `tools/tuning.example.json` equals `Tuning.BuiltIn` (read the file from the repo).
- `tools/README.md:617` ("prototype for story 3.4"), `:203` (Python tests list omits `test_plane_probe.py`), `:205` (GameLogic.Tests paragraph: the tuning, scroll and launch-report tests, and the library reference for tests only).
- `AGENTS.md` -- :51 `GameLogic.Tests` also references the library, for tests only (closes the deferred-work item from ticket 3.7: append a closing entry to `deferred-work.md`); :46 the refresh-rate warning now in `Main.RefreshRate.cs`; `plane_probe.py` follows the epic-era error rules with f-strings; "Rules kept in two places" gains the tuning file (`push-tuning.sh`'s JSON check and `Tuning.cs`; the built-ins in `Tuning.cs`, the example, the README and the contract).

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/` -- the dead `FromDisc`, the VRAM mask, the IDs shared with `game/AssetIds.cs`.
- [x] `game/` -- floor/canopy names, the `using`, the board lookup, the scene's Sky y, the two doc comments.
- [x] `tools/` -- `REMOTE` into `quest-adb.sh`; README fixes.
- [x] `tests/` -- the plane builders' file, the path constants, `OneLineTests.cs`, the example-file test.
- [x] `AGENTS.md`, `deferred-work.md` -- as the Code Map says.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every test passes, no existing assertion changed, and the new example-file test passes; the game builds with 0 warnings.
- Given `pdz-extract` on the JP cue and the PAL ISO, when compared with runs at the baseline, then `images/` and `manifest.json` are byte-identical.
- Given the headset, when `build-quest.sh` extracts in development mode, then `compare-headset.sh` reports identical, the log shows the tuning and plane lines as before, and the forest floor scrolls beneath with the canopy overhead.

## Implementation Notes

- Done as the Code Map says. Extras in the same spirit: the `AssetIds` doc comments, `LoadAssets()`'s tuple names and the locals in `Main.Extraction.cs` say floor/canopy; `PlaceholderTextures.Ground()`/`Sky()` and the scene's node names stay. The plane builders went into `tests/Decoding.Tests/SyntheticPlanes.cs` (a class cannot share the name of its `PlaneProgram()` method).
- `PlaneScroll.SkySpeed` became `CanopySpeed`, as the Code Map's rename asked; it is public, so its two tests in `PlaneScrollTests.cs` changed the method they call and their names (`SkySpeed_*` to `CanopySpeed_*`), with the same values asserted. This bends "assertions unchanged, names kept"; raised with the author at presentation.
- **Checks:** `tools/test.sh` 105 Python, 297 GameLogic (296 plus `TuningTests.ExampleFile_GivesEverySettingAtItsBuiltInValue`), 152 Decoding, all pass; the test lists differ from the baseline only by the moved `OneLine_*` tests, the two renamed tests and the new test. The game, both test projects and `pdz-extract` build with 0 warnings. `pdz-extract` at the baseline (a temporary worktree, removed afterwards) and after, on the JP cue and the PAL ISO: `diff -r` found no difference in `images/` or `manifest.json`, and the console output was the same. On the Quest 3, `build-quest.sh` exited 0, development mode extracted 420 textures, the log showed the same tuning, extraction and plane lines as before, and `compare-headset.sh` on the JP cue reported identical, 423 files. The scene itself was not seen then: the headset was asleep.
- **Headset, 2026-10-26, with the author:** the forest floor scrolls beneath with the canopy overhead as approved in 3.5 ("confirm").

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-26: high 0, medium 0, low 3, false 2, maybe-false 0. The reviewer re-ran `tools/test.sh` and the game build, and checked that nothing calls the deleted `FromDisc`, no old names or `0x7FFFF` are left, `REMOTE` is shared, the moved tests and builders are unchanged, and the Sky node's 10 matches the built-in.

- low, recorded: the `SkySpeed` rename changed two existing tests' names and the method their assertions call, against the plan's Always rule, which the Code Map's rename contradicts; recorded in Implementation Notes and raised with the author.
- false: no record that `pdz-extract` output was compared with the baseline. The implementer ran it and reported no difference; the record is now in Implementation Notes.
- false: no record of the headset check. `compare-headset.sh` reported identical and the log matched; now recorded. Seeing the scene is left to the author.
- low, patch: AGENTS.md listed `plane_probe.py` under the tool rules' known exceptions while saying it follows them. The entry now names only its real exceptions (usage inside the error line; exit 1 when the planes differ). Edited here rather than deferred, as AGENTS.md is in this ticket's approved scope.
- low, patch: `PlaceholderTextures.cs`'s doc comments still said ground and sky; now floor and canopy (the `Ground()`/`Sky()` names stay).

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `pdz-extract` on both discs before (at the baseline, in a scratch worktree) and after, `diff -r` -- expected: no difference.
- `tools/build-quest.sh`, then `tools/compare-headset.sh "rom/JP/Panzer Dragoon II Zwei (Japan).cue"` -- expected: identical.
