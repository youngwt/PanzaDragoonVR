---
title: 'Launch report and fallback'
type: 'feature'
ticket: '5'
created: '2026-10-14'
status: done
baseline_revision: '07fd277ceace4fcdbf9bf617869abb525fc5fead'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 1.4 falls back to placeholders correctly, but every problem goes only to the log, which the headset deletes soon after the app closes. A player whose copy went wrong sees chequers and is never told why.

**Approach:** Show the launch check's findings as a report readable in the headset, worded as the asset contract's launch table words them: a short notice when running on placeholders by design, a list of problems when something is wrong. Each affected ID keeps showing its placeholder.

## Boundaries & Constraints

**Always:**
- The asset contract's launch table decides what is a problem, what is only a notice, and what is said about each.
- Every problem is listed, not only the first; a long list is shortened with a count of the rest.
- The report is part of the world, not stuck to the player's face: the spec allows only the radar as a HUD element.
- The report's text is built by engine-free C# in `game/Logic/`, unit-tested on the PC.
- Everything shown is also logged with the `PDZ-ASSETS` prefix.
- The game still always runs.

**Never:**
- No controller input (epic 5).
- No models (story 1.7) and no extraction (epic 2).
- No change to which file the loader accepts or rejects.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| All good | Valid manifest, both images load | No report | No error expected |
| Empty folder | No `manifest.json` | A short notice: running on placeholders, with the folder path | Not an error |
| One image missing | Listed file deleted | Report names its ID and path as missing; its placeholder shows; the other image loads | Shown and logged |
| Truncated file | Listed file shorter than `bytes` | Report says incomplete or changed, with ID and path | Shown and logged |
| Unreadable manifest | Not JSON, not UTF-8, or a required field absent | Report says the manifest cannot be read, with the reason; placeholders | Shown and logged |
| Unsupported format | `format` other than 1 | Report says the assets were made by a newer or older tool | Shown and logged |
| Many problems | More than fit | The first several, then "and N more" | Full list in the log |

**Decision (author, 2026-10-14):** the report stays on screen for as long as the app runs; dismissing it comes with controller input in epic 5.

</frozen-after-approval>

## Code Map

- `game/Logic/AssetManifest.cs` -- `AssetManifest.Load`/`Parse` return a `ManifestRead` (`Status`, `Manifest`, `Problem`); `AssetManifest.Problems` lists manifest-level problems; `Find(id, kind)` returns an `AssetLookup` (`Status`, `FullPath`, `Problem`). The report text comes from these; do not re-implement the checks.
- `game/AssetFolder.cs` -- `Open()`, the image loading per ID, `LogSummary()` and `LogTag = "PDZ-ASSETS"`. Collect what it logs into one structure the report can show.
- `game/Main.cs` -- `_Ready` wraps asset loading in a `try` that falls back to placeholders; the readout under the camera is `FrameRateReadout` (a debug aid, head-locked). The report is a separate node placed in the scene.
- `game/Main.tscn` -- origin, camera, ground, sky, cube; the cube flies a 4 m circle around the player.
- `tools/push-assets.sh` -- pushes a checked folder; `--clear` empties the headset folder. To make a broken folder on the headset for testing, push a good one and then change files directly with `adb shell` (the push refuses folders that fail the check).
- Device checks: the app launches without a controller; `adb exec-out screencap -p` with the proximity sensor held closed shows the view (see `tools/README.md`). A Guardian prompt can block a launch.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/LaunchReport.cs` -- build the report's lines from the manifest read and the per-ID lookups: notice or problems, contract wording, shortening with a count.
- [x] `tests/GameLogic.Tests/LaunchReportTests.cs` -- one or more tests per matrix row.
- [x] `game/LaunchReportPanel.cs` and `game/Main.tscn` -- a readable panel in the world in front of the starting view, shown when there is something to say.
- [x] `game/AssetFolder.cs`, `game/Main.cs` -- gather the findings and hand them to the panel.
- [x] `tools/README.md` -- what the report means and how to read the full list in the log.

**Acceptance Criteria:**
- Given both images pushed and then one deleted on the headset, when the app starts, then the report names that image's ID and path as missing, its placeholder shows, and the other image loads.
- Given a listed file truncated on the headset, when the app starts, then the report says it is incomplete or changed.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

- `LaunchReport.Build(folder, read, lookups, loadProblems)` takes the manifest read, every per-ID `AssetLookup`, and problems found after a lookup succeeded (a PNG Godot cannot decode). It lists `AssetManifest.Problems` first, whether or not the game uses those IDs (as `write_manifest.py --check` does), then each lookup's problem not already listed, so a duplicate the game asks for appears once. `LaunchReport.Failed` covers the `catch` in `Main._Ready`.
- At most `MaxShownProblems` (6) problems are shown, then `and N more; the full list is in the log under PDZ-ASSETS`. `AllLines` (nothing shortened) is logged as `PDZ-ASSETS report: <line>`.
- `LaunchReportPanel` is a `Node3D` in `Main.tscn` at (0, 1.6, -2.2), not under the camera: a dark unshaded board with a left-aligned, wrapping `Label3D` (1.6 m wide, 36 px at 1 mm per pixel). The board's height is fitted to the text with `Font.GetMultilineStringSize`. Pale blue for a notice, amber for problems. A left-aligned `Label3D` with a `width` puts the text's left edge at its origin, so the label sits 0.8 m left of the board's centre.
- Showing the panel is wrapped in its own `try`, so a failure there cannot stop OpenXR from starting.

- **Headset checks, 2026-10-14.** With the sky image deleted on the headset, a screen capture showed the board naming `e03.canopy: images/e03.canopy.png: missing`, the supplied floor image and the sky placeholder. With the floor image then truncated to 100 bytes, the log of the running app read `e03.floor: images/e03.floor.png: incomplete or changed: the file is 100 bytes, the manifest says 209` alongside the missing sky; the screen capture of that run showed only the headset's "Finding position in room" prompt, so the board itself was seen for the first check only. The headset folder was emptied afterwards.
- In debug builds the head-locked frame-rate readout covers the lower part of the board in the starting view. Recorded as deferred work.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens), 2026-10-14: high 0, medium 1, low 3, false 0, maybe-false 0. Four patched, one carried.

- medium, patched: the board's height was measured with word-only breaking while the label also breaks over-long words, so a long path overflowed the board. Now measured the label's way.
- low, patched: "cannot be read" and "unreadable image" named the absolute device path instead of the listed path.
- low, patched: a failure part-way through loading reported the folder as not found and dropped earlier problems.
- low, patched: the unsupported-format heading repeated its problem line.
- carried: the two headset criteria were unverified at review time. Since met: see the headset checks in Implementation Notes.

## Design Notes

- **Placement.** A panel a couple of metres ahead of the starting view, at eye height, reads like a sign in the world rather than a HUD. It does not follow the head.

## Verification

**Done (2026-10-14):**
- `tools/test.sh`: 65 Python and 109 C# tests pass, including 16 in `LaunchReportTests.cs` (one or more per matrix row).
- Desktop, Godot rendering frames to files (`--write-movie`, `--xr-mode off`): no folder shows the notice; floor truncated and canopy missing shows `2 problems with the assets` with both IDs, paths and "incomplete or changed" / "missing", both placeholders; canopy only missing shows the supplied floor image, the sky placeholder, and the report naming `e03.canopy: images/e03.canopy.png: missing`. The log carried every report line.
- Headset: own test images pushed with `push-assets.sh`, canopy deleted with `adb shell rm`, APK built and installed. The launch was held back by the system's "Finding position in room" tracking prompt (proximity sensor held closed), so **the report has not yet been seen on the headset**. The headset is left in the one-image-missing state with the final APK installed; the author puts it on, clears the prompt and starts the app. Then truncate with `adb shell truncate -s 100 .../files/images/e03.floor.png` for the second check.

**Commands:**
- `tools/test.sh` -- expected: all tests pass.
- Push both images, delete one with `adb shell rm`, relaunch, `adb exec-out screencap -p` -- expected: the report names the missing ID and path; one supplied image, one placeholder.
- Truncate a pushed image with `adb shell` (for example `truncate -s 100`), relaunch, capture -- expected: "incomplete or changed".

