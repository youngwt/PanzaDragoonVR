---
title: 'Floor depth, canopy height and scrolling come from a tuning file'
type: 'feature'
ticket: '3'
created: '2026-10-22'
status: done
baseline_revision: '9cdc5320b1d702399e8ae0946c135f1a8a9fb149'
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

**Problem:** Floor depth, canopy height, speeds and image sizes are constants in the code (`Main.cs`, `Main.Scroll.cs`), so trying a new look means a rebuild. The epic's Decision makes them a small file the author edits on the PC and pushes with adb; the spec and the asset contract still say these are "set in the headset".

**Approach:** An optional `tuning.json` at the top of the asset folder, read once at launch. Each setting it gives replaces the built-in value; a missing file means all built-ins and no report; a setting that is wrong is reported on the launch board and its built-in value used, the others still applied. The contract gains a Tuning section defining the file, and the spec's Constraints line and the contract's "Not in the contract" line are reworded to it. A push script and an example file make the round trip one command.

The file (all numbers, metres or metres a second; every field optional; built-in values shown):

```json
{
  "floor_depth": 4,
  "canopy_height": 40,
  "flight_speed": 4,
  "canopy_speed_scale": 1,
  "floor_tile": 2,
  "canopy_tile": 20
}
```

- `floor_depth`: the ground plane's distance below the play area's floor (3 to 1000). The player rides the dragon at the play area's floor, and the dragon must be at least 3 m off the ground (the author, 2026-10-22); the built-in 4 m allows about a metre for the dragon's body below the rider.
- `canopy_height`: the canopy plane's height above the play area's floor (2 to 1000, so it is above the head).
- `flight_speed`: the floor's speed (0 to 100).
- `canopy_speed_scale`: how the canopy moves, as a multiple of the speed that matches the floor's angle a second (1 = a flat backdrop at the floor's on-screen speed, 0 = still; 0 to 10).
- `floor_tile`, `canopy_tile`: metres one copy of the image covers (0.1 to 1000).

## Boundaries & Constraints

**Always:**
- The parsing and range rules are engine-free in `game/Logic/` with xUnit tests; the game only reads the file and applies the values.
- The game never refuses to run because of the tuning file; problems are on the board and in the log as `PDZ-ASSETS` lines, and survive the board's rebuild after extraction.
- The sky's speed keeps the 3.1 angle rule, with the eye assumed 1.6 m above the play area's floor (so the floor is `floor_depth` + 1.6 m below it and the canopy `canopy_height` − 1.6 m above), times `canopy_speed_scale`.

**Never:**
- No in-headset controls; no reload while running (relaunch to apply).
- No change to the manifest, its rules, `format`, or the decoding library; `tuning.json` is not listed in the manifest.
- No change to `Main.tscn`: positions are set from code.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No file | no `tuning.json` | built-in values | nothing reported |
| All good | every field in range | those values | nothing reported |
| Some fields | only `flight_speed: 6` | 6 m/s, built-ins elsewhere | nothing reported |
| Not JSON / not an object | `{flight` or `[1]` | built-in values | one problem: the file cannot be read, with the reason, one line |
| Out of range / wrong type | `canopy_height: 1`, `floor_depth: 2`, `floor_tile: "2"` | built-ins for those three, the rest used | one problem per field, naming it and its allowed range |
| Unknown field | `flight_sped: 6` | ignored | one problem naming it (catches typos) |
| Unreadable file | permission error | built-in values | one problem with the reason |

</frozen-after-approval>

## Code Map

- `game/Main.cs` -- `GroundTileMetres`, `SkyTileMetres`, `LoadAssets` (builds the report at launch and after extraction; tuning problems must join it both times), `ShowAssets`, `Dress`; `_folder` from `AssetFolder.Locate()`.
- `game/Main.Scroll.cs` -- `FlightSpeed` 4, `EyeHeight` 1.6, `SkyHeight` 40, static `SkySpeed`; becomes values from the tuning.
- `game/Main.tscn` -- `Ground` at y 0, `Sky` at y 40; set their positions from the tuning in code.
- `game/Logic/PlaneScroll.cs` -- `SkySpeed(floor, depth, height)`; reuse.
- `game/Logic/AssetManifest.cs` -- the pattern for reading JSON with `System.Text.Json` and naming problems; `OneLine.Of` for exception messages.
- `game/Logic/LaunchReport.cs` -- `Build`, `WithExtractionProblem`, `Kind None` shows no board; tuning problems need a way in that shows the board even when nothing else is wrong.
- `tools/push-assets.sh`, `tools/quest-adb.sh` -- the pattern for a device script (`die`, `say`, host adb, `$PACKAGE`); push through `adb exec-in` as `push-assets.sh` does, since the host cannot see the sandbox's `/tmp`.
- `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md` -- "The folder" (fixed names list, line ~34), "What the game does at launch" table, "Not in the contract" line 300.
- `.../spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md:70` -- the Constraints line.
- `tools/README.md` -- headset section near `push-assets.sh` (~317).

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/Tuning.cs` -- the settings with built-ins, and a parse from text giving the settings and a list of problems -- the matrix.
- [x] `tests/GameLogic.Tests/TuningTests.cs` -- one test per matrix row, plus each field's range ends.
- [x] `game/Logic/LaunchReport.cs` (+ tests) -- a way to add tuning problems that shows the board on its own and keeps the folder's report.
- [x] `game/Main.cs`, `game/Main.Scroll.cs` (or a `Main.Tuning.cs`) -- read `tuning.json` from the asset folder at launch, log and report its problems, place the planes and use its speeds and tile sizes.
- [x] `tools/push-tuning.sh` + `tools/tuning.example.json` -- push one tuning file to the headset's asset folder; the example holds the built-in values.
- [x] `asset-contract.md`, `spec-...md`, `tools/README.md` -- the Tuning section and table rows; the two reworded lines; how to edit and push.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then all tests pass, the new ones among them; the game builds with 0 warnings.
- Given the headset and `push-tuning.sh` with a changed `floor_depth`, `canopy_height` and `flight_speed`, when the app is relaunched, then the floor is lower, the canopy at the new height and the scroll at the new speed, as the author sees it.
- Given a pushed file with a bad field, when the app is launched, then the board names the field and the scene uses the built-in for it.

## Implementation Notes

- `Tuning` (record) and `TuningRead` in `game/Logic/Tuning.cs`; problem lines start `tuning.json: ` and have no closing full stop. `Main.Tuning.cs` reads it once from `LoadAssets` (after the folder is located, or with none), so `LoadAssets` adds the problems to both the launch and the after-extraction report via `LaunchReport.WithTuningProblems`.
- `WithTuningProblems`: none leaves the report as it is; on a `None` report it gives a `<n> problem(s) with tuning.json` board; otherwise it keeps the heading and details, adds a detail line and puts the tuning problems ahead of the folder's (so the 6-line board cap never hides them), and the kind becomes `Problems` (a notice turns amber).
- Planes are placed in `ShowAssets` (`Ground` at y −`floor_depth`, `Sky` at y `canopy_height`); `Main.tscn` is unchanged.
- `push-tuning.sh <file> | --remove`; checks JSON with python3 (BOM allowed, NaN/Infinity rejected, reason in the error line), streams through `adb exec-in "cat > ..."`.
- Verified on the headset: the example file gives the tuning log line with no problems; a file with `floor_depth: 2` and `flight_sped` gives both problems on the board and in the log with `canopy_height 60` still applied. The example file was pushed back afterwards. Seeing the change by eye is left to the author.
- **Headset, 2026-10-22, with the author:** pushed `floor_depth` 10, `canopy_height` 20, `flight_speed` 8 and a misspelt `flight_sped`; the author confirmed the floor lower, the canopy lower, the faster scroll and the board naming the misspelt field ("lgtm"). The example file with the built-ins was pushed back afterwards.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-22: high 0, medium 0, low 5, false 0, maybe-false 0. The reviewer re-ran the GameLogic tests and the game build and checked the AGENTS.md rules.

- low, patch: with 6 or more folder problems, tuning problems came after them and were cut from the board by `MaxShownProblems`, so the board did not name the field. Tuning problems now come first.
- low, patch: `push-tuning.sh` hid Python's reason, accepted `NaN`/`Infinity` the game rejects, and rejected a UTF-8 BOM the game accepts. The check now reads `utf-8-sig`, rejects the non-standard constants and gives the reason.
- low, patch: the contract's tuning table intro ("when the folder has a `tuning.json`") contradicted its first row ("No `tuning.json`"). Reworded.
- low, rejected: a field given twice in the file is handled once per copy, so the board can contradict the value used. Needs a hand-written file with a repeated key, and the fix adds a branch.
- low, rejected: if `AssetFolder.Locate()` throws at launch, the tuning is fixed at the built-ins even if a later read finds the folder. `Locate` catches its own errors, so this needs an unexpected exception, and the fix adds a guard.
- Acceptance criterion 2 (by eye in the headset) is checked with the author before the plan is presented.

## Design Notes

- Heights are measured from the play area's floor, not the eye, because that is where the scene's nodes sit; the eye height enters only the sky's speed.
- With the built-ins the floor is 5.6 m below the eye, so it seems slower than in 3.1 at the same 4 m/s, and the sky runs at 4 × 38.4 / 5.6 ≈ 27.4 m/s.
- `push-assets.sh --clear` empties the folder, so it removes `tuning.json` too; say so in the README.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `dotnet build game/PanzaDragoonVR.csproj --no-incremental` -- expected: 0 warnings.
- `tools/push-tuning.sh tools/tuning.example.json` then `tools/build-quest.sh` -- expected: logcat shows the tuning read with no problems.

**Manual checks (if no CLI):**
- The author in the headset, the second and third acceptance criteria.
