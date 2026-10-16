---
title: 'Move the build off .NET 9 to .NET 10'
type: 'chore'
ticket: '4'
created: '2026-10-16'
status: 'built'
baseline_revision: 'f5a1e9d2a20a646e3e6a8e3e172c029971157aa8'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Everything the project builds with runs on .NET 9, which leaves support on 2026-11-10: the SDK the toolchain installs, the runtime inside the Quest APK and the unit tests. After that date it gets no security fixes.

**Approach:** Upgrade to the latest supported .NET, which is .NET 10 (LTS, supported to November 2028), as far as the engine allows, and record what is left on .NET 9 and when it moves. The author asked for the upgrade itself, not only the spike's answer (2026-10-16). The answer the spike asked for goes in `tools/README.md` and the stack notes.

## Boundaries & Constraints

**Always:**
- The epic's Done when checks still pass on the headset after the change: 90 Hz, own files loaded, a removed file reported, placeholders on an empty folder.
- `tools/setup-toolchain.sh` stays repeatable: a re-run downloads nothing, `--check` names what is missing, and downloads are verified wherever the publisher gives checksums.
- `tools/test.sh` passes, and so does `tools/build-quest.sh`.

**Never:**
- No game behaviour change.
- No change to Ghidra or its JDK 21.
- No .NET preview release.

**Decisions (author, 2026-10-16):**
- Option A. The toolchain installs the .NET 10 SDK in place of .NET 9. The unit tests and the desktop build target `net10.0`. The Android build keeps `net9.0`, as Godot 4.7.2's export templates require, and is built by the .NET 10 SDK. The APK moves to .NET 10 in a later change, with Godot 4.8 stable. If the .NET 10 SDK cannot build a `net9.0` Android export that runs on the headset, stop and ask the author.
- The work stays on the `engine-spike` branch.

</frozen-after-approval>

## Code Map

- `tools/setup-toolchain.sh` -- `DOTNET_CHANNEL="9.0"` (line 25), `install_dotnet()` (150-172), and the `--check` test for a `9.*` SDK (305-308). Godot 4.7.2, its SHA-512 sums, the export templates and the Meta vendor plugin 5.1.0 are pinned here too.
- `tools/toolchain-env.sh:17` -- `DOTNET_ROOT="$TOOLCHAIN_ROOT/dotnet-9"`; `GODOT_VERSION="4.7.2"`.
- `game/PanzaDragoonVR.csproj` -- `Godot.NET.Sdk/4.7.2`; `net8.0`, with `net9.0` on Android ("Godot 4.7's Android export requires .NET 9").
- `tests/GameLogic.Tests/GameLogic.Tests.csproj` -- `net9.0`; it compiles `game/Logic` directly and has no Godot reference.
- `tools/build-quest.sh` -- checks that the Android build template matches `${GODOT_VERSION}.stable.mono`.
- `tools/README.md` lines 24-25, 69, 76 and 89, and `spec-…/stack.md` lines 38-39 -- name `.NET 9` and `dotnet-9`.
- Installed now: `~/.local/opt/dotnet-9` (SDK 9.0.318). `~/.dotnet` holds a separate .NET 10.0.103 SDK from before the project, which the toolchain ignores.
- **Research, 2026-10-16:**
  - Godot 4.7.2's prebuilt Android export templates need the project to target exactly `net9.0`; a project set to `net10.0` fails to export ([godot#118989](https://github.com/godotengine/godot/issues/118989)).
  - The fix is [godot#123738](https://github.com/godotengine/godot/pull/123738), merged to master on 2026-09-26 for 4.8. It makes `net10.0` the minimum and moves Android to .NET 10. Nothing shows it backported to 4.7.x.
  - [Godot 4.8 beta 1](https://godotengine.org/article/dev-snapshot-godot-4-8-beta-1/) came out on 2026-10-15. Stable is expected in Q4 2026, so it may land after 2026-11-10.
  - Microsoft confirms .NET 9 ends on [2026-11-10](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/) and recommends .NET 10.
  - Whether the Meta vendor plugin 5.1.0 works with Godot 4.8 is not known.

## Tasks & Acceptance

**Execution:**
- [x] `tools/setup-toolchain.sh`, `tools/toolchain-env.sh` -- install the .NET 10 SDK (channel 10.0) into `dotnet-10` and point `DOTNET_ROOT` at it; `--check` expects a `10.*` SDK. Leave `~/.local/opt/dotnet-9` on disk, unused.
- [x] `game/PanzaDragoonVR.csproj` -- desktop target `net10.0`; Android stays `net9.0`, with the comment saying why and until when (Godot 4.8).
- [x] `tests/GameLogic.Tests/GameLogic.Tests.csproj` -- target `net10.0`; update the test packages only if they fail on .NET 10.
- [x] `tools/README.md`, `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md` -- the SDK row, paths and removal steps for `dotnet-10`; the old `dotnet-9` folder can be deleted; the spike's answer: the APK runs .NET 9 until Godot 4.8 (which makes `net10.0` the minimum, godot#123738) is stable.
- [x] `_bmad-output/initiative-panzer-dragoon-zwei-vr/deferred-work.md` -- one entry: move to Godot 4.8 stable and `net10.0` on Android, with the evidence above.

**Acceptance Criteria:**
- Given the toolchain set up afresh with `tools/setup-toolchain.sh`, when `--check` runs, then it reports a .NET 10 SDK from `dotnet-10` and exits 0, and a re-run downloads nothing.
- Given `tools/test.sh`, when it runs, then every Python and C# test passes, and the C# tests run on .NET 10.
- Given `tools/build-quest.sh`, when it runs with the headset connected, then it builds, installs and launches the APK, and the APK still holds no image files of the project's own.
- Given the app on the Quest 3, when it starts, then the readout's .NET line shows a 9.0 runtime, and the epic's Done when checks 1 to 4 pass.
- Given the game run on the desktop (`--xr-mode off`), when it starts, then the readout's .NET line shows a 10.0 runtime.

## Implementation Notes

- 2026-10-16: `setup-toolchain.sh` installed .NET SDK 10.0.402 into `~/.local/opt/dotnet-10`; a second run downloaded nothing and `--check` exited 0. `~/.local/opt/dotnet-9` is left on disk, unused.
- The test packages needed no update: 149 C# tests pass on `net10.0` (`.NETCoreApp,Version=v10.0`), and the 73 Python tests pass.
- The .NET 10 SDK builds the `net9.0` Android export: it restored `Microsoft.NETCore.App.Runtime.Mono.android-arm64` 9.0.21 (was 9.0.20 under SDK 9.0.318), and the APK's `System.Private.CoreLib.dll` matches that package byte for byte.
- The desktop build's runtimeconfig names `net10.0`, framework 10.0.0.
- README size for `dotnet-10`: measured 624 MB.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-16: high 0, medium 0, low 1, false 0, maybe-false 0. One patched.

- low, patched: on success `--check` printed only the SDK version, so it didn't show that the SDK came from `dotnet-10` rather than the old `dotnet-9` or the separate `~/.dotnet`. It now prints `SDK 10.0.402 in /home/deck/.local/opt/dotnet-10`. Re-run: exit 0, and `tools/test.sh` passes.

## Verification

**Commands:**
- `tools/setup-toolchain.sh` then `tools/setup-toolchain.sh --check` -- expected: the new SDK is reported, exit 0.
- `tools/test.sh` -- expected: all tests pass.
- `tools/build-quest.sh` -- expected: builds, installs and launches.

**Manual checks (if no CLI):**
- In the headset: the readout shows the .NET version the APK runs, and the epic's Done when checks 1 to 4 pass.

**Results, 2026-10-16:**
- Toolchain: SDK 10.0.402 reported from `dotnet-10`, exit 0; re-run printed "already installed, skipping" for every part.
- `tools/test.sh`: 73 Python and 149 C# tests pass, C# on net10.0.
- `tools/build-quest.sh`: exit 0; built, installed and launched on 2G0YC1ZF7L045Y. No image or model files under `assets/` in the APK (the `res/` PNGs are Android library notification drawables).
- Headset (adb screen capture and logcat): readout ".NET 9.0.21", "Target 90 Hz (4 rates offered)", "Measured 90.0 Hz" (check 1). With a pushed test folder: `supplied: e03.floor, e03.canopy, e03.flyer; placeholders: none` (check 2). With `images/e03.canopy.png` removed on the headset: `1 problem with the assets`, `e03.canopy: images/e03.canopy.png: missing` (check 3). With the folder emptied: `Running on placeholders` board and placeholders for all three IDs (check 4).
- Desktop (`--xr-mode off`, Movie Maker frame capture): readout ".NET 10.0.13".
