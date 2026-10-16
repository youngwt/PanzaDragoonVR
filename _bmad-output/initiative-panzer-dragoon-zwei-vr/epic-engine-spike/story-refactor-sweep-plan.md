---
title: 'Refactor sweep'
type: 'refactor'
ticket: '8'
created: '2026-10-16'
status: done
baseline_revision: '3707b89b978ca128dd751ff940d37294b9b4ba86'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Seven stories grew the engine spike's code one at a time. The two device scripts each carry their own copy of the `adb` helper, the package name and the headset lookup; `AssetFolder` repeats the same lookup-and-read steps for images and models; `Main._Ready` has grown a nested fallback that is hard to follow; the asset IDs the scene asks for live in two classes; the README still says the headset runs at 72 Hz; and in debug builds the frame-rate readout covers the launch report board (deferred from ticket 1.5).

**Approach:** Tidy each of these without changing what the game or the scripts do, then run the epic's five Done when checks again on the headset.

## Boundaries & Constraints

**Always:**
- Behaviour stays as it is: the same log lines, report lines, script messages and exit statuses, except the readout's position and the README's corrected facts.
- `game/Logic/` and its tests change only if a task needs it; every existing test still passes unchanged.
- The game still always runs, whatever loading throws.

**Never:**
- No new features and no change to what the loader accepts or rejects.
- No change to the asset contract, the tickets or the Python manifest rules.
- No attempt to fix the `AndroidRuntime` folder lookup, which returns nothing on the Quest 3; the fixed path keeps working (epic 2's question).
- No asset files committed.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Own files | Two images and the test model pushed | Images on ground and sky, the model flies; no board | No error expected |
| One file removed | A listed file deleted on the headset | Board names its ID and path; its placeholder shows; the rest load | Shown and logged as before |
| Empty folder | `push-assets.sh --clear` | Placeholders; the notice on the board | Not an error |
| Loading throws | An unexpected exception while loading | Placeholders; board says loading failed, keeping problems found before | Logged as before |
| No headset | Either device script with nothing connected | Same one-line message and exit status as now | As now |

</frozen-after-approval>

## Code Map

- `tools/build-quest.sh` (lines 23, 50-56, 103-111) and `tools/push-assets.sh` (lines 22, 36-42, 73-80) -- identical `PACKAGE`, `host_adb()` and serial-picking block; build-quest's "no headset" and "not authorised" messages end with `(the APK is in $apk)`, push-assets' do not. Each keeps its own `die`/`say` (the prefix differs).
- `game/AssetFolder.cs` -- `Image()` (83-128) and `LoadModel()` (159-207) share: `_read.Find`, `_lookups.Add`, the NotListed/Problem switch, `GetRelativePath`, `File.ReadAllBytes` with its catch, and `_placeholders.Add` on every null return. `Model()` wraps `LoadModel()` in a catch that must stay.
- `game/Main.cs` `_Ready` (32-134) -- asset loading, the failure fallback (nested `try` around `FailedReport`, `LogReport` called on two paths so each report is logged once), panel, `Dress`, `Carry`. The class summary names stories 1.3 to 1.7.
- `game/PlaceholderTextures.cs` (`GroundAssetId`, `SkyAssetId`) and `game/FlyingCube.cs` (`AssetId`) -- the three IDs; used in `Main.cs` only.
- `game/Main.tscn` -- `FrameRateReadout` at (0, -0.25, -1.2) under the camera (eye height 1.6); `LaunchReport` at (0, 1.6, -2.2), its board up to about 0.6 m tall.
- `tools/README.md` lines 176 and 187 -- an example log line and a sentence still saying the Quest 3 runs at 72 Hz; line 147 has the current fact (90 Hz since ticket 1.10).
- Leave alone: `game/Logic/*`, `tools/manifest.py`, `tools/write_manifest.py`, `tools/setup-toolchain.sh`.

## Tasks & Acceptance

**Execution:**
- [x] `tools/quest-adb.sh` (new, sourced), `tools/build-quest.sh`, `tools/push-assets.sh` -- one home for `PACKAGE`, `host_adb` and finding the one connected headset, with the caller adding its own suffix to the messages -- removes the copies.
- [x] `game/AssetFolder.cs` -- one private helper for the shared lookup-and-read steps, recording the placeholder in one place; `Image` and `LoadModel` keep only what is theirs.
- [x] `game/Main.cs` -- move asset loading and its fallback into a method of its own, so each report is built and logged exactly once on every path; describe what the class does rather than which story added what.
- [x] `game/AssetIds.cs` (new), `game/PlaceholderTextures.cs`, `game/FlyingCube.cs`, `game/Main.cs` -- the three IDs the scene uses in one class.
- [x] `game/Main.tscn` -- move the readout down out of the board's way.
- [x] `tools/README.md` -- the example log line and the bar sentence match the 90 Hz result; mention `quest-adb.sh` where the scripts are listed.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every Python and C# test passes.
- Given `tools/build-quest.sh` with no headset connected, when it runs, then it builds and ends with the same "no headset found" line as before.
- Given the APK on the Quest 3 and the readout visible, when the app starts with a launch report showing, then the readout and the board do not overlap in the starting view.
- Given the headset, when the epic's Done when checks 1 to 4 are run, then each passes: 90 Hz in `adb logcat -s PDZ-FPS`, own images on ground and sky, a removed file reported with its placeholder, and placeholders on an empty folder.
- Given `git diff --stat` against the baseline, when read, then no file outside Tasks and this plan changed.

## Implementation Notes

- `tools/quest-adb.sh` holds `PACKAGE`, `host_adb` and `find_headset [<suffix>]`, which sets `serial` or calls the sourcing script's `die`. build-quest passes `", then run this again (the APK is in $apk)"`; push-assets passes nothing.
- `AssetFolder`: `Load<T>(id, kind, use)` records the ID as supplied or placeholder in one place; `Read` does the lookup, the relative path and the file read; `Decode` (image) and `BuildModel` (model) keep only their own checks. `Model`'s catch is unchanged.
- Each report is logged once: `AssetFolder.Report()` and `FailedReport()` no longer log; `Main.LoadAssets()` builds the report on whichever path and calls `AssetFolder.LogReport` once.
- `FrameRateReadout` moved from y -0.25 to -0.45 (1.2 m ahead): its top is about 16 degrees below the view's centre, the board's bottom edge about 8 degrees (13 degrees for a 1 m tall board).
- Godot generated `game/AssetIds.cs.uid`, as for every other script; it belongs with `AssetIds.cs`.
- Verified on the desktop: the `PDZ-ASSETS` log lines for no folder, own files, one file missing, a bad image and model, and an empty folder are identical before and after (headless runs with `PDZ_ASSETS`). Not yet verified: the headset checks (no headset was connected) and the "loading throws" path, which cannot be triggered without changing code.
- "Loading throws" row, 2026-10-16: a temporary `throw` after the sky lookup, desktop run with one image listed and missing. The log read `loading the asset folder failed (InvalidOperationException: injected); running on placeholders`, then the report once: "The asset folder could not be loaded", the folder, `e03.canopy: images/e03.canopy.png: missing` and the exception. The throw was removed and the solution rebuilt.

- **Headset checks, 2026-10-16** (the author wore the headset and cleared a Guardian prompt; `tools/build-quest.sh` and `tools/push-assets.sh` built, installed and pushed through `quest-adb.sh`):
  - Done when 1: `PDZ-FPS` read `target=90`, averages 90.0 after the first second (84.4 to 86.2 while starting), `slow_pct` under 1 within about ten seconds; readout "Target 90 Hz (4 rates offered)", "Measured 90.0 Hz". Each run was read for about 15 seconds, not a full minute; ticket 1.10 had already shown a minute at 90.
  - Done when 2: with two generated images and the test model pushed, the log read `supplied: e03.floor, e03.canopy, e03.flyer; placeholders: none` and a capture showed the striped floor.
  - Done when 3: with `e03.canopy.png` deleted on the headset, the board read "1 problem with the assets" and `e03.canopy: images/e03.canopy.png: missing`; the sky showed its chequer placeholder and the floor its supplied image.
  - Done when 4: after `push-assets.sh --clear`, the board read "Running on placeholders" with the folder path, and a capture showed the green ground, the blue sky and the grey cube.
  - Readout and board: in the captures of both board states, the readout sits below the board's lower edge in both eyes, with the head tilted.
  - The headset's asset folder was left empty and the proximity sensor restored.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens), 2026-10-16: high 0, medium 1, low 0, false 0, maybe-false 0. One carried.

- medium, carried: the headset criteria (readout clear of the board; Done when checks 1 to 4) are unverified, because no headset was connected. There's no code to fix: the reviewer re-checked the geometry, tests, build, script messages, `AssetFolder` order and single report logging, and found nothing else. These checks wait for the headset. Since met: see the headset checks in Implementation Notes.

## Design Notes

- **The readout moves, not the board.** The board is the player's; the readout is a debug aid that release builds do not show.
- **`AssetIds` is the start of the game's ID list.** The dragon and enemy epics will add many IDs; one class beside the asset contract's prefix table is where they go.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all tests pass.
- `tools/build-quest.sh --no-install`, then `unzip -l game/build/PanzaDragoonVR.apk | grep -c e03` -- expected: builds; 0.
- `tools/push-assets.sh` with a folder and with `--clear`, and `tools/build-quest.sh`, on the headset -- expected: pushes, installs and launches as before.

**Manual checks (if no CLI):**
- Screen captures (`adb exec-out screencap -p`, proximity sensor held closed) for own files, one file removed and an empty folder; `adb logcat -s PDZ-FPS` while running reads `target=90` with averages of at least 89. A Guardian prompt can block a launch; then the author looks.
