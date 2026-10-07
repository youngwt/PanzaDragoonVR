---
title: 'Toolchain for building Godot C# apps for Quest 3'
type: 'chore'
ticket: '1'
created: '2026-10-07'
status: 'built'
baseline_revision: 'b4c6709f5341a0e81f94a56752cfda3184d49ffe'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nothing on the Steam Deck can build a Godot C# app for the Quest 3: there is no Godot, .NET SDK, JDK 17, Android SDK or `adb`.

**Approach:** One repeatable script installs the whole toolchain under the home directory, and a shared environment file tells later scripts where it is. This plan covers the toolchain only; the Godot project, the build command and the headset proof were split off and are recorded in `deferred-work.md`.

## Boundaries & Constraints

**Always:**
- Everything installs under `~/.local/opt`; nothing system-wide (SteamOS is read-only).
- Godot runs in self-contained mode so its settings and export templates sit beside the binary and work both inside the VS Code Flatpak sandbox and on the host.
- Downloads are verified against published checksums where the publisher provides them.
- Running the setup script accepts the Android SDK licence terms on the author's behalf; approving this plan is that consent.
- Nothing the toolchain installs lands in git.
- The script fails with a one-line message naming what went wrong.

**Never:**
- No Godot project, scene, export preset or build command (deferred).
- No change to Ghidra or its JDK 21 in `~/.local/opt`.
- No Android Studio, NDK or CMake.
- No edits to the user's shell profile.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh machine | `tools/setup-toolchain.sh`, nothing installed | Godot, .NET 9 SDK, JDK 17 and the Android SDK packages installed; prints each version | No error expected |
| Re-run | Same script, everything present | Skips each installed part, downloads nothing, exits 0 | No error expected |
| Interrupted download | A partial archive from an earlier run | The part is downloaded again and installed | Partial files are never treated as installed |
| Bad checksum | A download that does not match | Nothing from it is installed | Message names the file; exit non-zero |
| Sourcing the env file before setup | `. tools/toolchain-env.sh` with nothing installed | Variables are set; no error | The check command reports what is missing |

</frozen-after-approval>

## Code Map

- `tools/` -- existing Python extraction scripts; add the shell scripts here. Do not change the Python files.
- `~/.local/opt` -- holds Ghidra and `jdk-21.0.12.1+1` (Ghidra's; leave alone). `~/.local/bin` is on PATH.
- The shell runs inside a Flatpak sandbox; `flatpak-spawn --host <cmd>` reaches the real system. Home is shared, so files installed under it are visible to both.
- Godot 4.7.2-stable: `Godot_v4.7.2-stable_mono_linux_x86_64.zip` and `Godot_v4.7.2-stable_mono_export_templates.tpz` from `github.com/godotengine/godot/releases/download/4.7.2-stable/`, checksums in `SHA512-SUMS.txt` there. An empty `._sc_` file beside the binary enables self-contained mode; the `.tpz` is a zip whose `templates/` contents go in `editor_data/export_templates/4.7.2.stable.mono/`.
- .NET: Android export needs the .NET 9 SDK. `https://dot.net/v1/dotnet-install.sh --channel 9.0 --install-dir <dir>`; Godot finds `dotnet` on PATH and `DOTNET_ROOT`.
- JDK 17: Temurin tarball via `api.adoptium.net/v3/assets/latest/17/hotspot` (gives link and sha256), as was done for JDK 21.
- Android: command-line tools zip from `dl.google.com/android/repository/` (current file name listed on developer.android.com/studio), then `sdkmanager --sdk_root=<dir>` for `platform-tools`, `build-tools;36.1.0`, `platforms;android-36`. Godot reads `JAVA_HOME` and `ANDROID_HOME`.

## Tasks & Acceptance

**Execution:**
- [x] `tools/toolchain-env.sh` -- sets the install paths, `JAVA_HOME`, `ANDROID_HOME`, `DOTNET_ROOT`, a `GODOT` variable and PATH; safe to source -- one owner for paths, reused by the deferred build command.
- [x] `tools/setup-toolchain.sh` -- idempotent installer for the four parts, each with a done-marker written only after it is fully installed; `--check` prints the versions found and exits non-zero if any part is missing -- the repeatable setup.
- [x] `tools/README.md` -- what the script installs, where, how much space, the licence acceptance, and how to remove it -- the human-facing record.

**Acceptance Criteria:**
- Given a machine with none of the toolchain, when `tools/setup-toolchain.sh` finishes, then `tools/setup-toolchain.sh --check` exits 0 and prints Godot 4.7.2 (mono), a .NET 9 SDK, Java 17 and an `adb` version.
- Given the toolchain is installed, when Godot is started headless with `--version` from both the sandbox and the host, then both print `4.7.2.stable.mono`.
- Given the toolchain is installed, when the export templates folder is listed, then it contains the Android templates.
- Given the work is done, when `git status` is run, then only the three files in Tasks are new.

## Implementation Notes

- Installed under `~/.local/opt`: `godot-4.7.2-stable-mono` (2.1 GB, self-contained, with export templates), `dotnet-9` (SDK 9.0.318), `jdk-17` (Temurin 17.0.20.1), `android-sdk` (platform-tools 37.0.1, build-tools 36.1.0, platform android-36). About 3.5 GB.
- Each part is built in `~/.local/opt/.toolchain-setup-tmp` and moved into place with an `.installed` marker only when complete.
- `toolchain-env.sh` honours a `TOOLCHAIN_ROOT` override; it was used to test the bad-checksum and interrupted-download rows in a scratch root.
- Microsoft publishes no checksum for `dotnet-install.sh`; it is run as downloaded over HTTPS.
- Runtime caches land outside `~/.local/opt`: `~/.android`, `~/.dotnet`, `~/.nuget`.
- The machine already had a .NET 10.0.103 SDK in `~/.dotnet` and July leftovers of a Godot 4.7 run in `~/.config/godot` and `~/.local/share/godot`. Left alone; self-contained Godot ignores them.
- Matrix rows verified by running the script: fresh install, re-run, `--check` with nothing installed, bad checksum and interrupted download (the last two in a scratch root).
- Review patch also updated `stack.md`, whose "Not yet installed" section had become false.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 1, low 7, false 1, maybe-false 0. One rejected low.

| # | Finding | Verdict | Route | Evidence |
|---|---------|---------|-------|----------|
| 1 | `git status` shows untracked workflow files beyond the three in Tasks | false | rejected | The extra paths are this build's own plan, `deferred-work.md` and the pre-existing `_bmad/render/`; nothing the toolchain installed is in the tree. |
| 2 | `dotnet --version` obeys a `global.json` in the working directory, so `--check` can report .NET MISSING | medium | patch | Reviewer reproduced it with a `global.json` pinning 8.0.100. Both calls now run from `/`; re-verified. |
| 3 | Java check reads the first output line, which is "Picked up ..." when `JAVA_TOOL_OPTIONS` is set | low | patch | Reproduced. Now selects the version line; re-verified with the variable set. |
| 4 | An unmarked folder at an install path is deleted without warning; changing pinned Android packages rebuilds the SDK | low | patch | Real by design. Documented in the README; no guard added. |
| 5 | `TOOLCHAIN_ROOT` override is undocumented and not in the plan | low | patch | Real; it is the seam used for scratch-root tests. Documented in the README. |
| 6 | No lock: two concurrent runs destroy each other's scratch folder | low | rejected | Real, but two simultaneous installs are unlikely and the fix needs locking logic. |
| 7 | `--check` prints hard-coded Android package versions without testing the folders | low | patch | Real. `--check` now tests both folders. |
| 8 | A failed download prints two lines, against the one-line rule | low | patch | Real. `--show-error` removed. |
| 9 | README prerequisites omit `awk` and `find` | low | patch | Real. Added. |
| 10 | `stack.md` still lists the toolchain as not installed | low | patch | Real. Section replaced. |

## Design Notes

- Self-contained Godot matters here because the sandbox and the host disagree about where configuration lives; keeping editor data beside the binary gives both the same templates and settings.
- .NET 9 leaves support on 2026-11-10 and Godot plans .NET 10 templates for 4.8. Not acted on; noted in the README.
- Godot's docs list `build-tools;35.0.1` and `platforms;android-35`, but the 4.7.2 source targets 36. Install the 36 set.

## Verification

**Commands:**
- `tools/setup-toolchain.sh` -- expected: installs all four parts, exits 0.
- `tools/setup-toolchain.sh` again -- expected: downloads nothing, exits 0.
- `tools/setup-toolchain.sh --check` -- expected: four version lines, exit 0.
- `flatpak-spawn --host sh -c '. tools/toolchain-env.sh && "$GODOT" --headless --version'` -- expected: `4.7.2.stable.mono`.
- `git status --short` -- expected: only the three new files.
