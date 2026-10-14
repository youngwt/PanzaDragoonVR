# tools

## Build toolchain for Quest 3

`setup-toolchain.sh` installs everything needed to build a Godot C# app for the Quest 3. It is safe to run again: parts already installed are skipped and nothing is downloaded.

```sh
tools/setup-toolchain.sh           # install whatever is missing
tools/setup-toolchain.sh --check   # print the versions found; exits non-zero if a part is missing
```

It needs `bash`, `curl`, `unzip`, `tar`, `jq`, `awk`, `find`, `grep`, `sha256sum` and `sha512sum`, all of which SteamOS and the VS Code Flatpak sandbox already have.

### Licence acceptance

**Running `setup-toolchain.sh` accepts the Android SDK licence terms on your behalf.** It answers "yes" to every licence `sdkmanager --licenses` offers. Read them at <https://developer.android.com/studio/terms> before running it if you have not agreed to them.

### What is installed, and where

Everything except the OpenXR vendors plugin goes under `~/.local/opt`; set the `TOOLCHAIN_ROOT` environment variable before running the script or sourcing `tools/toolchain-env.sh` to use another folder (the default is `~/.local/opt`). Nothing is installed system-wide and nothing is added to your shell profile. The one part that lands inside this repository's folder, the OpenXR vendors plugin, is git-ignored.

| Part | Folder in `~/.local/opt` | Size | Source | Verified against |
|------|--------------------------|------|--------|------------------|
| Godot 4.7.2-stable, .NET build, with export templates | `godot-4.7.2-stable-mono` | 2.1 GB | Godot's GitHub release | the release's `SHA512-SUMS.txt` |
| .NET 9 SDK | `dotnet-9` | 580 MB | Microsoft's `dotnet-install.sh`, channel 9.0 | no published checksum for the install script |
| Temurin JDK 17 | `jdk-17` | 320 MB | Adoptium API, latest 17 | the SHA-256 the API gives |
| Android SDK: command-line tools, `platform-tools` (`adb`), `build-tools;36.1.0`, `platforms;android-36` | `android-sdk` | 490 MB | Google | the SHA-256 on developer.android.com for the command-line tools; `sdkmanager` checks the packages it fetches |

The fifth part goes into the project, because Godot only loads a plugin from the project's own `addons` folder:

| Part | Folder | Size | Source | Verified against |
|------|--------|------|--------|------------------|
| Godot OpenXR vendors plugin 5.1.0-stable | `game/addons/godotopenxrvendors` | 48 MB to download, 82 MB unpacked | the GodotVR GitHub release | a SHA-256 pinned in `setup-toolchain.sh` |

The plugin is what tells the headset that the app is built for the Quest 3. Without it the app's manifest named no supported device, the headset offered the app no refresh rates, and it would not start the app until a controller was picked up. With it the manifest lists the Quest 3 and declares hand tracking as optional; the headset now offers four refresh rates and starts the app with no controller prompt. Only the plugin's Meta part is switched on, in `game/export_presets.cfg`.

**The plugin is not committed.** It is too large for the repository, so `game/addons/godotopenxrvendors/` is git-ignored and every checkout gets it by running `setup-toolchain.sh`. `build-quest.sh` stops with a pointer to that script when the plugin is absent.

Setup also adds the plugin to Godot's list of extensions, `game/.godot/extension_list.cfg`. Godot 4.7.2 crashes in a headless import when it meets an extension that list does not name yet. `game/.godot/` is generated, so after deleting it run `setup-toolchain.sh` again before building; `--check` reports the plugin as missing until then.

About 3.6 GB in all. Setup also needs roughly 3 GB of temporary space in `~/.local/opt/.toolchain-setup-tmp`, which it removes when it finishes or fails.

Ghidra and its JDK 21 in `~/.local/opt` are not touched.

Godot runs in self-contained mode (an empty `._sc_` file beside the binary), so its editor settings and export templates live in `godot-4.7.2-stable-mono/editor_data` and are the same whether Godot is started from the VS Code sandbox or from the host. It does not use any settings in `~/.config/godot` or `~/.local/share/godot`.

Two tools keep small caches outside `~/.local/opt` when they run: `sdkmanager` and `adb` use `~/.android`, and `dotnet` uses `~/.dotnet` and `~/.nuget`.

### Using it

Source the environment file to get the paths. It only sets variables, so it works before setup has run too.

```sh
. tools/toolchain-env.sh
"$GODOT" --headless --version    # 4.7.2.stable.mono...
dotnet --version
java -version
adb version
```

It sets `GODOT`, `GODOT_DIR`, `GODOT_EXPORT_TEMPLATES`, `DOTNET_ROOT`, `JAVA_HOME`, `ANDROID_HOME` and `TOOLCHAIN_ROOT`, and puts `dotnet`, `java`, `sdkmanager` and `adb` first on `PATH`. It also turns off .NET CLI telemetry.

### How a part counts as installed

Each part is built in the temporary folder and moved into place only when complete, with an `.installed` file inside it. The plugin's file records its version and checksum, so changing either in `setup-toolchain.sh` replaces the plugin on the next run. A folder without that file is treated as not installed and is rebuilt from a fresh download; partial downloads are never reused. A download that fails its checksum stops the script with a message naming the file, and nothing from it is installed.

Two consequences to be aware of:

- An existing folder at one of the five install paths (`godot-4.7.2-stable-mono`, `dotnet-9`, `jdk-17`, `android-sdk` and `game/addons/godotopenxrvendors`) that has no `.installed` file is **deleted** and rebuilt, whatever it contains.
- The `android-sdk` marker records the pinned command-line tools and package list. Changing either in `setup-toolchain.sh` rebuilds `android-sdk` from scratch, which drops any packages you added by hand with `sdkmanager`.

### Removing it

```sh
rm -rf ~/.local/opt/godot-4.7.2-stable-mono \
       ~/.local/opt/dotnet-9 \
       ~/.local/opt/jdk-17 \
       ~/.local/opt/android-sdk \
       ~/.local/opt/.toolchain-setup-tmp
rm -rf game/addons/godotopenxrvendors
```

Remove one folder and run the script again to reinstall just that part. `~/.android` can be deleted too if nothing else on the machine uses it.

The plugin is needed to build this project, so only remove it for good if the project is to stop using it. Deleting its folder leaves two things behind that still name it: the line `res://addons/godotopenxrvendors/plugin.gdextension` in `game/.godot/extension_list.cfg` (delete that line, or all of the generated `game/.godot` folder), and the options `xr_features/enable_meta_plugin`, `meta_xr_features/quest_3_support` and `meta_xr_features/hand_tracking` in `game/export_presets.cfg` (delete those three lines).

### Notes

- .NET 9 leaves support on 2026-11-10, and Godot plans .NET 10 export templates for 4.8. Godot 4.7.2's Android export needs the .NET 9 SDK, so 9 is what is installed.
- Godot's documentation lists `build-tools;35.0.1` and `platforms;android-35`, but the 4.7.2 source targets 36, so the 36 set is installed.
- Android Studio, the NDK and CMake are not installed.
- The command-line tools file name and checksum are pinned in `setup-toolchain.sh`; update both from <https://developer.android.com/studio> when moving to a newer release.
- The OpenXR vendors plugin's version and checksum are pinned there too; update both from <https://github.com/GodotVR/godot_openxr_vendors/releases>.

## Building and running on the Quest 3

`build-quest.sh` builds the Godot project in `game/` into a debug APK, installs it on the connected headset and starts it.

```sh
tools/build-quest.sh                # build, install and launch
tools/build-quest.sh --no-install   # build only; adb is not used
```

The APK is written to `game/build/PanzaDragoonVR.apk`, and each step's full output to `game/build/logs/`. When a step fails, the script prints one line saying what to do or which log to read.

### One-time headset setup

1. **Turn on developer mode.** Register as a developer at <https://developers.meta.com/horizon/> (creating an organisation is enough). Then, in the Meta Horizon phone app: Menu, Devices, pick the headset, Headset settings, Developer mode, and switch it on. Restart the headset.
2. **Authorise USB debugging.** Connect the headset to this computer with a USB-C cable and put it on. Accept the "Allow USB debugging?" prompt, ticking "Always allow from this computer".

If the script says no headset was found, check the cable and developer mode. If it says the headset has not authorised this computer, put the headset on and accept the prompt. In both cases the APK has still been built.

### What to expect

- The first run unpacks Godot's Android build template into `game/android/` and downloads Gradle and its libraries (about 2 minutes); later runs take about a minute. Gradle keeps its downloads in `~/.gradle`, outside `~/.local/opt`.
- OpenXR needs Godot's Gradle build, so the prebuilt export template is not used. The build includes the Meta part of the OpenXR vendors plugin, which `setup-toolchain.sh` installs.
- The APK is signed with Godot's debug keystore, which Godot creates in its `editor_data` folder. It is not a release build.
- The headset no longer holds the launch back with a "controller required" prompt, because the app declares hand tracking as optional (the app itself does nothing with hands). So `build-quest.sh` can launch the app with nobody wearing the headset. When the app has not started within 10 seconds, usually because the headset is asleep, the script says so and exits with status 2.
- In the headset you should see a green chequered ground underfoot (or the image the asset folder supplies for `e03.floor`; see [Your own images in the headset](#your-own-images-in-the-headset)), a blue chequered sky overhead (or `e03.canopy`) and a grey cube flying in a circle right around you. A frame-rate readout stays in front of you as you turn; see [Reading the frame rate](#reading-the-frame-rate).
- The ground and sky images are tiled by a small shader in `game/Main.cs` that wraps the texture coordinates itself. On the Quest 3 a texture set to repeat came out clamped to its edge on these planes, so each showed one flat colour; the same scene tiled correctly on a desktop. The cause was not found.
- Inside the VS Code Flatpak sandbox there is no USB access, so the script runs `adb` on the host through `flatpak-spawn --host`. Outside a sandbox it runs `adb` directly.
- `game/.godot/`, `game/build/` and `game/android/` are generated and git-ignored.

### Exit statuses

| Status | Meaning |
|--------|---------|
| 0 | Built; and, unless `--no-install` was given, installed and still running 5 seconds after it started. |
| 1 | Something failed. One line on stderr says what, and what to do or which log to read. |
| 2 | Built and installed, but the app had not started within 10 seconds of the launch. The headset is usually asleep. |

### If the app crashes

The script clears the headset's log before launching. If the app starts and then stops, it saves the log to `game/build/logs/logcat-crash.log` and exits with status 1. Read that file before changing anything.

To capture the log by hand, use the host's `adb`, since the sandbox has no USB access (outside a sandbox, drop `flatpak-spawn --host`):

```sh
. tools/toolchain-env.sh
flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" logcat -d > game/build/logs/logcat.txt
```

### Reading the frame rate

A debug build shows a readout fixed in front of the camera, and writes the same figures to the headset's log once a second under the tag `PDZ-FPS`. A release build shows and logs neither.

**Where this stands on the Quest 3.** The display runs at 72 Hz, not 90. The headset offers four refresh rates and accepts the game's request for 90 Hz, but the display stays at 72 Hz, so the readout says `Asked for 90 Hz; running at 72 Hz (4 rates offered)` and the measured rate is 72. The cause is not known. Reaching 90 Hz is open work.

The readout's first line says what happened when the game asked for 90 Hz. After asking, the game reads back the rate the headset is running at, and frames are measured against that rate, not the rate asked for:

| First line | Meaning |
|------------|---------|
| `Target 90 Hz (4 rates offered)` | The headset offers 90 Hz, the game asked for it, and the headset is running at it. |
| `Asked for 90 Hz; running at 72 Hz (4 rates offered)` | The headset offers 90 Hz and the game asked for it, but the headset is still running at another rate. |
| `Running at 72 Hz; 90 Hz is not offered (3 rates offered)` | The headset's list of rates has no 90, so the rate was left alone. |
| `Running at 72 Hz; 90 Hz could not be set (0 rates offered)` | The headset gave no list of rates, so the rate was left alone. |
| `Not in VR` | OpenXR is not running; the scene is on a flat screen. |
| `Waiting for the headset` | The headset's session has not begun yet. |

The number in brackets is how many refresh rates the headset listed. The game asks when the headset's session begins and once more the first time the session gains focus; the line shows the latest answer. It also changes when the headset reports that its rate has changed.

Below it are the rate measured over the last second, the slow frames in that second and since the start, and the .NET version. A frame is slow when it lasts longer than 1.5 times the target interval: over 16.7 ms at 90 Hz, over 20.8 ms at 72 Hz.

Frames are counted only while the headset is showing the app. When it is not, for example with the headset taken off, the app loops without the headset pacing it, so counting stops and the readout says `Paused: the headset is not showing the app`. Counting carries on, with the totals kept, when the app is shown again.

Read the log while the app is running: the headset deletes an app's log lines soon after the app exits. Outside a sandbox, drop `flatpak-spawn --host`:

```sh
. tools/toolchain-env.sh
flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" logcat -d -s PDZ-FPS
```

Each second gives one line:

```
target=72 avg=72.0 slow=0 slow_total=7 frames_total=934 slow_pct=0.75
```

| Field | Meaning |
|-------|---------|
| `target` | The rate frames are measured against: the rate the headset is running at, which is 90 only when the request for it took effect. |
| `avg` | Frames in the last second divided by that second's exact length. |
| `slow` | Slow frames in the last second. |
| `slow_total`, `frames_total` | Slow frames and all frames counted since the headset first showed the app. |
| `slow_pct` | `slow_total` as a percentage of `frames_total`. |

A line beginning `status=` is written when the first line of the readout changes, and when counting is paused or resumed. The story's bar is `target=90`, every `avg` at least 89, and `slow_pct` under 1 after a minute of looking around. The Quest 3 does not meet it yet: `target` and `avg` are both 72, as said above. `build-quest.sh` clears the log at each launch, so the lines are all from the latest run.

On a desktop the same lines go to Godot's own output, each starting with `PDZ-FPS`.

## Unit tests

`test.sh` runs the unit tests on this computer: the Python tests in `tests/tools/`, then the C# tests in `tests/GameLogic.Tests/`. No headset is needed, and it takes a few seconds. It exits non-zero if either set fails.

```sh
tools/test.sh                         # run every test, Python and C#
tools/test.sh --filter FrameStats     # extra arguments go to `dotnet test`; only the C# tests run
```

The Python tests cover the scripts in `tools/`, at present the manifest writer. They use `unittest` from the standard library and work in temporary folders.

The C# tests cover the game's logic that needs no engine types, which lives in `game/Logic/`. They include the game's own reading of the manifest, with the same edge cases as the Python tests, so the two sides of the asset contract stay in step. The test project compiles those files itself and does not reference Godot. The first run downloads xUnit from NuGet into `~/.nuget`. Build output goes to `bin/` and `obj/` inside the test project, both git-ignored.

## A manifest for your own files

The game reads an asset folder only through its `manifest.json`, which must give every file's exact size. `write_manifest.py` writes that file for a folder of your own images and models, and checks a folder against it. The rules are those of the asset contract, `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md`.

### Naming files

Name each file after the asset ID it supplies, followed by `.png` or `.glb`. The name without its extension is the ID. Subfolders are yours to choose and are not part of the ID.

```
my-assets/
  images/
    e03.floor.png      supplies e03.floor
    e03.canopy.png     supplies e03.canopy
```

- An ID is lowercase letters, digits and underscores, in levels separated by single dots: `e03.floor`.
- Folder names follow the contract's path rules: lowercase letters, digits, underscores, hyphens and dots.
- A `.png` must be 8 bits per channel, RGB or RGBA. A `.glb` must be binary glTF version 2. Only the file's header is looked at.

### Writing the manifest

```sh
python3 tools/write_manifest.py my-assets
```

```
e03.canopy               image      204817  images/e03.canopy.png
e03.floor                image      181234  images/e03.floor.png
wrote my-assets/manifest.json with 2 assets
```

`manifest.json` is written at the top of the folder, with each file's size and SHA-256. Run the command again after changing any file; the manifest is replaced.

A file that is not an asset, such as a text file or a PNG whose name is not a valid ID, is left out and named in a one-line notice. For a file not named after its ID, give `ID=path`, with the path relative to the folder. The rest of the folder is still scanned:

```sh
python3 tools/write_manifest.py my-assets e03.floor=images/grass.png
```

When anything is wrong, each problem is printed on its own line, nothing is written, an existing `manifest.json` is left as it was, and the exit status is 1. That covers a folder with no assets, two files giving the same ID, and a file whose contents do not match its extension.

### Checking a folder

```sh
python3 tools/write_manifest.py --check my-assets
```

```
2 assets checked in my-assets: no problems
```

This reads `manifest.json` and the files it lists and changes nothing. It reports every problem, not only the first, and exits with status 1 if there is one: a manifest that is absent, unreadable or of an unsupported `format`, a duplicate ID, an invalid ID or path, a missing file, a wrong size, a wrong checksum, or contents that are not of the stated kind. Files the manifest does not list are ignored, as the game ignores them. An entry of a kind this script does not know is not a problem: its path, size and checksum are still checked, and a one-line notice says its contents were not.

The rules live in `tools/manifest.py`, which the extraction tool can import. Copying the folder to the headset is not part of this script; `push-assets.sh` does that.

## Your own images in the headset

### Where the game looks

On the headset the asset folder is the app's external files folder:

```
/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/
```

`manifest.json` goes directly inside `files/`. The game reads the folder once, when it starts. Nothing in the folder is packed into the APK.

How the game reaches it: it asks Android, through Godot's `AndroidRuntime` bridge, for `getApplicationContext().getExternalFilesDir(null)`, which also creates the folder if it is absent. If that cannot be asked or gives nothing, it uses the fixed path `/storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files` (the same folder; `/sdcard` links to `/storage/emulated/0`) and logs that it did. Extraction on the headset (epic 2) writes to this same folder. The code is in `game/AssetFolder.cs`; the manifest rules the game applies are in `game/Logic/AssetManifest.cs`, which follows the asset contract as `tools/manifest.py` does.

On a desktop run the game reads the folder named by the `PDZ_ASSETS` environment variable, and runs on placeholders when it is unset:

```sh
PDZ_ASSETS=my-assets "$GODOT" --path game --xr-mode off
```

### What the game uses

For now the game uses two images: `e03.floor` on the ground and `e03.canopy` on the sky. Each is tiled at the placeholder's tile size: one copy per 2 metres of ground and one per 20 metres of sky. Any image the folder does not supply, or supplies with a problem, is shown as its chequered placeholder instead; the game never stops for a bad file. The manifest's `sha256` is not read in the headset, only the size, so run `write_manifest.py --check` before copying.

### Copying a folder to the headset

```sh
python3 tools/write_manifest.py my-assets   # write the manifest
tools/push-assets.sh my-assets              # check it, then copy what is inside it to the headset
tools/push-assets.sh --clear my-assets      # the same, emptying the headset's folder first
tools/push-assets.sh --clear                # only empty it: the game runs on placeholders (also with a folder holding no files)
```

`push-assets.sh` runs `write_manifest.py --check` on the folder first and copies nothing if it reports a problem. It copies the folder's contents, not the folder itself, so `my-assets/manifest.json` lands at `files/manifest.json`. Without `--clear`, files already on the headset stay; the game ignores any the new manifest does not list. The copy goes as a `tar` stream through `adb exec-in`, since `adb push` runs on the host from inside the Flatpak sandbox and the host cannot see the sandbox's `/tmp`. Like `build-quest.sh`, it runs `adb` on the host when inside the sandbox.

The app is not restarted. Start it again to see the change, with `tools/build-quest.sh` or from the headset's library.

### Reading what the game made of the folder

Every line the loader writes starts with `PDZ-ASSETS`, in Godot's log (tag `godot` on the headset). Read it while the app is running:

```sh
. tools/toolchain-env.sh
flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" logcat -d | grep PDZ-ASSETS
```

| Line | Meaning |
|------|---------|
| `no manifest.json in <folder>: running on placeholders` | The folder is empty or has no manifest. Not an error. |
| `the manifest cannot be read: <reason>; running on placeholders` | Not JSON, a byte-order mark, or a required field absent or of the wrong type. A warning. |
| `manifest.json: unsupported format <n>; ...` | Made by a newer or older tool; run the matching tool again. A warning. |
| `<id>: using <file> (<w> x <h>)` | That image is in use. |
| `<id>: <reason>; using the placeholder for <id>` | That image had a problem: missing, wrong size, invalid entry, duplicate ID, wrong kind, or not a PNG that can be decoded. A warning. |
| `supplied: <ids>; placeholders: <ids>` or `no image was supplied: running on placeholders (...)` | The summary, last. |

An ID the manifest does not list gets its placeholder with no warning: that is the normal state before an image is supplied. Problems are only in the log for now; a report readable in the headset is story 1.5.

## Extraction scripts

The Python scripts here (`saturn_disc.py`, `cgb_textures.py`, `png.py`, `mednafen_state.py`) read the game disc and emulator save states. `tools/mednafen/` is a local emulator install and is git-ignored.
