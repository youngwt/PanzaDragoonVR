---
title: 'Test scene at 90 Hz'
type: 'feature'
ticket: '3'
created: '2026-10-08'
status: 'built'
baseline_revision: '3d75c71394063689e4eaf884b6c3a5ce707dd708'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/tools/README.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The headset shows one cube in a grey void, at whatever refresh rate the Quest picks. CAP-1 asks for a scene the player can look all around, holding a steady 90 Hz, and nothing yet measures frame rate.

**Approach:** Replace the spike scene with a ground, a sky and a grey cube flying around the player, ask the headset for 90 Hz, and add a frame-rate readout the player can see while looking around, with the same numbers written to the device log.

## Boundaries & Constraints

**Always:**
- The ground and sky are the built-in placeholders for `e03.floor` and `e03.canopy`: the author's own work, generated in code, with no image files.
- All logic is C#. Logic that needs no engine types lives where it can be unit-tested off the headset.
- The readout is a developer aid: shown in debug builds only.
- The scene still runs on a desktop without a headset, saying it is not in VR.

**Never:**
- No loading from the asset folder and no manifest (stories 1.4 and 1.5).
- No controller input, no scrolling floor, no dragon.
- No Meta vendor plugin unless 90 Hz cannot be requested without it; if so, stop and report rather than add it.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| 90 Hz offered | Headset lists 90 among its refresh rates | Display and physics set to 90; readout shows target 90 and measured rate | No error expected |
| 90 Hz not offered | List lacks 90 | Rate left as it is; readout names the rate in use and says 90 is not offered | Logged as a warning |
| No rate list | Runtime returns an empty list | Rate left as it is; readout says the rate could not be set | Logged as a warning |
| Not in VR | OpenXR not initialised | Scene shown flat; readout says not in VR | Logged as a warning |
| Slow frames | Frames longer than 1.5 times the target interval | Counted; readout and log show the count for the last second and since start | No error expected |

## Renegotiated by the author, 2026-10-08

After seeing the scene in the headset: it ran at 72 Hz and the readout said 90 Hz could not be set. The author accepted this for now ("will have to do for now"). The 90 Hz target and its acceptance criterion are not met by this story and carry forward as deferred work.

## Renegotiated by the author, 2026-10-09

The Meta OpenXR vendor plugin may be added. Without it the headset offered no refresh rates and the app's manifest did not declare Quest 3 support; the author approved adding the plugin to pursue 90 Hz and to remove the controller prompt at launch. Later the same day, with the plugin in and the display still at 72 Hz while worn, the author's reading was accepted as the result for this story.

</frozen-after-approval>

## Code Map

- `game/Main.tscn` -- XR origin and camera, light, orange cube, `RuntimeLabel`. Rework it; keep the origin, camera and light.
- `game/Main.cs` -- `StartOpenXr()` sets `UseXR` and turns vsync off; keep that, extend it with the refresh rate. The label text moves into the readout.
- `game/project.godot` -- Compatibility renderer, OpenXR on. `game/PanzaDragoonVR.csproj` -- `Godot.NET.Sdk/4.7.2`, `net8.0`, `net9.0` on Android.
- `tools/build-quest.sh` -- builds, installs, launches; exit 2 means installed but held behind the headset's controller prompt. Do not change it.
- `tools/toolchain-env.sh` -- puts `dotnet` on PATH. No test project exists yet.
- Godot's `OpenXRInterface` has `GetAvailableDisplayRefreshRates()` and a `DisplayRefreshRate` property, backed by an extension Godot ships without the vendor plugin. Set `Engine.PhysicsTicksPerSecond` to match.
- `adb` runs on the host: `flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb"`.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/RefreshRate.cs`, `game/Logic/FrameStats.cs` -- engine-free C#: choose a rate from the offered list; count frames, slow frames and per-second averages -- the testable core of the matrix.
- [x] `tests/GameLogic.Tests/` -- an xUnit project that compiles `game/Logic/*.cs` directly, with one test per matrix row it can cover -- first unit tests in the repo.
- [x] `tools/test.sh` -- runs `dotnet test` with the toolchain environment -- one command for tests.
- [x] `game/PlaceholderTextures.cs` -- generates the ground and sky placeholder textures in code -- no image files.
- [x] `game/Main.tscn`, `game/Main.cs` -- large ground plane at floor level, sky overhead, grey cube; request 90 Hz through `RefreshRate` -- the CAP-1 scene.
- [x] `game/FlyingCube.cs` -- moves the grey cube on a closed path all the way around the player, so following it means turning right round.
- [x] `game/FrameRateReadout.cs` -- a label fixed in front of the camera showing target and measured rate and slow-frame counts, plus the .NET version; writes one line a second to the log with the tag `PDZ-FPS`; debug builds only.
- [x] `.gitignore`, `tools/README.md` -- ignore test build output; document `tools/test.sh` and how to read the frame-rate log.

**Acceptance Criteria:**
- Given the app running on the Quest 3, when the player turns all the way round, then ground, sky and the flying cube are visible and the readout stays readable.
- Given the app running for one minute while the player looks around, when the `PDZ-FPS` log lines are read, then the target is 90, every per-second average is at least 89, and slow frames since start are under 1% of frames.
- Given `tools/test.sh`, when it runs, then every test passes.
- Given the built APK, when its file list is inspected, then it holds no image files of the project's own.

## Implementation Notes

- **Rate requested when the session begins.** `Main` asks for 90 Hz on OpenXR's `session_begun` signal, as Godot's XR start-up guidance does, or at once if the session has already begun. Until then the readout says `Waiting for the headset`. Frame counting starts at that point, so "since start" means since the session began.
- **Frame time from the clock.** The readout times frames with `Time.GetTicksUsec()`, not the engine's `delta`, which the engine may clamp.
- **A real log tag.** Godot's own print reaches logcat under the tag `godot`, which `adb logcat -s PDZ-FPS` would not show. On Android the readout calls `__android_log_write` in the system's `liblog` directly; if that call cannot be made it warns once and falls back to Godot's print with a `PDZ-FPS` prefix. On a desktop it uses Godot's print.
- **Target when 90 is not set.** Slow frames are then measured against the rate in use (the screen's rate when not in VR), so the counts stay meaningful.
- **Readout text is in `Logic`.** The summary line and the log line are built by `RefreshRateChoice` and `FrameReport`, so the tests cover the words in every matrix row.
- **Verified off the headset:** `tools/test.sh` (25 tests pass); a desktop run with `--xr-mode off` shows the scene and the `Not in VR` readout and writes `PDZ-FPS` lines; `tools/build-quest.sh` built and installed, exit 2 (headset waiting for a controller); the APK holds no image files of the project's own (only Godot's launcher icons and AndroidX's notification drawables).
- **Not yet verified:** everything that needs the headset worn: the two headset acceptance criteria, including that the `PDZ-FPS` tag reaches logcat.

### 2026-10-09: second day

- **Vendor plugin added** (author approved). godot_openxr_vendors 5.1.0 is downloaded by `tools/setup-toolchain.sh` into `game/addons/godotopenxrvendors/` (48 MB, git-ignored). The preset enables the Meta plugin, Quest 3 support and optional hand tracking. Result on the headset: no controller prompt at launch, four refresh rates offered, the request for 90 accepted.
- **Still 72 Hz.** The display stays at 72 worn or not. The headset's own shell also asks for 90 and stays at 72, and the kernel log only ever sets `fps=72`. Battery saver is off. Cause unknown; the author accepted 72 Hz for now.
- **Sky fixed.** On the Quest 3 a repeating texture on these planes came out clamped to its edge (one flat colour). Colouring the sky by its raw coordinates showed a correct grid, and sampling with hand-wrapped coordinates showed the pattern. Both planes now use a small shader that wraps coordinates itself with `textureGrad`. Verified on the headset by screen capture. Why the stock material fails on the device and not on the desktop is not known.
- **Self-service device testing.** With the prompt gone, `adb shell am broadcast -a com.oculus.vrpowermanager.prox_close` keeps the headset awake, `tools/build-quest.sh` launches the app, and `adb exec-out screencap -p` captures the view (`automation_disable` restores the sensor). A Guardian boundary prompt can still block a launch.
- **Logs.** The headset deletes an app's log lines soon after the app exits; read `adb logcat -s PDZ-FPS` while it runs. The `PDZ-FPS` tag works.
- **Readout.** It now reports the rate in use, not the rate asked for ("Asked for 90 Hz; running at 72 Hz"), and stops counting while the headset is not showing the app (the log had shown `avg=1631.1` during a session restart).
- **Build folder.** `game/build/.gdignore` stops Godot importing files saved there; diagnostic screenshots had been packed into the APK.
- Godot 4.7.2 segfaults on the first headless import with the plugin unless `game/.godot/extension_list.cfg` already names it; the setup script writes that line.
- 32 unit tests pass. The last round (read-back of the rate, pause and resume) is **not yet verified on the headset**: a Guardian prompt blocked the launch.

## Plan Change Log

- 2026-10-09: Code Map said the refresh-rate extension works without the vendor plugin and that `tools/build-quest.sh` must not change. Both superseded: the plugin was needed for the headset to offer any rates (author approved), and the script's messages were reworded. Known-bad state avoided: an app the headset runs in original-Quest compatibility mode. KEEP: the engine-free `Logic/` classes and their tests, the placeholder generator, the readout's log format.

## Review Triage Log

Pass 1 (quick lens, 2026-10-08) is not in this file; its table is in commit dfdd387. Its six patches were applied on 2026-10-09 before pass 2.

Pass 2 (quick lens), 2026-10-09, after the pass 1 patches, the plugin and the sky fix: high 0, medium 2, low 8, false 0, maybe-false 0. Two rejected.

| # | Finding | Verdict | Route | Evidence |
|---|---------|---------|-------|----------|
| 1 | Acceptance criterion 2 still unmet (72 Hz) | medium | defer | carried: the author accepted 72 Hz. New deferred-work entry with the current evidence. |
| 2 | "90 requested" treated as "90 in use": wrong target, physics ticks and slow-frame threshold | medium | patch | Real; the log showed `target=90` at a measured 72. Now reads the rate back and reports the rate in use. Unit-tested; not yet seen on the headset. |
| 3 | Diagnostic screenshots packed into the APK | low | patch | Real; caused by captures saved in `game/build/logs/`. Captures removed, `game/build/.gdignore` added, APK rebuilt and checked. |
| 4 | `_rateSet` unused; comment disagrees with code | low | patch | Real. Field removed, comment corrected. |
| 5 | README contradicts messages in `tools/build-quest.sh` | low | patch | Real. Script wording updated; no behaviour change. |
| 6 | Plugin removal steps leave dangling references | low | patch | Plausible. README now says what else to remove. |
| 7 | `Pause()`/`Resume()` can overwrite the waiting text before measuring starts | low | rejected | Needs a signal order that was not observed; the fix would guard an undemonstrated state. |
| 8 | Static Godot `Shader` held for the assembly's life | low | patch | Real in the editor only. Now owned by the scene. |
| 9 | The plan's unfrozen sections are stale | low | rejected | A finding whose fix is to edit this plan. Notes and change log were updated at finalisation. |
| 10 | The 90 Hz deferred-work entry is out of date | low | patch | Real. A new entry was appended. |

## Design Notes

- **Sky as a plane overhead.** The canopy is a flat tiling image, so the placeholder is a large plane high above the player with a plain background colour behind it. Story 1.4 can then put an image on it without reshaping the scene.
- **Readout fixed to the head.** It must stay visible while turning, so it is a child of the camera. The spec's no-HUD rule covers the game's information; this is a debug aid that release builds do not show.
- **Unit tests off the headset.** The test project compiles the `Logic` sources itself and does not reference Godot, so `dotnet test` runs on the PC in seconds.
- The headset check needs the author: the Quest holds a launch until a controller is picked up.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all tests pass.
- `tools/build-quest.sh` -- expected: exit 0, or exit 2 while the headset waits for a controller.
- `adb logcat -d -s PDZ-FPS` after a minute in the headset -- expected: target 90, averages at least 89, slow frames under 1%.
- `git status --short` -- expected: no build or test output.

**Manual checks (if no CLI):**
- In the headset: ground underfoot, sky overhead, the grey cube circling right around, the readout showing about 90.
