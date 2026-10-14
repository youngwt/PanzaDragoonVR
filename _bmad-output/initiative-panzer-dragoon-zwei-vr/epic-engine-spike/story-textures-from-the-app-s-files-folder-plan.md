---
title: "Textures from the app's files folder"
type: 'feature'
ticket: '4'
created: '2026-10-14'
status: 'built'
baseline_revision: 'd5b3627cf6eabd2e44ebc0c1001da12259da32e2'
route: 'full'
route_source: 'auto'
risk: 'medium'
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

**Problem:** The game shows only its built-in placeholders. Nothing yet reads the asset folder on the headset, so neither the author's own files nor, later, extracted ones can reach the scene, and the In-game extraction epic does not know how the game reaches that folder.

**Approach:** Add a runtime asset loader that finds the app's external files folder, reads `manifest.json` as the asset contract defines, and replaces the ground and sky placeholders with the images listed for `e03.floor` and `e03.canopy`. Anything missing or invalid falls back to the placeholder. A small command pushes a checked folder to the headset.

## Boundaries & Constraints

**Always:**
- The asset contract is the authority for the manifest, IDs, paths and the image kind.
- The game always runs: no manifest, a bad manifest or a bad file means the placeholder for that ID, never a crash or a blank plane.
- Manifest reading and validation are engine-free C# in `game/Logic/`, unit-tested on the PC.
- No asset file is packed into the APK.
- Every fallback is written to the log with the ID and the reason.

**Never:**
- No launch report in the headset (story 1.5); here problems go to the log only.
- No models, no `e03.flyer` (story 1.7).
- No disc reading or extraction (epic 2).
- No change to the tiling shader's behaviour beyond giving it a loaded image.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Both images supplied | Valid manifest listing `e03.floor` and `e03.canopy` as images, files present at the stated sizes | Ground and sky show the supplied images, tiled | No error expected |
| Empty folder | No `manifest.json` | Placeholders; one log line saying the game runs on placeholders, with the folder path | Not an error |
| One image supplied | Manifest lists only `e03.floor` | Supplied ground, placeholder sky | No error expected |
| Bad manifest | Not JSON, a required field absent, or a `format` other than 1 | Placeholders for both | Logged with the reason |
| Bad entry | Missing file, wrong size, broken path rules, wrong kind, duplicate ID | Placeholder for that ID only | Logged with the ID and reason |
| Unreadable image | Listed PNG that cannot be decoded | Placeholder for that ID | Logged with the ID and path |
| Not on Android | Desktop run | The folder named by the `PDZ_ASSETS` environment variable, or placeholders when it is unset | Not an error |

**Decision (2026-10-14):** the headset check uses two test images generated in code, clearly unlike the placeholders. The author approved the plan without choosing between generated images and their own; their own pictures can replace the generated ones at any time with `tools/push-assets.sh`.

</frozen-after-approval>

## Code Map

- `game/Main.cs` -- `_Ready` calls `Dress(plane, PlaceholderTextures.Ground() / .Sky(), GroundTileMetres / SkyTileMetres)`; `Dress` sets the tiling `ShaderMaterial`'s `image` and `tiles`. The loader's result goes in where the placeholder textures are passed now.
- `game/PlaceholderTextures.cs` -- holds `GroundAssetId = "e03.floor"` and `SkyAssetId = "e03.canopy"`; reuse them.
- `game/Logic/` -- engine-free C#, compiled into `tests/GameLogic.Tests` by `<Compile Include="../../game/Logic/**/*.cs" />`. `System.Text.Json` is available on .NET 8 and 9.
- `tools/manifest.py` -- the PC side's version of the same rules (IDs, paths, kinds). The C# must agree with it; it is not shared code.
- `tools/write_manifest.py` -- writes and checks a folder's manifest; `--check` before pushing.
- `tools/build-quest.sh` -- builds, installs and launches; `host_adb` shows how to reach `adb` from the Flatpak sandbox through `flatpak-spawn --host`.
- On the headset `/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/` exists (created by the app's runtime) and is writable by `adb push`. How Godot reaches it from C# is to be settled here (the contract leaves it to this story): for example Android's external files directory through Godot's Android bridge, or the fixed path. Record the choice in Implementation Notes and in `tools/README.md`, since epic 2 writes to the same folder.
- `stack.md` already says: `Image.LoadFromFile`, then `ImageTexture`, `GenerateMipmaps()` for the tiling planes.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/AssetManifest.cs` -- parse and validate a manifest to the contract; look up an image entry by ID with its file checks; report problems as a list -- testable core.
- [x] `tests/GameLogic.Tests/AssetManifestTests.cs` -- one or more tests per matrix row that the logic covers.
- [x] `game/AssetFolder.cs` -- locate the folder (Android external files directory; `PDZ_ASSETS` on desktop) and load an image for an ID, or return null with the reason logged.
- [x] `game/Main.cs` -- use the loaded image when there is one, the placeholder otherwise.
- [x] `tools/push-assets.sh` -- run `write_manifest.py --check` on a folder, then push its contents (not the folder itself) to the app's files folder through host `adb`; `--clear` empties the app's folder first.
- [x] `tools/README.md` -- the asset folder's location on the headset, and how to supply your own images.

**Acceptance Criteria:**
- Given two images named `e03.floor.png` and `e03.canopy.png` with a manifest from `write_manifest.py`, when `tools/push-assets.sh` pushes them and the app starts, then the ground and sky show those images.
- Given `tools/push-assets.sh --clear` on an empty folder, when the app starts, then the chequered placeholders are back and the log says it is running on placeholders.
- Given the built APK, when its file list is inspected, then neither image is in it.
- Given `tools/test.sh`, when it runs, then all Python and C# tests pass.

## Implementation Notes

- **How the game reaches the folder (settled here).** `game/AssetFolder.cs` asks Android through Godot's `AndroidRuntime` singleton: `getApplicationContext().getExternalFilesDir(null).getAbsolutePath()`. If the bridge is absent, throws or gives nothing, it falls back to the fixed path `/storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files` and logs that. On a desktop it reads `PDZ_ASSETS`. Recorded in `tools/README.md` under "Your own images in the headset". Not yet seen running on the headset: see Verification status below.
- **Manifest rules in C#.** `game/Logic/AssetManifest.cs` mirrors `tools/manifest.py`: the same ID and path rules and wording, integers that reject `1.0` and `true`, a byte-order mark rejected, a symbolic link that leads out of the folder rejected. Two differences by design, both from the contract: the game does not read `sha256`, and it checks files only for the IDs it asks for (`Find`), not for every entry. A duplicate ID rejects every entry with that ID. A kind the game does not know is not a manifest problem; it is "wrong kind" when that ID is asked for. Entry-level problems are also collected in `AssetManifest.Problems` for the launch report of story 1.5.
- **Image check.** Before decoding, the header must be an 8-bit RGB or RGBA PNG, as the contract's image kind and `write_manifest.py --check` require. The file is then decoded with `Image.LoadPngFromBuffer`, so a file is decoded as PNG whatever its name; a decode failure falls back with "unreadable image". Mipmaps are generated as for the placeholders.
- **Log.** Every line starts `PDZ-ASSETS` and goes through Godot's log (`GD.Print` for notices, `GD.PushWarning` for fallbacks). The "running on placeholders" line names the folder.
- **`push-assets.sh` copies with `tar` through `adb exec-in`, not `adb push`.** In the Flatpak sandbox `adb` runs on the host, which cannot see the sandbox's `/tmp`, so a folder there could not be pushed by path. Files arrive owned by `shell`, as with `adb push`.
- **Test images** (Decision 2026-10-14): two 64 x 64 RGBA PNGs generated in code in the session's scratch folder, orange and dark red diagonal stripes for `e03.floor`, magenta with yellow dots for `e03.canopy`. Not committed. They are on the headset now.

### Verification status

- `tools/test.sh`: 65 Python and 83 C# tests pass.
- `tools/build-quest.sh --no-install`, then `unzip -l game/build/PanzaDragoonVR.apk | grep -c e03`: 0.
- Desktop, `godot --headless --xr-mode off` with `PDZ_ASSETS` at seven folders: both images used; unset variable and empty folder give the "running on placeholders" line; not-JSON manifest, a wrong-size file and an undecodable PNG each fall back with the reason; one image supplied gives that image and the other placeholder.
- Headset: `push-assets.sh` and `--clear` work against the real files folder. The app could not be launched: twice the headset held the launch back because "a Guardian dialog is currently showing", with the proximity sensor held closed. So the Android folder lookup, the images on ground and sky, and the logcat line after `--clear` are **not yet seen on the headset**. The test images are pushed; the author puts the headset on, clears the Guardian prompt, and looks (or runs `tools/build-quest.sh` and reads `logcat | grep PDZ-ASSETS`).

- **Headset check, 2026-10-14.** With the generated images pushed, the author saw the dotted sky and the striped ground (orange and dark red, which read as orange and brown) in the headset. After `tools/push-assets.sh --clear` and a relaunch, the log read `no manifest.json in /storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files: running on placeholders`.
- **Folder lookup on the Quest 3.** The `AndroidRuntime` call for `getExternalFilesDir` returned nothing (`Android gave no external files folder`), so the fixed path is what runs. It is the right folder; epic 2 should treat the fixed path as the working route, or fix the bridge call.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens), 2026-10-14: high 1, medium 1, low 5, false 0, maybe-false 0. Six patched, one carried.

- high, patched: invalid UTF-8 inside a manifest string threw out of `AssetManifest.Parse`, so `Main._Ready` stopped before dressing the planes and starting OpenXR. The file is now checked as UTF-8 first, and asset loading in `_Ready` is wrapped so any exception falls back to placeholders.
- medium, patched: the inside-the-folder check followed links only on the last path part, so a link in a parent folder or a `..` after a link escaped the folder (reproduced twice). Now resolves every part like realpath; tests for both cases.
- low, patched: a duplicate ID named the ID where `entry N` was meant.
- low, patched: an integer too large for 64 bits was "must be an integer"; now matches `tools/manifest.py`.
- low, patched: `push-assets.sh --clear <empty folder>` failed the manifest check and cleared nothing.
- low, patched: a README sentence read as if the C# tests used `unittest`.
- carried: acceptance criteria 1 and 2 unverified on the headset at review time (a Guardian prompt blocked the launch). Since met: see the headset check in Implementation Notes.

## Design Notes

- **Logic in C#, rules mirrored from Python.** The game cannot run Python, and epic 2's decoding library is C#, so the manifest rules need a C# home anyway. Two implementations of one contract can drift; the contract's worked example and the same edge cases in both test suites keep them aligned.
- **The images tile at the placeholder's tile size for now.** Real floor and canopy images get their own tiling and scrolling in epic 3.
- **Headset check.** The app launches without a controller prompt, so after a push the ground and sky can be checked by `adb exec-out screencap -p` with the proximity sensor held closed (`adb shell am broadcast -a com.oculus.vrpowermanager.prox_close`, restored with `automation_disable`). A Guardian prompt can block a launch; then the author looks instead.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all tests pass.
- `tools/build-quest.sh --no-install` then `unzip -l game/build/PanzaDragoonVR.apk | grep -c e03` -- expected: 0.
- `tools/push-assets.sh <folder>`, launch, `adb exec-out screencap -p` -- expected: the supplied images on ground and sky.
- `tools/push-assets.sh --clear`, launch, `adb logcat -d` while running -- expected: placeholders, and the "running on placeholders" log line.

