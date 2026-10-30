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
| .NET 10 SDK | `dotnet-10` | 620 MB | Microsoft's `dotnet-install.sh`, channel 10.0 | no published checksum for the install script |
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

- An existing folder at one of the five install paths (`godot-4.7.2-stable-mono`, `dotnet-10`, `jdk-17`, `android-sdk` and `game/addons/godotopenxrvendors`) that has no `.installed` file is **deleted** and rebuilt, whatever it contains.
- The `android-sdk` marker records the pinned command-line tools and package list. Changing either in `setup-toolchain.sh` rebuilds `android-sdk` from scratch, which drops any packages you added by hand with `sdkmanager`.

### Removing it

```sh
rm -rf ~/.local/opt/godot-4.7.2-stable-mono \
       ~/.local/opt/dotnet-10 \
       ~/.local/opt/jdk-17 \
       ~/.local/opt/android-sdk \
       ~/.local/opt/.toolchain-setup-tmp
rm -rf game/addons/godotopenxrvendors
```

Remove one folder and run the script again to reinstall just that part. `~/.android` can be deleted too if nothing else on the machine uses it.

The plugin is needed to build this project, so only remove it for good if the project is to stop using it. Deleting its folder leaves two things behind that still name it: the line `res://addons/godotopenxrvendors/plugin.gdextension` in `game/.godot/extension_list.cfg` (delete that line, or all of the generated `game/.godot` folder), and the options `xr_features/enable_meta_plugin`, `meta_xr_features/quest_3_support` and `meta_xr_features/hand_tracking` in `game/export_presets.cfg` (delete those three lines).

### Notes

- The .NET 10 SDK (LTS, supported to November 2028) is installed, because .NET 9 leaves support on 2026-11-10. The unit tests and the desktop build target `net10.0`. The Android build still targets `net9.0`, because Godot 4.7.2's prebuilt Android export templates need exactly `net9.0` and a `net10.0` project fails to export ([godot#118989](https://github.com/godotengine/godot/issues/118989)); the .NET 10 SDK builds it, and the APK carries a .NET 9 runtime (9.0.21 on 2026-10-16). The APK moves to .NET 10 with Godot 4.8 stable, which makes `net10.0` the minimum and moves Android to .NET 10 ([godot#123738](https://github.com/godotengine/godot/pull/123738)); 4.8 beta 1 came out on 2026-10-15 and stable may land after 2026-11-10. Until then the APK's runtime gets no .NET security fixes after 2026-11-10.
- An older toolchain installed .NET 9 in `~/.local/opt/dotnet-9`. Nothing uses it any more; delete it with `rm -rf ~/.local/opt/dotnet-9`.
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
- In the headset you should see a green chequered ground underfoot (or the image the asset folder supplies for `e03.floor`; see [Your own images in the headset](#your-own-images-in-the-headset)), a blue chequered sky overhead (or `e03.canopy`), a grey cube flying in a circle right around you (or the model the folder supplies for `e03.flyer`; see [Your own model in the headset](#your-own-model-in-the-headset)) and a grey dragon under you, its paler head in front where the floor comes from (or the model the folder supplies for `dragon.ride`; see [Your own dragon](#your-own-dragon)). A frame-rate readout stays in front of you as you turn; see [Reading the frame rate](#reading-the-frame-rate).
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

A debug build shows a readout fixed in front of the camera, a little below the middle of the view so that it stays clear of the launch report board, and writes the same figures to the headset's log once a second under the tag `PDZ-FPS`. A release build shows and logs neither.

**Where this stands on the Quest 3.** The game runs at 90 Hz. It ran at 72 until the headset's battery saving was turned off and the headset restarted (ticket 1.10, 2026-10-17): with battery saving on, the headset holds every app at 72 Hz and ignores requests for other rates. If the readout says "Asked for 90 Hz; running at 72 Hz", check Settings, Device, Power for battery saving or power saving, turn it off and restart the headset. While the headset shows a system prompt such as the Guardian boundary dialog, it runs at 72 Hz until the prompt is cleared.

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
target=90 avg=90.3 slow=0 slow_total=6 frames_total=2604 slow_pct=0.23
```

| Field | Meaning |
|-------|---------|
| `target` | The rate frames are measured against: the rate the headset is running at, which is 90 only when the request for it took effect. |
| `avg` | Frames in the last second divided by that second's exact length. |
| `slow` | Slow frames in the last second. |
| `slow_total`, `frames_total` | Slow frames and all frames counted since the headset first showed the app. |
| `slow_pct` | `slow_total` as a percentage of `frames_total`. |

A line beginning `status=` is written when the first line of the readout changes, and when counting is paused or resumed. The story's bar is `target=90`, every `avg` at least 89, and `slow_pct` under 1 after a minute of looking around. The Quest 3 meets it with battery saving off (ticket 1.10): `target=90` with averages of about 90. `build-quest.sh` clears the log at each launch, so the lines are all from the latest run.

On a desktop the same lines go to Godot's own output, each starting with `PDZ-FPS`.

## Unit tests

`test.sh` runs the unit tests on this computer: the Python tests in `tests/tools/`, then the C# tests in `tests/GameLogic.Tests/` and `tests/Decoding.Tests/`. No headset is needed, and it takes a few seconds. It exits non-zero if any set fails.

```sh
tools/test.sh                         # run every test, Python and C#
tools/test.sh --filter FrameStats     # extra arguments go to `dotnet test`; only the C# tests run
```

With a filter, each C# project runs it, so the one with no matching test says so; that is not a failure.

The Python tests cover the scripts in `tools/`, at present the manifest writer, the test model writer, the shared asset-rule cases and `plane_probe.py`'s unpacker. They use `unittest` from the standard library and work in temporary folders.

`tests/GameLogic.Tests/` covers the game's logic that needs no engine types, which lives in `game/Logic/`. They include the game's own reading of the manifest, its check of a model file (`game/Logic/GlbCheck.cs`), the development and release mode decision and the deletion of the disc image (`game/Logic/ExtractionMode.cs`), with a case for each: development mode, release mode on a first launch, a folder already extracted from the same disc by this extractor or by another version of it, or from a different disc, a failed extraction or one that wrote nothing (the image kept), the generator the game passes held to the library's, no disc, a deletion failing and a hand-written manifest; the tuning file (`game/Logic/Tuning.cs`: the built-in values, each setting read, out of range or of the wrong type, and `tools/tuning.example.json` matching the built-ins), the planes' scroll (`game/Logic/PlaneScroll.cs`) and the launch report board (`game/Logic/LaunchReport.cs`). That project compiles those files itself and does not reference Godot; it references the decoding library for the tests only, so they can check the game's strings against the library's, and `game/Logic/` itself uses neither. The first run downloads xUnit from NuGet into `~/.nuget`. Build output goes to `bin/` and `obj/` inside each test project, both git-ignored.

`tests/Decoding.Tests/` covers the decoding library, `lib/PdzDecoding/` (see [Extracting from your own disc](#extracting-from-your-own-disc)): reading a cue sheet and the ISO9660 file system, finding and numbering the textures in a `.MDB`, decoding modes 1 and 5, finding `EPISODE3.PRG`'s colour upload tables and decoding modes 4 and 0 from them, unpacking and drawing the floor and canopy (the probe's hand-built streams, the literal pool, flipped pattern names, and a notice for no pool, too few maps or a bad stream), the PNG writer, the model writer (`GlbWriter`: one part, parts, all four drawing modes, flips, a triangle, a second atlas, bad models, the atlas's border and pixels, UVs, the sampler, the materials, no `uri`, accessor bounds and the same bytes written twice), and extraction as a whole, with a case for each behaviour (disc present, no disc, a bad image, a repeated record, transparency; the nine Episode 3 files, mode 5, a file missing, none present, a file with no textures; two colour tables, one, none, a bad pair, a colour code no pair filled, and modes 1 and 5 unchanged; the staging folder: none left behind, progress by disc file, the previous extraction kept while writing and replaced after, a failure part way, a leftover staging folder, and the disc file named in a disc file's error; the list of a disc image's own files, which release mode deletes, and a `.cue` that names an `.iso` beside it counted as one image). Every disc image, `.MDB` and `.CGB` it uses is built by the tests in a temporary folder; nothing comes from the disc.

The model writer's tests also compare three sample models, built in the tests, with what is recorded for them in `tests/Decoding.Tests/expected/models/`: `one_part.glb` and `parts.glb` byte for byte, and `two_atlases.glb.sha256`, the SHA-256 of the third, whose first atlas is a whole 2048 x 2048 image stored uncompressed (see the PNG writer's notes) and makes it about 16 MB, too large to commit. A deliberate change to the writer's output is recorded again, and the new files committed, with

```sh
PDZ_RECORD_EXPECTED=1 tools/test.sh --filter GlbWriterTests   # rewrites the recorded samples in the source tree
tools/validate-gltf.sh                                         # then check the samples with the Khronos validator
```

`tests/GameLogic.Tests/GlbCheckTests.cs` also checks that a model the library writes passes the game's own model check.

### Shared asset-rule cases

`tools/manifest.py` and the game's `AssetManifest` apply the same asset contract in two languages. So that they cannot drift apart unseen, both suites run one set of cases on disk, in `tests/fixtures/asset-rules/`. Each case is a small asset folder: a `manifest.json`, the files it lists, and `expected.json` with the problem lines, in order, that the game's launch report gives for it and `write_manifest.py --check` must give too. For a manifest that cannot be read at all, `expected.json` says `"unreadable_manifest": true` and only the start of the one line, `manifest.json: is not valid JSON`, is compared, since the two JSON readers word the detail differently; with `"whole_line": true` as well, as for the empty asset list (`manifest.json: lists no assets`), the whole line is compared. `"pc_notices"` lists notices only the PC gives.

`tests/tools/test_asset_rules.py` runs every case through `check_report`, runs the model cases through `write_manifest.py --check` and through write mode, checks that the good case's model is `make_test_model.py`'s output byte for byte, and checks that the folders are exactly what `make_cases.py` writes. `tests/GameLogic.Tests/AssetRulesTests.cs` reads every case with `AssetManifest.Load`, looks each entry up with its own kind when that is `image` or `model`, and merges the lines as `LaunchReport.Build` does.

Every file in the cases is written by `tests/fixtures/asset-rules/make_cases.py` and is the author's own; nothing comes from the disc. To change a case or add one, edit `cases()` in that script, run it, and commit the folders it writes:

```sh
python3 tests/fixtures/asset-rules/make_cases.py   # rewrites the case folders; running it twice changes nothing
tools/test.sh                                      # both suites run the new case
```

Where `tools/manifest.py` and the game word a shared rule differently, the game's wording is the reference, since its lines are what players read. The asset contract stays the authority on the rules themselves: a case that shows the game breaking the contract is a question for the author, not a case to copy. Rules that need a symbolic link stay in each suite's own tests, as git cannot keep links portably.

## A manifest for your own files

The game reads an asset folder only through its `manifest.json`, which must give every file's exact size. `write_manifest.py` writes that file for a folder of your own images and models, and checks a folder against it. The rules are those of the asset contract, `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md`.

### Naming files

Name each file after the asset ID it supplies, followed by `.png` or `.glb`. The name without its extension is the ID. Subfolders are yours to choose and are not part of the ID.

```
my-assets/
  images/
    e03.floor.png      supplies e03.floor
    e03.canopy.png     supplies e03.canopy
  models/
    e03.flyer.glb      supplies e03.flyer
```

- An ID is lowercase letters, digits and underscores, in levels separated by single dots: `e03.floor`.
- Folder names follow the contract's path rules: lowercase letters, digits, underscores, hyphens and dots.
- A `.png` must be 8 bits per channel, RGB or RGBA; only its header is looked at. A `.glb` must pass the game's model checks: see [When a model is rejected](#when-a-model-is-rejected).

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

When anything is wrong, each problem is printed on its own line, nothing is written, an existing `manifest.json` is left as it was, and the exit status is 1. That covers a folder with no assets, two files giving the same ID, and a file whose contents do not match its extension. A file the game would reject is named with its ID and path, in the game's own words, such as `e03.flyer: models/e03.flyer.glb: not self-contained: image 0 names the file 'skin.png', which was not opened`.

### Checking a folder

```sh
python3 tools/write_manifest.py --check my-assets
```

```
2 assets checked in my-assets: no problems
```

This reads `manifest.json` and the files it lists and changes nothing. It is a preview of the launch report: where the game applies a rule too, the line is the one the game shows in the headset, and the lines come in the game's order, first the problems with the entries themselves, then each file's. It reports every problem, not only the first, and exits with status 1 if there is one:

- a manifest that is absent, unreadable or of an unsupported `format`, or that lists no assets (`manifest.json: lists no assets`; a manifest of another `format` is reported as that even with no assets). As in the game, `NaN`, `Infinity` and nesting deeper than 64 arrays and objects make it unreadable, and absent or mistyped top-level fields are reported on one line;
- an invalid entry: a field absent or of the wrong type, an invalid ID or path, a negative `bytes`, or an `id` or `path` holding a lone surrogate escape such as `"\ud800"`. All of an entry's field problems share one line;
- a duplicate ID, which drops every entry with that ID, as the game does;
- a file that is missing, not a file, of another size (`incomplete or changed`), or not of its kind: an image that is not an 8-bit RGB or RGBA PNG (`unreadable image`), or a model the game rejects, by the whole table in [When a model is rejected](#when-a-model-is-rejected);
- and, on the PC only, a `sha256` that is malformed or does not match, and a `meta` that is not an object.

Files the manifest does not list are ignored, as the game ignores them. An entry of a kind this script does not know is not a problem: its path, size and checksum are still checked, and a one-line notice says its contents were not.

The rules live in `tools/manifest.py`, which the extraction tool can import. Copying the folder to the headset is not part of this script; `push-assets.sh` does that.

## Your own images in the headset

### Where the game looks

On the headset the asset folder is the app's external files folder:

```
/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/
```

`manifest.json` goes directly inside `files/`. The game reads the folder when it starts, and again when extraction from a disc image in `disc/` ends (see [On the headset](#on-the-headset)). Nothing in the folder is packed into the APK.

How the game reaches it: it asks Android, through Godot's `AndroidRuntime` bridge, for `getApplicationContext().getExternalFilesDir("")`, which also creates the folder if it is absent, and logs `asset folder from Android: <path>`. The argument is an empty string, not `null`: Godot's bridge cannot match the method's `String` parameter to a null value and returns nothing without an error, which until ticket 2.8 made every Quest launch fall back. If Android cannot be asked or gives nothing, it uses the fixed path `/storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files` (the same folder; `/sdcard` links to `/storage/emulated/0`) and logs that it did. Extraction on the headset (epic 2) writes to this same folder. The code is in `game/AssetFolder.cs`; the manifest rules the game applies are in `game/Logic/AssetManifest.cs`, which follows the asset contract as `tools/manifest.py` does.

On a desktop run the game reads the folder named by the `PDZ_ASSETS` environment variable, and runs on placeholders when it is unset:

```sh
PDZ_ASSETS=my-assets "$GODOT" --path game --xr-mode off
```

### What the game uses

For now the game uses two images, `e03.floor` on the ground and `e03.canopy` on the sky, and two models, `e03.flyer` and `dragon.ride` (see [Your own model in the headset](#your-own-model-in-the-headset)). Each is tiled at the tuning's tile size, by default one copy per 24 metres of ground and one per 48 metres of sky (see [Tuning the scene](#tuning-the-scene)). Any image the folder does not supply, or supplies with a problem, is shown as its chequered placeholder instead, and the board in front of you says why (see [The launch report in the headset](#the-launch-report-in-the-headset)); the game never stops for a bad file. The manifest's `sha256` is not read in the headset, only the size, so run `write_manifest.py --check` before copying.

### Copying a folder to the headset

```sh
python3 tools/write_manifest.py my-assets   # write the manifest
tools/push-assets.sh my-assets              # check it, then copy what is inside it to the headset
tools/push-assets.sh --clear my-assets      # the same, emptying the headset's folder first
tools/push-assets.sh --clear                # only empty it: the game runs on placeholders (also with a folder holding no files)
```

`push-assets.sh` runs `write_manifest.py --check` on the folder first and copies nothing if it reports a problem. It copies the folder's contents, not the folder itself, so `my-assets/manifest.json` lands at `files/manifest.json`. Without `--clear`, files already on the headset stay; the game ignores any the new manifest does not list. The copy goes as a `tar` stream through `adb exec-in`, since `adb push` runs on the host from inside the Flatpak sandbox and the host cannot see the sandbox's `/tmp`. Like `build-quest.sh`, it runs `adb` on the host when inside the sandbox; the device scripts (`build-quest.sh`, `push-assets.sh`, `push-tuning.sh` and `compare-headset.sh`) share the app's package name and its folder on the headset, that `adb` call and finding the connected headset through `quest-adb.sh`, which they source and which is not run on its own.

The app is not restarted. Start it again to see the change, with `tools/build-quest.sh` or from the headset's library.

`--clear` empties the whole folder, so it removes a pushed `tuning.json` too; push it again afterwards.

### Tuning the scene

How deep the floor is, how high the canopy, how fast they scroll and their tile sizes come from an optional `tuning.json` at the top of the asset folder; whatever it leaves out, or gets wrong, keeps its built-in value.

| Setting | Means | Built-in | Allowed |
|---------|-------|----------|---------|
| `floor_depth` | metres the floor lies below the play area's floor, where the player rides; at least 3 so the dragon clears the ground | 6 | 3 to 50 |
| `canopy_height` | metres the canopy is above the play area's floor | 10 | 4 to 100 |
| `flight_speed` | metres a second the floor scrolls past | 6 | 0 to 30 |
| `canopy_speed_scale` | how the canopy moves: 1 crosses the same angle a second as the floor, a flat backdrop; 0 stands still | 1 | 0 to 2 |
| `floor_tile`, `canopy_tile` | metres one copy of the floor or canopy image covers | 24, 48 | 4 to 500, 8 to 500 |

The asset contract's "Tuning" section is the full definition. To try new values:

1. Copy the built-ins somewhere outside `tools/`: `cp tools/tuning.example.json my-tuning.json`.
2. Edit the settings you want to change, for example `"floor_depth": 8`. Leave out or keep the others.
3. With the headset connected, push it: `tools/push-tuning.sh my-tuning.json`. It lands as `files/tuning.json` whatever the file was called.
4. Start the app again, from the headset's library or with `tools/build-quest.sh`; the file is read only at launch.
5. Check the launch board: a setting that is out of range or misspelt is named there and keeps its built-in value. The log's `PDZ-ASSETS tuning (...)` line shows every value in use (below).

To go back to the built-ins, run `tools/push-tuning.sh --remove`, or push `tools/tuning.example.json` itself.

`push-tuning.sh` checks that the file is JSON and copies it through `adb exec-in` as `push-assets.sh` does. Nothing can be changed from inside the headset. On a desktop run, put `tuning.json` in the `PDZ_ASSETS` folder.

The game logs what it uses at launch, for example:

```
PDZ-ASSETS tuning (from tuning.json and the built-ins): floor_depth 8, canopy_height 10, flight_speed 6, canopy_speed_scale 1, floor_tile 24, canopy_tile 48
```

or `tuning (built-in, no tuning.json): ...` when there is no file. A setting that is out of range or not a number, a field that is not a setting (a misspelt name, say) and a file that cannot be read each give a `PDZ-ASSETS tuning.json: ...` warning and a line on the board. The rules are in `game/Logic/Tuning.cs`.

### Reading what the game made of the folder

Every line the loader writes starts with `PDZ-ASSETS`, in Godot's log (tag `godot` on the headset). Read it while the app is running:

```sh
. tools/toolchain-env.sh
flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" logcat -d | grep PDZ-ASSETS
```

| Line | Meaning |
|------|---------|
| `no manifest.json in <folder>: running on placeholders` | The folder is empty or has no manifest. Not an error. |
| `the manifest cannot be read: <reason>; running on placeholders` | Not JSON, a byte-order mark, a required field absent or of the wrong type, or an empty `assets` list (`manifest.json: lists no assets`). A warning. |
| `manifest.json: unsupported format <n>; ...` | Made by a newer or older tool; run the matching tool again. A warning. |
| `<id>: using <file> (<w> x <h>)` | That image is in use. |
| `<id>: using <file>` | That model is in use. |
| `<id>: <reason>; using the placeholder for <id>` | That image or model had a problem: missing, wrong size, invalid entry, duplicate ID, wrong kind, not a PNG that can be decoded, or a model problem from the table in [Your own model in the headset](#your-own-model-in-the-headset). A warning. |
| `supplied: <ids>; placeholders: <ids>` or `no asset was supplied: running on placeholders (...)` | The summary, last. |
| `tuning (...): floor_depth <n>, ...` | The tuning the scene uses; see [Tuning the scene](#tuning-the-scene). |
| `tuning.json: <problem>` | A problem with the tuning file; that setting, or every setting when the file cannot be read, keeps its built-in value. A warning. |

An ID the manifest does not list gets its placeholder with no warning: that is the normal state before an image is supplied.

### The launch report in the headset

When the game has something to say about the asset folder, a dark board stands about 2 metres in front of where you start, at eye height. It is part of the world, like a sign, and does not follow your head. It stays for as long as the app runs; dismissing it comes with controller input. When every image and model the folder lists loads, there is no board.

Its wording follows the launch table in the asset contract:

| Board | Meaning |
|-------|---------|
| `Running on placeholders` (pale blue) | There is no `manifest.json` in the folder (on a desktop: `PDZ_ASSETS` is unset). Not an error; the folder path is shown. |
| `The asset manifest cannot be read` (amber) | Not JSON, not UTF-8, a byte-order mark, a required field absent or of the wrong type, or an empty `assets` list (`manifest.json: lists no assets`). The reason is on the next line. Everything is a placeholder. |
| `The assets were made by a newer or older tool than this build understands` (amber) | `format` is not 1. Run the matching tool again. Everything is a placeholder. |
| `<n> problems with the assets` (amber) | One line per problem, each starting with its ID: missing, incomplete or changed (size differs from `bytes`), invalid entry, duplicate ID, wrong kind, an image that cannot be decoded, or a model problem (see [Your own model in the headset](#your-own-model-in-the-headset)). Each affected ID shows its placeholder; the others load. |
| `The asset folder could not be loaded` (amber) | Something the checks did not foresee went wrong; the reason is shown and everything is a placeholder. |
| `<n> problems with tuning.json` (amber) | Only the tuning file has problems: one line each, naming the setting and its range, or the field that is not a setting, or why the file cannot be read. Those settings keep their built-in values. With other problems, the tuning file's are listed first, and a pale-blue notice turns amber. See [Tuning the scene](#tuning-the-scene). |
| `Nothing was extracted from the disc image` (amber) | Extraction from the disc image in `disc/` could not run or failed; the next line says why, and the folder's own report follows. See [The board while extracting, and when it fails](#the-board-while-extracting-and-when-it-fails). While extraction runs, the board shows its progress instead. |

The board shows at most 6 problems, then `and <n> more; the full list is in the log under PDZ-ASSETS`. Every line of the report, with nothing left out, is also logged as `PDZ-ASSETS report: <line>`:

```sh
flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" logcat -d | grep "PDZ-ASSETS report"
```

The text is built in `game/Logic/LaunchReport.cs` and shown by `game/LaunchReportPanel.cs`.

## Your own model in the headset

The grey cube that flies round you is the placeholder for `e03.flyer`, a model. A `.glb` the folder lists as `e03.flyer` flies the same path in its place, tumbling as the cube does, at the size the file gives it in metres. The rules for the file are the Models section of the asset contract; a hand-made model need only be a readable, self-contained binary glTF 2.0 file with at least one mesh, no required extensions and images the game can decode. Animations, skins, cameras and lights in it are ignored, and so are vertex colours (`COLOR_0`).

### A test model

`make_test_model.py` writes a small model to try this with: a pyramid about half a metre across whose faces are red, yellow, green and white, with an orange base, from one 16 x 16 PNG stored inside the file. It needs only the standard library.

```sh
python3 tools/make_test_model.py my-assets/models/e03.flyer.glb
python3 tools/write_manifest.py my-assets
tools/push-assets.sh my-assets
```

Then start the app: the pyramid flies where the cube did. The test model also carries blue vertex colours (`COLOR_0`), so it shows that a file with them is accepted. It cannot show that they are ignored: Godot 4.7.2 does not apply them on its own, so the faces keep their texture colours whether or not the game switches vertex colour off. The game switches it off on every material anyway, in case a later Godot does. A model of your own, exported from Blender as `glTF Binary (.glb)`, is supplied the same way.

### Your own dragon

The grey dragon under you is the placeholder for `dragon.ride`, a model: boxes for a body about 4 m long, a paler head on a short neck in front, a thinner tail behind and two flat wings about 3 m long. It is held still at the seat, with the top of its back 0.4 m above the play area's floor and directly below your head when the app starts, so a seated rider is astride it; standing, your legs pass into it. It does not follow your head: if you move or turn, recentre the view (hold the Meta button): the dragon comes back under you and the way of flight turns to where you are facing. A `.glb` the folder lists as `dragon.ride` sits there in its place. Build it in glTF's axes, front towards +Z, with its origin where the rider sits, on top of the back: the game puts the origin at the seat and turns the model 180 degrees about Y to face the way of flight. To try it with the test pyramid:

```sh
python3 tools/make_test_model.py my-assets/models/pyramid.glb
python3 tools/write_manifest.py my-assets dragon.ride=models/pyramid.glb
tools/push-assets.sh my-assets
```

Then start the app: the pyramid sits under you in place of the grey dragon, and the log's `PDZ-ASSETS supplied: ...` line lists `dragon.ride`. A rejected dragon reads as the flyer's lines below, with `dragon.ride` as the ID, and keeps the grey dragon. If the model loads but cannot be placed, the log reads `PDZ-ASSETS dragon.ride: the model could not be shown (<error>); using the placeholder`.

### When a model is rejected

The game reads the file's header and its JSON before the engine sees it, and never opens a file the model names. Any problem keeps the model's placeholder (the grey cube for `e03.flyer`, the grey dragon for `dragon.ride`), and the board and the log say why, with one line starting with the ID:

| Line | Meaning |
|------|---------|
| `e03.flyer: <path>: unreadable model: <reason>` | Not a `.glb` of glTF 2.0, cut short, JSON that cannot be read, or something the engine could not load. |
| `e03.flyer: unreadable model: it could not be loaded (<error>)` | Loading failed in a way the checks did not foresee. The other assets keep what they loaded. |
| `e03.flyer: <path>: not self-contained: buffer 0 names the file '<name>', which was not opened` | The model refers to a file outside itself; also for an external image (`image <n> names the file ...`). Re-export it as one `.glb`. |
| `e03.flyer: <path>: not self-contained: image <n> has a data: URI` | Also for a buffer. The contract allows no `uri` of any kind. |
| `e03.flyer: <path>: unsupported model: it requires the extension <name>` | The file lists the extension in `extensionsRequired`, such as mesh compression. Export without it. |
| `e03.flyer: <path>: unreadable texture in model: image <n> could not be decoded` | An image inside the file is not one the game can decode. |
| `e03.flyer: <path>: empty model: it has no mesh` | The file has no mesh. |
| `e03.flyer: <path>: empty model: no node uses any of its meshes` | The file has meshes, but no node uses one. |
| `e03.flyer: wrong kind: the manifest says 'image', the game needs 'model'` | The manifest lists `e03.flyer` with another kind. |

Missing files, wrong sizes and the other manifest problems read as they do for images. The model checks are in `game/Logic/GlbCheck.cs`; loading it is in `game/AssetFolder.cs`.

`write_manifest.py` applies the same checks, in the same order and words, when it writes a manifest and with `--check`, so a model it passes is not rejected for any reason above except two it cannot see: an image inside the file that the engine cannot decode, and a load that fails in the engine.

### Checking a model with the Khronos validator

The asset contract says every model the tool writes passes the Khronos glTF Validator with no errors. `validate-gltf.sh` runs it:

```sh
tools/validate-gltf.sh                        # the decoding library's sample models
tools/validate-gltf.sh my-assets/models/*.glb # any .glb files, your own included
```

With no files named, it first runs the sample models' test with `PDZ_GLB_SAMPLES` set to a temporary folder, which makes the test write the three samples there as well as checking them (so it needs `dotnet`, as `tools/test.sh` does), validates those and removes the folder. It prints one line per file, `validate-gltf: <file>: 0 errors, 0 warnings, 0 infos, 0 hints`. A file with an error gets `validate-gltf: error: <file>: <counts>; the first is <code> at <where>` on stderr instead, and the script exits 1; so does a file that is missing or that the validator gives no report for, or an unknown argument. It exits 0 when no file has an error. Warnings, infos and hints are counted but do not fail it.

The validator is the pinned release 2.0.0-dev.3.10 for Linux, checked against its SHA-256. The script reads the report with `jq`, which it needs on every run. The first run downloads the validator (with `curl`, `tar`, `xz`, `sha256sum` and `find`) into `$TOOLCHAIN_ROOT/gltf-validator-2.0.0-dev.3.10/`, by default `~/.local/opt`, beside the rest of the toolchain, with an `.installed` marker naming the release and checksum; a folder without that marker is downloaded again. It is never put in the project or committed, and the script asks the validator for its report on stdout, so it leaves no report files beside the models. To remove it, delete that folder.

## Extracting from your own disc

The game extracts the textures, and the floor and canopy, from the player's own disc image itself, on the headset, at launch. A PC command runs the same code on the PC, so the files can be looked at and checked there. Both use one C# library, `lib/PdzDecoding/`, which holds everything the project knows about the Saturn disc and uses no Godot types. It targets `net9.0` for the APK and `net10.0` for the desktop game, the PC command and the tests; the game references it as a project, and the APK carries `PdzDecoding.dll`.

### What is extracted

The textures of the nine Episode 3 files the asset contract names (`E03CMN`, `E03B0N0` to `E03B0N2`, `E03B1N0` to `E03B1N2`, `E03BOSS`, `E03END`), in the four colour modes they use: 16 colours through a lookup table (mode 1), true colour (mode 5), and the colour-bank modes, 256 colours (mode 4) and 16 colours (mode 0).

**Disc images.** Any layout the asset contract accepts is read: a cue sheet whose track 1 is MODE1/2352 or MODE1/2048, with one `.bin` per track or one `.bin` holding every track, or a plain `.iso` of 2048-byte sectors (the data track alone). Track 1 is read from its `INDEX 01`; the Saturn header at its sector 0 gives the disc's product and version, and its ISO9660 file system starts at sector 16. CHD and other compressed images, and a MODE2 or audio track 1, are not read.

**Names and numbers.** Each texture is written as `images/e03/<file>/<n>.png`, `<file>` being the disc file's name in lowercase, numbered from `0000` as the asset contract's "Extracted textures" says: in the order its first drawing record appears in the file's `.MDB`, with a record whose four words all equal an earlier one's taking no new number.

**Colours for modes 4 and 0.** The colour-bank modes take their colours from VDP2 colour RAM, which Episode 3 fills from colour upload tables in `EPISODE3.PRG`. The tool finds the tables by their pattern (a destination of `0x25F00000` then a source address `0x060xxxxx`, pairs running until the first destination that is not `0x25Fxxxxx`), builds colour RAM from the second when there are two or more, else from the only one, and colours each pixel as `zwei-disc-formats.md`, "Palettes for colour banks", says; a colour code no pair filled is black, and a pair whose source lies outside the file is skipped and named. The colours come from the disc, never from a save state. On the author's disc the table used is the one at 0xE23C. A disc with no `EPISODE3.PRG`, or no table in it, leaves the mode 4 and mode 0 textures out with a notice saying why; they keep their numbers, so there may be gaps.

**The floor and canopy.** `e03.floor` and `e03.canopy` are the two planes of VDP2's RBG0 layer, packed in `EPISODE3.PRG` with an LZ77 variant (see `zwei-disc-formats.md`, "Floor and canopy"). The tool finds them as `tools/plane_probe.py` does, not by fixed offsets, so the Japanese and European discs both work: the set-up code's literal pool (`0x25E40000` then the cells' address, and `0x25E60000` then the map list's) and the colour RAM literals `0x25F00C00` and `0x25F00E00` 0x3C and 0x30 bytes before it. It unpacks the cells and the two maps into a copy of VDP2's VRAM, copies the 256 and 128 colours into colour RAM, and draws each plane as one whole 512x512 page, every pattern name read as VDP2 reads it (cell, palette and flips), with every pixel opaque. The VDP2 plane settings (16x16-pixel cells, 256 colours, one-word names with PNCR 0x8008's supplement, maps 0xC0 and 0xC1) are constants in the library, read from the Episode 3 save state, since the code that sets them is not traced. They are written as `images/e03/floor.png` and `images/e03/canopy.png`. The planes are an addition: a disc with no `EPISODE3.PRG`, no literal pool in it, fewer than two maps, or a stream that is malformed (a pointer outside the file, a stream that runs off its end or copies from before its start) still has its textures written, and a notice says why there is no floor and canopy. They are decoded with the colours and written with the textures, so they add no progress step. On both of the author's discs the pixels equal `plane_probe.py`'s renders.

**Files the disc lacks.** A file of the nine that the disc lacks (its `.MDB` or its `.CGB`) is left out and named, and the others are still extracted; a disc with none of the nine is an error. A file with nothing to write gets no folder.

On the author's disc that is 420 textures in nine folders, nothing left out: 250 in modes 1 and 5, 161 in mode 4 and 9 in mode 0; and the floor and canopy, 422 assets in all.

### What is written

**The manifest.** After the PNGs, extraction writes `manifest.json` at the top of the output folder, as the asset contract's "What extraction writes" fixes it: `format` 1, `generator` `pdz-extract 0.2` (the library's version; 0.2 added the floor and canopy), `source` the disc's product and version, and one `image` entry per PNG written, the floor and canopy included, sorted by ID, with its `bytes` and `sha256`, in the same text `write_manifest.py` writes. It replaces any manifest already there; files extraction did not write stay but are no longer listed. When no PNG was written, no manifest is written and any there is left alone. `write_manifest.py --check` passes on the folder.

**The staging folder.** Everything goes through a staging folder, as the contract's "What extraction writes" says: the PNGs and then the manifest are written into `extracting/` inside the output folder, which is emptied first (a leftover from an interrupted run is cleared that way); only when every one is written are they moved into place, `manifest.json` last, and `extracting/` removed. A failure before that, such as a full disk, moves nothing, so the previous files and manifest stay as they were. The PC command and the game share this.

**The PNGs** are 8-bit RGBA, and their data is stored without compression. .NET's own compressor gives different bytes on the PC and on the headset (zlib-ng in .NET 9 and 10 on the PC, in two versions, and Android's system zlib in the APK), while stored data is the same everywhere. The cost is size: `E03B0N0`'s 28 textures take 110 KB instead of 15 KB.

The game's scene uses none of the extracted texture IDs. It asks for `e03.floor`, `e03.canopy`, `e03.flyer` and `dragon.ride`, and shows a placeholder for each the folder lacks; extraction supplies the first two. The built-in tuning (a 512-pixel floor page covering 24 m, the canopy's 48 m) was set by the author in the headset for the extracted planes, so the placeholders' chequers look large.

### On the PC

```sh
dotnet run --project tools/pdz-extract -- "rom/JP/Panzer Dragoon II Zwei (Japan).cue" extracted/pdz-extract
```

```
pdz-extract: disc GS-9049 V1.001
pdz-extract: modes 0 and 4 coloured from the colour table at 0xE23C of EPISODE3.PRG (table 2 of 2), 5 blocks
pdz-extract: floor and canopy from EPISODE3.PRG: cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244
pdz-extract: wrote 420 textures from 9 disc files to /.../extracted/pdz-extract/images/e03
pdz-extract: wrote the floor and canopy as images/e03/floor.png and images/e03/canopy.png
pdz-extract: wrote /.../extracted/pdz-extract/manifest.json listing 422 assets
pdz-extract:   E03CMN: 125 written
pdz-extract:   E03B0N0: 28 written
...
pdz-extract:   E03END: 45 written
```

Check the folder against its manifest:

```sh
python3 tools/write_manifest.py --check extracted/pdz-extract
```

```
422 assets checked in extracted/pdz-extract: no problems
```

The European disc's ISO reads the same way, and gives the same 422 files, byte for byte, though the planes sit at other offsets; only the manifest's `source` differs:

```sh
dotnet run --project tools/pdz-extract -- "rom/PAL/Panzer Dragoon II Zwei (Europe).iso" extracted/pdz-extract-pal
```

```
pdz-extract: disc MK-81022 V1.000
pdz-extract: modes 0 and 4 coloured from the colour table at 0xE23C of EPISODE3.PRG (table 2 of 2), 5 blocks
pdz-extract: floor and canopy from EPISODE3.PRG: cells at 0x3FD90, maps at 0x525EC and 0x52860, colours at 0x52C28 and 0x53228
pdz-extract: wrote 420 textures from 9 disc files to /.../extracted/pdz-extract-pal/images/e03
pdz-extract: wrote the floor and canopy as images/e03/floor.png and images/e03/canopy.png
pdz-extract: wrote /.../extracted/pdz-extract-pal/manifest.json listing 422 assets
...
```

`pdz-extract <disc.cue|disc.iso> <output folder>` writes the same files, with the same names and bytes, that the game writes in its asset folder, into the output folder, `manifest.json` included. Write it under `extracted/`, which is git-ignored: the files come from the disc. `-h` prints the usage. An error is one line, `pdz-extract: error: <line>`, and the exit status is 1: no such file, a file that is neither a `.cue` nor an `.iso`, a cue sheet naming a missing `.bin` or with another kind of track 1 (the error names the mode found), no ISO9660 volume at sector 16, or a disc with none of the nine files (the error names them). The second line names the colour table used, with any pair skipped (`; skipped: the pair at 0x... (destination 0x..., source 0x...)`), or, when there is none, reads `no colours for modes 0 and 4: the disc has no EPISODE3.PRG; those textures are left out` (or `EPISODE3.PRG holds no colour upload table`, `EPISODE3.PRG's colour table at 0x<offset> has no block inside the file and colour RAM` when every pair of the chosen table is skipped, or `EPISODE3.PRG cannot be read (<reason>)`, after which modes 1 and 5 are still written); that is a notice, not an error. The third line names where the floor and canopy came from (`floor and canopy from EPISODE3.PRG: cells at 0x..., maps at 0x... and 0x..., colours at 0x... and 0x...`) or, when there are none, reads `no floor and canopy: <reason>; the game keeps its placeholders for them`, the reason being `the disc has no EPISODE3.PRG`, `EPISODE3.PRG cannot be read (<reason>)`, `EPISODE3.PRG holds no literal pool with the cells' and maps' VRAM addresses`, `map list entry 0x<address> at 0x<offset> is outside EPISODE3.PRG`, `no colour RAM 0x<address> source inside EPISODE3.PRG 0x<n> bytes before the literal pool at 0x<offset>`, `the cells pointer 0x<address> at 0x<offset> is outside EPISODE3.PRG`, `the map list pointer 0x<address> at 0x<offset> is outside EPISODE3.PRG`, `the map list runs off the end of EPISODE3.PRG at 0x<offset>`, `EPISODE3.PRG's map list holds 0 maps, not the 2 the floor and canopy need` (or `holds 1 map`), `EPISODE3.PRG's map <i> at 0x<offset> would go past the end of VRAM`, or a stream's own, `EPISODE3.PRG's <stream>: the stream at 0x<offset> <problem>`, where `<stream>` is `cells` or `map <i>` and `<problem>` is `runs off the end of the file`, `copies from before its start`, `starts outside the file`, or `unpacks to more than 0x<n> bytes` (when it would not fit VRAM); that too is a notice, and the textures are still written. One line per disc file follows the total, with any textures left out by mode, such as `E03CMN: 8 written (1 in mode 0, 116 in mode 4 left out)`: a file the disc lacks reads `<file>: not on the disc (no <file>.MDB)` or `(no <file>.CGB)`, naming whichever is absent (the `.MDB` when both are), and one whose `.MDB` holds no record that fits its `.CGB` reads `<file>: no textures`. Every texture of every file is decoded before any file is written, so a decoding error writes nothing, and a file with no texture to write gets no folder. A write that fails part way, such as a full disk, writes only into `extracting/`, so the output folder keeps what it held; no `extracting/` is left behind either way, unless it cannot be removed, when the next run empties it.

### On the headset

The game looks in `disc/` inside its asset folder, `/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/disc/`. When that holds one disc image, a `.cue` sheet with its `.bin` files (or with an `.iso` it names, which is then the sheet's file and not a second image) or one `.iso`, it extracts at launch and writes the texture PNGs into `files/images/e03/<file>/`, the floor and canopy as `files/images/e03/floor.png` and `canopy.png`, and `manifest.json` into `files/`, through `files/extracting/`, byte for byte as the PC command writes them for the same disc. Whether it extracts, and whether the disc image stays, depends on the mode (see [Development and release modes](#development-and-release-modes)).

#### Putting your disc image on the headset

Copy your disc image there with the host's `adb`: for the Japanese dump about 475 MB, the `.cue` and its four `.bin` files; for the European ISO, the one `.iso` of about 390 MB. The home folder is shared with the host, so the host's `adb` can read the dumps in `rom/JP/` and `rom/PAL/` directly (outside the Flatpak sandbox, drop `flatpak-spawn --host`). Do not use `push-assets.sh` for it, which checks a manifest.

```sh
. tools/toolchain-env.sh
adb() { flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" "$@"; }
dir=/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/disc
adb shell mkdir -p "$dir"
for f in "$PWD/rom/JP"/*.cue "$PWD/rom/JP"/*.bin; do adb push "$f" "$dir/"; done
# or, instead (one image only in disc/):
# adb push "$PWD/rom/PAL/Panzer Dragoon II Zwei (Europe).iso" "$dir/"
```

Then start the app. To check what it extracted against the PC, see [The PC and the headset write the same files](#the-pc-and-the-headset-write-the-same-files).

#### Development and release modes

The asset contract's "Development and release modes" fixes two modes, decided at each launch from the asset folder alone, whatever the build:

- **Development mode** is on when an empty file named `development` sits at the top of the asset folder, `files/development`. The game extracts on every launch and keeps the disc image.
- **Release mode** is the mode without that file. The game extracts when `disc/` holds an image, unless the folder's `manifest.json` was written by this extractor (its whole `generator` is the library's, such as `pdz-extract 0.2`) from a disc with the same `source` product and version as the image's Saturn header; then it skips extraction. A folder extracted by an older extractor is extracted again when the image is copied back. After a successful extraction that wrote something (a texture or a plane), and after such a skip, it deletes the disc image; an extraction that succeeds but writes nothing keeps it, and says so.

What release mode deletes is the image's own files only: the `.cue` and every `.bin` it names that sits inside `disc/` (a subfolder counts, but never anything outside `disc/`), or the `.iso`. `disc/` itself and anything else in it stay. The `.cue` goes first; if it cannot be deleted, the `.bin` files are kept with it. Nothing is deleted after a failed extraction, which keeps the previous contents as before, nor after one that wrote nothing. A deletion that fails is logged as one warning and the game carries on. A hand-written manifest (`hand-written`, or any `generator` other than the library's), one from another version of the extractor, or one from another disc is never a reason to skip. The PC command has no modes: it always extracts and never deletes anything.

Turn development mode on and off on the headset with the host's `adb` (set up as above):

```sh
adb shell touch /sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/development   # on: extract every launch, keep the image
adb shell rm /sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/development      # off: release mode
```

In release mode the image is gone after the first launch that extracts; push it again (above) to extract again, after removing `manifest.json`, with a disc of another product or version, or once a newer build's extractor has a different version.

#### The board while extracting, and when it fails

The scene does not wait for extraction. The game reads the folder as it is, then starts extraction on a background thread, which runs the decoding library alone (nothing on that thread touches the engine), and at once, without waiting for it, dresses the scene and starts OpenXR and the frame-rate readout. While it runs, the launch report's board shows its progress instead, in pale blue: `Extracting the assets from your disc image`, the disc image's name, the step under way (`Reading E03B0N0 (2 of 9)`, `Writing E03B0N0 (2 of 9)`, then `Moving the new files into place`) and that the scene shows what the folder already holds until then. Each step is also logged, one `PDZ-ASSETS extracting: ...` line each. The full Japanese disc takes about 0.3 s on the Quest 3, so the board may only flash. When extraction ends the game reads the folder again, dresses the ground, sky, flyer and dragon in what it now holds, and puts the launch report back on the board.

When extraction cannot run or fails, the board says so and the game carries on with what the folder held, or placeholders. The board's heading is `Nothing was extracted from the disc image` (amber), then one of these lines, worded as the asset contract's extraction rows, then `The scene shows what the asset folder already holds.` and the folder's own report, if it has one:

| Board line | Meaning |
|------------|---------|
| `More than one disc image in <folder>/disc (<names>). Nothing is extracted; leave only one.` | More than one `.cue` or `.iso`. |
| `The disc image cannot be read: <reason>.` | The image as a whole cannot be read: a missing `.bin`, no ISO9660 volume, another kind of track 1, or none of the nine files. |
| `Extraction failed on <disc file>: <reason>. The previous contents are kept.` | A disc file could not be read, such as one past the end of a cut-short image; nothing was moved into place. |
| `Extraction failed: <reason>. The previous contents are kept.` | Something else failed before the move, such as a full disk while writing into `extracting/` or emptying a leftover one; nothing was moved into place. |
| `Extraction failed while moving files into place: <reason>. Some new files may already be in place; the previous manifest is kept.` | A move failed part way. The manifest is moved last, so the previous one stays; a new PNG that replaced an old one at the same path shows as changed against it. |

A damaged image tried on the PC: the Japanese track 1 cut to 30 MB gives `Extraction failed on E03CMN: Panzer Dragoon II Zwei (Japan) (Track 1).bin: the image ends inside sector 50310. The previous contents are kept.`, and the folder's files and manifest are unchanged.

#### Reading the log

Read the log while the app is running (with `adb` set up as above):

```sh
adb logcat -d | grep PDZ-ASSETS
adb shell ls /sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/images/e03/
```

| Line | Meaning |
|------|---------|
| `extracted <n> textures from <m> disc files of <product> <version> into <folder>/images/e03, listed in <folder>/manifest.json, in <s> s (<details>)` | Extraction ran; `<n>` texture PNGs were written, from the `<m>` disc files that gave at least one, and the manifest listing them. `<details>` gives the PNGs per file (`E03CMN 125, E03B0N0 28, ...`), then the colour table modes 0 and 4 came from (`colour table at 0xE23C of EPISODE3.PRG (table 2 of 2), 5 blocks`, with any pair skipped) or why there is none (`no colours for modes 0 and 4: the disc has no EPISODE3.PRG`, `EPISODE3.PRG holds no colour upload table`, or `EPISODE3.PRG's colour table at 0x<offset> has no block inside the file and colour RAM`), then, when there are any, the textures left out by colour mode (`left out: mode 0 9, mode 4 161`), then where the floor and canopy came from (`floor and canopy from EPISODE3.PRG: cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244`) or why there are none (`no floor and canopy: <reason>`, with the PC command's reasons), then the files the disc lacks (`missing: E03END (no E03END.MDB)`, or `.CGB`, whichever is absent) and the files with no textures (`no textures in: ...`). |
| `<mode>: no disc image in <folder>/disc: nothing to extract` | `disc/` is absent or holds no `.cue` and no `.iso`; `<mode>` is `development mode` or `release mode`. Nothing is extracted or deleted. Not an error. |
| `more than one disc image in <folder>/disc (<names>): nothing extracted` | A warning: more than one `.cue` or `.iso`, such as a cue sheet and an ISO together; remove all but one. A cue sheet's `.bin` files are part of it and do not count, but an `.iso` does, even one the cue sheet names: for a cue sheet that points at an `.iso`, keep only the `.iso`. |
| `development mode (development is in <folder>): extracting from <image> in the background into <folder>; the disc image is kept` | Development mode: extraction has started in the background; the scene does not wait for it. |
| `release mode: extracting from <image> in the background into <folder>; the disc image is deleted after a successful extraction that writes something` | Release mode, and the folder was not extracted from this disc: extraction has started in the background. |
| `release mode: <folder> is already extracted from <product> <version>: not extracting again; deleting the disc image <image>` | Release mode, and the folder's manifest is the extractor's from the same disc: nothing is extracted, and the image is deleted. |
| `release mode: deleted the disc image <image> (<files>)` | Release mode, after a successful extraction that wrote something, or a skip: the files named were deleted. |
| `release mode: the extraction wrote nothing, so the disc image <image> is kept` | Release mode, after an extraction that succeeded but wrote no texture and no plane: the image stays in `disc/`. |
| `release mode: the disc image <image> could not be deleted (<file>: <error>; ...); carrying on` | A warning, one line for every file that could not be deleted. When the `.cue` or `.iso` itself cannot go, nothing else of the image is deleted. The game carries on. |
| `extracting: reading <file> (<n> of 9)`, `extracting: writing <file> (<n> of 9)`, `extracting: moving the new files into place` | Each step, as the board shows it. A file with nothing to write has no `writing` line. |
| `the disc image cannot be read: <reason>; nothing extracted, the previous contents are kept` | A warning: the image cannot be read, or it has none of the nine Episode 3 files; the reasons are those of the PC command. The game carries on. |
| `extraction failed on <file>: <reason>; nothing moved into place, the previous contents are kept` | A warning: a disc file could not be read. |
| `found no textures to write on <product> <version>: nothing written (<details>)` | The disc was read but gave no texture to write; no manifest is written either. |
| `extraction failed (<error>); nothing moved into place, the previous contents are kept` | Something the checks did not foresee, such as a full disk, before the move. A warning. |
| `extraction failed while moving files into place (<error>); some new files may already be in place, the previous manifest is kept` | A move into place failed part way. A warning. |
| `extraction could not be started (<error>); carrying on with what the folder holds` | `disc/` could not be looked in. A warning. |

Every report line that follows is logged too, as `PDZ-ASSETS report: <line>`. The code is in `game/DiscExtraction.cs`, started from `_Ready` and followed from `_Process` in `game/Main.cs`; the board's words are in `game/Logic/ExtractionLines.cs` and `LaunchReport.WithExtractionProblem`; the mode decision and its log lines are in `game/Logic/ExtractionMode.cs`, and the list of an image's files in `DiscFolder.ImageFiles` in the library.

### The PC and the headset write the same files

`tools/compare-headset.sh <disc.cue|disc.iso>` shows it. Launch the app on the headset first, so it has extracted, then give the same disc image as it holds:

```sh
tools/compare-headset.sh "rom/JP/Panzer Dragoon II Zwei (Japan).cue"
```

It pulls the headset's `manifest.json` and `images/`, sets aside any file under `images/` that the headset's manifest does not list (such as one left by an earlier push), runs `pdz-extract` on the disc image on this computer, and compares the two byte for byte. Both copies go under `extracted/compare-headset/` (git-ignored), as `headset/` and `pc/`. It exits 0 and says `identical` when every file matches, the manifest included; otherwise it exits 1 and lists the files that differ (the full list is in `differences.txt` there). This is also the only check that runs the library's .NET 9 build, the one the APK carries: the unit tests run on .NET 10.

## Extraction scripts

The Python scripts here (`saturn_disc.py`, `cgb_textures.py`, `png.py`, `mednafen_state.py`) read the game disc and emulator save states. They are the prototypes the decoding library was ported from, and stay as they are. `tools/mednafen/` is a local emulator install and is git-ignored.

`plane_probe.py` unpacks Episode 3's floor and canopy planes (cells, maps and colours) from a disc image, compares them byte for byte with a save state unpacked by `mednafen_state.py`, and renders the two planes. It is the prototype the library's plane extraction (`lib/PdzDecoding/Planes.cs`) was ported from, and its tests check the port against it:

```sh
python3 tools/plane_probe.py "rom/JP/Panzer Dragoon II Zwei (Japan) (Track 1).bin" extracted/plane-probe-jp extracted/state_e03
```

It takes the data track (a 2352-byte-sector `.bin` or a 2048-byte `.iso`), writes only inside `extracted/`, prints `identical` for each region that matches the state, and exits 1 if one differs. `-h` prints its usage. Leave out the state folder to unpack and render only.
