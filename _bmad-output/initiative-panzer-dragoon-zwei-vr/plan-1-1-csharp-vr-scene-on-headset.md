---
title: 'C# VR scene on the Quest 3, built and installed by one command'
type: 'feature'
ticket: ''
created: '2026-10-08'
status: 'built'
baseline_revision: '2bc34adec6aa80e7e3e963692a36fc38c6c439c8'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The toolchain is installed, but there is no Godot project and nobody knows whether C# on Android (experimental in Godot 4.7) runs on the Quest 3. Every later story depends on that answer. This is the second half of ticket 1.1.

**Approach:** Scaffold a Godot 4.7 C# project in `game/` and add one command that builds a debug APK and installs and launches it on a connected Quest 3. The scene is one cube rotated each frame by C# code, with a text label showing the .NET runtime version as proof that C# is running.

## Boundaries & Constraints

**Always:**
- Package name `io.github.youngwt.panzadragoonvr`; app name `Panzer Dragoon Zwei VR`.
- C# from the first line: all scene logic is C#, no GDScript.
- Tool paths come only from `tools/toolchain-env.sh`.
- Build output and Godot's generated folders stay out of git. The APK holds no project images or models.
- The build script fails with a one-line message naming what is missing and how to fix it.
- If the app crashes at launch, capture `adb logcat` before changing anything.

**Never:**
- No ground, sky, flying cube, asset loading or manifest (stories 1.3 to 1.7).
- No controller input and no 90 Hz tuning (story 1.3).
- No release signing; the debug keystore only.
- No changes to the toolchain installer.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Headset connected | `tools/build-quest.sh` | APK built, installed and launched on the headset | No error expected |
| No headset | `tools/build-quest.sh`, no device attached | APK still built in `game/build/`; install does not run | Message says to connect the headset and enable developer mode; exit non-zero |
| Headset not authorised | Device listed as `unauthorized` | APK built; install does not run | Message says to accept the USB debugging prompt in the headset; exit non-zero |
| No toolchain | `tools/setup-toolchain.sh --check` fails | Nothing built | Message says to run `tools/setup-toolchain.sh`; exit non-zero |
| Build only | `tools/build-quest.sh --no-install` | APK built; `adb` not used | No error expected |

</frozen-after-approval>

## Code Map

- `tools/toolchain-env.sh` -- source it for `GODOT`, `DOTNET_ROOT`, `JAVA_HOME`, `ANDROID_HOME`, PATH. Do not edit.
- `tools/setup-toolchain.sh` -- `--check` exits non-zero when a part is missing; reuse it for the no-toolchain row. Do not edit.
- `tools/README.md`, `.gitignore` -- extend; keep what is there.
- `game/` -- does not exist yet.
- Godot 4.7.2 is self-contained: editor settings and the debug keystore live in `$GODOT_DIR/editor_data`. It reads `JAVA_HOME` and `ANDROID_HOME` for the Android export.
- C# project: `Godot.NET.Sdk/4.7.2`; target `net8.0`, and `net9.0` when `GodotTargetPlatform` is `android` (the prebuilt Android template accepts only `net9.0`).
- Project settings: `xr/openxr/enabled=true`, `xr/shaders/enabled=true`, renderer `gl_compatibility` for desktop and mobile, `textures/vram_compression/import_etc2_astc=true`.
- Export preset keys: `xr_features/xr_mode=1`, `architectures/arm64-v8a=true` (others false), `screen/immersive_mode=true`, `gradle_build/use_gradle_build=false`, `package/unique_name`, `package/name`.
- Headless: `"$GODOT" --headless --path game --import`, then `--build-solutions --quit`, then `--export-debug "<preset>" build/<name>.apk`.
- The shell runs in a Flatpak sandbox with no USB access. `adb` must run on the host: through `flatpak-spawn --host` when that command exists, directly otherwise.

## Tasks & Acceptance

**Execution:**
- [x] `game/project.godot`, `game/PanzaDragoonVR.csproj`, `game/PanzaDragoonVR.sln` -- Godot 4.7 C# project with OpenXR enabled and the Compatibility renderer -- the scaffold later stories build on.
- [x] `game/Main.tscn`, `game/Main.cs` -- XR origin and camera, one cube about a metre ahead at eye height, a `Label3D` beside it; C# starts OpenXR, rotates the cube in `_Process` and writes `System.Environment.Version` to the label -- the proof.
- [x] `game/export_presets.cfg` -- one Android preset named `Quest 3` -- the export target.
- [x] `tools/build-quest.sh` -- checks the toolchain, imports, builds the solution, exports the debug APK, then installs and launches through host `adb`; `--no-install` stops after the export -- the one command.
- [x] `.gitignore` -- add `/game/.godot/`, `/game/build/`, `/game/android/`, `*.apk` -- keep generated files out of git.
- [x] `tools/README.md` -- how to enable developer mode, authorise USB debugging and run the build -- the human steps.

**Acceptance Criteria:**
- Given a Quest 3 in developer mode, connected and authorised, when `tools/build-quest.sh` finishes, then the app is running in the headset showing a rotating cube in stereo that stays fixed in space as the head moves.
- Given the app is running, when the author reads the label, then it shows a .NET version number.
- Given the built APK, when its file list is inspected, then it contains the project's C# assembly and no project images or models.
- Given the work is done, when `git status` is run, then nothing under `game/.godot`, `game/build` or any APK appears.

## Implementation Notes

- **Switched to Godot's Gradle build, without the vendors plugin.** The prebuilt-template export (`use_gradle_build=false`) was built first and inspected: its manifest had no `org.khronos.openxr.intent.category.IMMERSIVE_HMD` category and no headtracking feature, the APK had no `libopenxr_loader.so` (which `libgodot_android.so` loads at run time), and the Godot binary carries the message `"Use Gradle Build" must be enabled when xr_mode is set to "OpenXR"`. That APK could not have started as an immersive app, so the switch was made on that evidence, before any on-headset run. With `use_gradle_build=true` and no plugin the APK has the loader (Khronos 1.1.53), the `IMMERSIVE_HMD` category and the headtracking feature. The plugin half of the fallback (5.1.0, `enable_meta_plugin`, `quest_3_support`) was not applied; it is the next step if the headset still shows the app as a flat panel.
- `org.gradle.daemon=false` is passed through `GRADLE_OPTS` in `tools/build-quest.sh`, because `game/android/` is generated and git-ignored. The script passes `--install-android-build-template` when `game/android/.build_version` is not `4.7.2.stable.mono`.
- Gradle caches its distribution and libraries in `~/.gradle`.
- Godot generated `game/Main.cs.uid` and `game/openxr_action_map.tres` (the default action map, unused). Both are meant to be committed; neither is in the Tasks list.
- A `DirectionalLight3D` was added so the cube's rotation is visible. No project icon is set, so Godot logs "No project icon specified" and uses its default.
- First on-headset attempt (Quest 3 `2G0YC1ZF7L045Y`, asleep): install succeeded; Horizon OS intercepted the launch with its "controller required" dialog (`LaunchCheckControllerRequiredDialogActivity`), so the app process had not started. No crash; nothing in logcat from Godot or .NET yet. The on-headset acceptance criteria are still unconfirmed.

- After review: the label sits centred 0.4 m above the cube (0, 1.9, -1) so it cannot intersect it. `tools/build-quest.sh` exits 2 when the app has not started within 10 seconds, clears logcat before launching, and when the app starts and then dies within 5 seconds saves the log to `game/build/logs/logcat-crash.log` and exits 1.

## Plan Change Log

- 2026-10-08: Code Map and Design Notes say `gradle_build/use_gradle_build=false` with the prebuilt template. Implemented `true`; Godot 4.7.2 cannot export OpenXR without the Gradle build. See Implementation Notes. The frozen block is unchanged.

## Review Triage Log

Pass 1 (quick lens): high 0, medium 1, low 5, false 2, maybe-false 0. One rejected low.

| # | Finding | Verdict | Route | Evidence |
|---|---------|---------|-------|----------|
| 1 | Acceptance criteria 1 and 2 unmet: no evidence the app ever ran on the headset | false | rejected | After the reviewer's evidence was gathered, the author put the headset on and reported the orange cube and ".NET 9"; the device log shows the app's OpenXR session reaching `XR_SESSION_STATE_FOCUSED`. |
| 2 | Script exits 0 when the app has not started | low | patch | Real; reproduced with the headset behind its controller prompt. Now exits 2; statuses documented. Re-verified: exit 2. |
| 3 | A start-up crash is reported as running and no logcat is captured | medium | patch | Real by reading the wait loop. Now clears logcat before launch, re-checks the process after 5 seconds and saves `logcat-crash.log` on death. Not exercised live: the launch was held by the controller prompt on every scripted run. |
| 4 | Godot's default `openxr_action_map.tres` ships, against "no controller input" | false | rejected | No code reads input. Godot generates the file on import and would recreate it, so it is committed. |
| 5 | Label overlaps the cube | low | patch | Plausible from the coordinates. Label moved above the cube. Not yet seen in the headset after the move. |
| 6 | Device states other than `device`/`unauthorized` get the wrong advice; any Android device is accepted | low | rejected | Real, but unlikely here and the fix adds branches. |
| 7 | README crash snippet writes `logcat.txt` to the current folder and uses the sandbox's `adb` | low | patch | Real. Now uses host `adb` and `game/build/logs/`. |
| 8 | Stale comment in the `.csproj` about the prebuilt template | low | patch | Real. Corrected. |

Matrix rows verified by running the script: build only (exit 0); no toolchain (exit 1); no headset and not authorised (exit 1 each, using a stand-in `adb`); headset connected (built and installed; launch confirmed by the author in the headset).

## Design Notes

- **No vendor plugin and no Gradle build to start.** The OpenXR vendors plugin is optional since Godot 4.6, and skipping it allows the prebuilt export template. If the app will not start as an immersive app, fall back to plugin 5.1.0 with Gradle build (`--install-android-build-template`, `xr_features/enable_meta_plugin=true`, `meta_xr_features/quest_3_support=true`, and `org.gradle.daemon=false` to stop headless export hanging). Record the switch in Implementation Notes.
- **Renderer: Compatibility**, as Meta's samples use; story 1.3 may revisit.
- **Known risk:** Godot issues #99021 and #115678 report C# startup crashes on Android.
- Build and verify up to the APK without a device; the author confirms the on-headset criteria afterwards.

## Verification

**Commands:**
- `tools/build-quest.sh --no-install` -- expected: `game/build/*.apk` exists, exit 0.
- `tools/build-quest.sh` with no headset -- expected: APK built, connect message, exit non-zero.
- `unzip -l game/build/*.apk` -- expected: the project's C# assembly present; no project images or models.
- `git status --short` -- expected: only the files listed in Tasks.

**Manual checks (if no CLI):**
- In the headset: the cube rotates, holds its place as the head moves, and the label shows a .NET version.
