---
title: 'The game cannot ask Android for its files folder on the Quest 3'
type: 'bugfix'
ticket: '8'
created: '2026-10-18'
status: done
baseline_revision: '53e6e5ef652acac05b5a2fafc672a5b4ac4b53cf'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/backlog/bug-android-folder-lookup-returns-nothing-on-quest-3.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On the Quest 3 every launch logs `Android gave no external files folder; using /storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files`. The game asks Android for the folder through Godot's `AndroidRuntime` singleton (`getApplicationContext().getExternalFilesDir(null).getAbsolutePath()`) and gets nothing back. The fixed path happens to be right, but the extraction epic writes to that folder and should use the route Android actually gives, or know for certain that the fixed path is the route.

**Approach:** Find which step of the request returns nothing, with a logged probe on the headset, and fix the call if Godot's Android bridge can make it. If it cannot, record the cause with evidence, and the author chooses whether the fixed path stays the route.

**Reproduction:**
1. Build and launch on the Quest 3 with `tools/build-quest.sh`.
2. Read `adb logcat -d | grep PDZ-ASSETS` while the app runs.
3. Actual: the "Android gave no external files folder" line, then the fixed path. Expected: the folder Android names, and no fallback line.
Seen on every headset launch from 2026-10-14 to 2026-10-18.

**Cause hypothesis:** the call returns a value C# cannot convert, or the bridge cannot match `getExternalFilesDir` when its argument is a null Variant (the method takes a `String`), rather than throwing. Not a fix: where it fails is the first thing to find.

## Boundaries & Constraints

**Always:**
- The game keeps finding the folder on every path: if the request fails, it falls back to the fixed path as today, with one `PDZ-ASSETS` line saying why.
- The probe's per-step lines are temporary diagnostics; what stays is one line naming the folder and how it was found.
- Headset state is put back after testing (proximity sensor), and the author's disc image in `files/disc/` is left in place.

**Never:**
- No change to the fixed path itself, to the folder layout or to the asset contract.
- No Godot or vendor plugin version change.

## Acceptance Criteria

1. **The folder comes from Android**
   **Given** the app launched on the Quest 3
   **When** the `PDZ-ASSETS` log is read while it runs
   **Then** it names the folder Android returned, with no "Android gave no external files folder" line, and extraction and loading use that folder.
2. **Tests cover the condition found and fixed**
   **Given** `tools/test.sh`
   **When** it runs
   **Then** every test passes, and any engine-free part of the fix (choosing between Android's answer and the fixed path, and the log line) has a test.
3. **Or: the fixed path is the route, with proof**
   **Given** the probe's per-step log from the headset
   **When** it shows the bridge cannot return the folder
   **Then** the cause and evidence are recorded in this plan and `tools/README.md`, the author has chosen to keep the fixed path, and the fallback line is reworded to say the fixed path is the route — this supersedes 1.

</frozen-after-approval>

## Code Map

- `game/AssetFolder.cs` -- `Locate()` (public): non-Android reads `PDZ_ASSETS`; on Android calls `AskAndroid()` and falls back to `FixedAndroidPath` with a log line, or on an exception logs its message. `AskAndroid()`: `Engine.HasSingleton("AndroidRuntime")`, `GetSingleton`, `runtime.Call("getApplicationContext").AsGodotObject()`, `context.Call("getExternalFilesDir", new Variant()).AsGodotObject()`, `folder.Call("getAbsolutePath").AsString()`; any null step returns null silently.
- `game/DiscExtraction.cs`, `game/Main.cs` `LoadAssets()` -- use the folder `Locate()` returns.
- Godot 4.7.2's Android bridge: the `AndroidRuntime` singleton and Java objects as `JavaObject`, callable from C# through `GodotObject.Call`; `JavaClassWrapper` wraps a Java class by name. Read the 4.7.2 class reference or source before choosing a call shape.
- Device checks: `tools/build-quest.sh`; host `adb` through `tools/quest-adb.sh`; `adb shell am broadcast -a com.oculus.vrpowermanager.prox_close` keeps the headset awake (`automation_disable` restores it); read logcat while the app runs. A Guardian prompt can block a launch.

## Tasks & Acceptance

**Execution:**
- [x] `game/AssetFolder.cs` -- a probe logging each step's result and type (singleton present, context, folder object, path), built and run on the headset.
- [x] `game/AssetFolder.cs` -- the fix the probe points to (for example a typed argument, or `JavaClassWrapper`), or the reworded fallback if none works; remove the per-step probe lines.
- [x] `tests/GameLogic.Tests/` or `game/Logic/` -- tests for any engine-free part.
- [x] `tools/README.md` -- how the game finds its folder now, or why the fixed path is the route.

## Implementation Notes

Done in the main conversation, with the author clearing a Guardian prompt.

- **Probe, 2026-10-18,** on the Quest 3: `AndroidRuntime` present (a `JNISingleton`); `getApplicationContext` gives a `JavaObject` of `android.app.Application`; `getActivity` gives `com.godot.game.GodotApp`. `getExternalFilesDir` with a nil Variant returns `Nil`, and `JavaClassWrapper.GetException()` is null, so no Java exception was thrown. With an empty string it returns a `java.io.File` whose `getAbsolutePath` is `/storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files`, the same folder as the fixed path. `getExternalFilesDirs(nil)` is also `Nil`. Godot's own data folder is internal storage (`/data/data/.../files`), not this one.
- **Cause, confirmed:** Godot's Java bridge does not match a nil Variant to `getExternalFilesDir(String)` and returns nothing silently. The hypothesis held.
- **Fix:** `AskAndroid` passes `""` (Android treats an empty type as the folder itself), checks `JavaClassWrapper.GetException()` after the call, and `Locate` logs `asset folder from Android: <path>`. Probe lines removed. The fallback to the fixed path is unchanged.
- **Headset check after the fix:** the log reads `asset folder from Android: /storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files`, with no fallback line, and the launch extracted 420 textures from 9 files into that folder with the manifest (0.3 s). Proximity sensor restored.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-18: high 0, medium 1, low 2, false 0, maybe-false 0. All three patched.

- medium, patched: criterion 2 names the choice between Android's answer and the fixed path, and the log line, as engine-free parts to test; the first version called them inapplicable. The choice and its three lines now live in `game/Logic/AndroidFolderChoice.cs`, with `tests/GameLogic.Tests/AndroidFolderChoiceTests.cs` (Android's answer, no answer as null or empty, a failure with its reason).
- low, patched: `tools/README.md` said the bridge cannot match the parameter "to an empty value"; it is a null value it cannot match. Corrected.
- low, patched: only `getExternalFilesDir` was checked for a Java exception. `getApplicationContext` and `getAbsolutePath` now are too, through one helper, and a nameless exception reads "an exception".

After the patches `tools/test.sh` passes (97 Python, 199 GameLogic, 98 Decoding). A second headset launch to recheck the refactored code was held back by the headset (the app did not start); the unit tests cover the choice, and the bridge call itself is unchanged from the version seen working on the headset.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `tools/build-quest.sh`, then `adb logcat -d | grep PDZ-ASSETS` while running -- expected: as acceptance criterion 1 or 3.
