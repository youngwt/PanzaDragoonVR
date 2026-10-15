---
title: 'Grey cube loaded from a model file'
type: 'feature'
ticket: '7'
created: '2026-10-14'
status: done
baseline_revision: 'cb1b739b32a432001cdf9ff4932e56be9f732176'
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

**Problem:** The asset contract defines a `model` kind (a self-contained binary glTF) and the flying grey cube stands in for `e03.flyer`, but the game cannot load a model, so the dragon and enemy epics have nothing to build on and the model format has never been tried on the headset.

**Approach:** Extend the asset loader to read a `model` entry by the contract's Models section: check the file is a self-contained `.glb` it can use, build it at runtime, and fly it in place of the grey cube when the folder supplies `e03.flyer`. Any problem keeps the grey cube and is named in the launch report.

## Boundaries & Constraints

**Always:**
- The contract's "When a model is invalid" table decides what is rejected; the game does not police conventions beyond it.
- A model is used whole or not at all; never half a model.
- The game never opens a file a model names.
- Vertex colours in a model are ignored, as the contract requires for format 1, although Godot turns them on by default.
- Model checks that need no engine are engine-free C# in `game/Logic/`, unit-tested on the PC.
- The game still always runs.

**Never:**
- No disc decoding and no conversion of Saturn models (epics 2, 4 and 6).
- No animations, skins or collisions.
- No change to the image loading or to which images are accepted.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Model supplied | Valid `.glb` listed as `e03.flyer` | That model flies the cube's path in place of the grey cube | No error expected |
| Not listed | No `e03.flyer` entry | Grey cube | Not an error |
| Not readable | Not a `.glb` of glTF 2.0 | Grey cube | Report: unreadable model, with ID and path |
| Not self-contained | External buffer, external image or `data:` URI | Grey cube; the named file is not opened | Report: not self-contained, with ID and path |
| Required extension | `extensionsRequired` lists anything | Grey cube | Report: unsupported model, with ID and extension |
| Bad texture | An embedded image the game cannot decode | Grey cube | Report: unreadable texture in model, with ID |
| No mesh | Loads but has no mesh | Grey cube | Report: empty model, with ID |
| Vertex colours | Valid model carrying `COLOR_0` | Shown without the vertex colours applied | Not an error |
| Wrong kind | `e03.flyer` listed as `image` | Grey cube | Report: wrong kind, as for images |

</frozen-after-approval>

## Code Map

- `game/Logic/AssetManifest.cs` -- `Find(id, expectedKind)` gives an `AssetLookup`; it already handles the `model` kind's listing, size and kind checks. Add the model's content checks alongside, engine-free.
- `game/Logic/LaunchReport.cs` -- builds the board's lines from the lookups and load problems; model problems join the same list.
- `game/AssetFolder.cs` -- opens the folder, loads images, collects problems, `Report()`. Model loading goes here too.
- `game/Main.cs`, `game/Main.tscn`, `game/FlyingCube.cs` -- the cube is a `MeshInstance3D` named `Cube` with `FlyingCube.cs` moving it; a loaded model must follow the same path.
- Godot 4.7 runtime glTF: `GLTFDocument.AppendFromBuffer(bytes, basePath, state)`, then `GenerateScene(state)`. Per the 1.6 research (read in source, not run): a missing or undecodable image only warns and leaves a null texture, so the game must check for it; `COLOR_0` turns on vertex colour as albedo, so it must be switched off on each loaded material; `KHR_materials_unlit` becomes unshaded.
- `tools/write_manifest.py` already accepts `.glb` (glTF magic and version 2); `tools/push-assets.sh` pushes a checked folder; device checks by `adb exec-out screencap -p` with the proximity sensor held closed.
- `deferred-work.md` carries the `COLOR_0` requirement from ticket 1.6.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/GlbCheck.cs` -- read a `.glb`'s header and JSON chunk and report the contract's problems: not glTF 2.0, external buffers or images, `data:` URIs, required extensions, no mesh.
- [x] `tests/GameLogic.Tests/GlbCheckTests.cs` -- one or more tests per matrix row it covers, with small `.glb` files built in the tests.
- [x] `game/AssetFolder.cs` -- load `e03.flyer`: run the check, build the scene, reject on any null texture, switch off vertex colour on every material, record problems for the report.
- [x] `game/Main.cs`, `game/FlyingCube.cs` -- fly the loaded model in place of the grey cube's mesh when there is one.
- [x] `tools/make_test_model.py` -- write a small self-contained test `.glb` (for example a coloured pyramid with an embedded PNG), standard library only, so the headset check needs no outside tool.
- [x] `tools/README.md` -- supplying a model, and what the report says about a bad one.

**Acceptance Criteria:**
- Given the test model pushed as `e03.flyer` with the two images, when the app starts, then the test model flies the cube's path and the ground and sky show the images.
- Given the model file then removed from the headset, when the app starts, then the grey cube flies and the launch report names `e03.flyer` and its path as missing.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

- `AssetManifest.Find(id, "model")` runs `GlbCheck` on the file, so the contract's table is applied in the engine-free lookup; `AssetFolder.Model` runs it again on the exact bytes handed to `GltfDocument.AppendFromBuffer` (empty base path), so a file changed between the two is still never followed outside itself.
- `GlbCheck` order: header and length, JSON chunk, `asset.version` 2.x, any `uri` on a buffer or image (external or `data:`), buffer 0 against the BIN chunk, `extensionsRequired`, then meshes and at least one node using one. Images are not decoded there; `AssetFolder` rejects the model when `GltfState.GetImages()` has a null where the file lists an image.
- `GltfState.HandleBinaryImageMode` is `EmbedAsUncompressed`, since there is no folder to extract textures to.
- `FlyingCube` now derives from `MeshInstance3D` (the node it was already attached to) so `Carry(model)` can clear the cube's mesh and add the model as a child; the path and tumble are unchanged.
- `tests/tools/test_make_test_model.py` checks the generated file's structure and that `manifest.py` accepts it.
- Desktop run (Godot 4.7.2, `PDZ_ASSETS`): the test model loads and flies, drawn in its texture colours, not the blue `COLOR_0`; Godot 4.7.2 left `VertexColorUseAsAlbedo` off on this textured material even before the game switches it off. A corrupt embedded PNG, an external buffer, a required extension and a broken accessor each kept the cube and gave the expected report line. `build-quest.sh --no-install` builds the APK.
- Review fixes: a JSON string that parses but cannot be read (a lone surrogate escape such as `"\uD800"` in `asset.version`, an `extensionsRequired` entry or an image `uri`) is now "unreadable model", with three tests. `AssetFolder.Model` catches any unexpected exception on its own, keeping the cube and reporting `e03.flyer: unreadable model: it could not be loaded (...)`. With an injected exception on the desktop, the ground and sky still loaded their images. `Main` also guards `Carry`.
- Vertex colours, tried on the desktop with the game's switch disabled: Godot 4.7.2 imports `COLOR_0` (16 blue colours in the surface arrays) but leaves `VertexColorUseAsAlbedo` off in all five variants tried: textured unlit material, untextured unlit material, untextured lit material, no material, and no material with `VEC3` colours. Rendered frames of the untextured unlit and no-material variants show a white pyramid, not blue. So no model could be made where Godot turns vertex colour on, the deferred `COLOR_0` concern from ticket 1.6 does not hold for 4.7.2 runtime loading, and the game's switch has not been seen to change anything. It is kept in case a later Godot does turn vertex colour on. The README no longer claims the test model checks this.
- Not done: the two headset acceptance checks; no headset was connected (`adb devices` empty).

- **Headset checks, 2026-10-15.** With the test pyramid and two generated images pushed, the log read `supplied: e03.floor, e03.canopy, e03.flyer; placeholders: none` and a screen capture showed the red and yellow pyramid flying with the striped ground and dotted sky. With the model then deleted on the headset, the log read `e03.flyer: models/e03.flyer.glb: missing` and the capture showed the grey cube and the report board naming it. The first capture is kept as `devlog/media/2026-10-15-model-flyer-and-own-images.png`.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens), 2026-10-14: high 1, medium 1, low 1, false 0, maybe-false 0. Two patched, one patched in part, one carried.

- high, patched: a lone surrogate escape in the model's JSON passed parsing and then threw, reaching `Main._Ready`'s catch-all, which dropped the ground and sky images too. Now reported as an unreadable model; the model load is also guarded on its own, so a model failure never touches the images (checked on the desktop by injecting an exception).
- medium, patched in part: the test model could not show whether vertex colours are ignored. Five model variants showed Godot 4.7.2 never turns vertex colour on, so there is no case for the switch to act on; the switch stays as a guard for later Godot versions and the README no longer claims the test model checks it.
- low, patched: the README quoted one empty-model line for two different cases.
- carried: the two headset criteria were unverified (no headset connected). Since met: see the headset checks in Implementation Notes.

## Design Notes

- **Checks before Godot sees the file.** Reading the JSON chunk first lets the game reject an external reference or a required extension without Godot ever trying to open another file, which the contract forbids.
- **A generated test model.** The contract says hand-made models need only pass the invalid-model table. A small script keeps the check repeatable without Blender; the author can push their own `.glb` the same way.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all tests pass.
- `python3 tools/make_test_model.py <folder>/models/e03.flyer.glb`, add the two images, `tools/push-assets.sh <folder>`, launch, capture -- expected: the test model flies, images on ground and sky.
- `adb shell rm` the model, launch, capture -- expected: grey cube; the report names `e03.flyer` as missing.
