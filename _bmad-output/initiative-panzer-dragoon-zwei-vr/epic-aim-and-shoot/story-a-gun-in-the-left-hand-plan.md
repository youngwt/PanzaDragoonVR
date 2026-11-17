---
title: 'A gun in the left hand'
type: 'feature'
ticket: '2'
created: '2026-11-16'
status: 'built'
baseline_revision: '943a9f39531c696eb2e77c787dc1d59bc82a6f63'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The left hand aims a reticle (5.1), but nothing is held there. CAP-7 asks for a gun that tracks the controller, and in the end it will be the rider's gun from the disc (5.3).

**Approach:** The game asks the asset folder for a new model ID, `rider.gun`. It holds that model in the left hand when the folder supplies one, else a grey placeholder gun built in code. Either one is turned so its barrel points along the aim, at the reticle. The asset contract gains the ID, its prefix, its placeholder and a rule for how a gun model is laid out. That rule is what 5.3's extracted gun and a hand-made test gun both follow.

## Boundaries & Constraints

**Always:**
- The gun rule, written in the contract's Models section: a gun's origin is its grip, the point the hand holds; its barrel points along +Z; units are metres, in glTF's axes. The game puts the origin at the left controller's aim point and turns the model 180° about Y, so +Z points along the aim, as `dragon.ride` is turned to face the flight.
- The grey placeholder is built in the same model axes, origin at its grip, barrel along +Z, with a paler muzzle so the front can be told at a glance, as the grey dragon's paler head is.
- The placeholder is the author's own, made in code; nothing from the disc.
- A model the folder supplies is loaded and checked like `e03.flyer` and `dragon.ride`: the same `AssetFolder.Model` path, the same problems on the board and in the log, the same fallback to the placeholder (`CarryOrPlaceholder`). It is read again after extraction.
- `tools/make_test_model.py` gains a test gun, a long shape with its tip along +Z and its root node named `gun`, made here and owing nothing to the disc, so the author can check the convention in the headset.
- Every place that lists the IDs the game asks for names `rider.gun`.

**Never:**
- No extraction of the gun; that is 5.3.
- No firing or muzzle point; those are 5.6 onwards.
- No tuning setting for the gun's position or size.
- No change to the asset rules' grammar, or to `format`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No gun in the folder | Manifest lacks `rider.gun` | Grey gun in the left hand, pointing at the reticle; `rider.gun` among the placeholders in the log | None |
| Gun supplied | `rider.gun` listed, a valid `.glb` | That model in the left hand, its +Z along the aim; the grey gun hidden | None |
| Gun rejected | `rider.gun` listed, file missing, wrong size or wrong kind | Grey gun; the board and log name `rider.gun` and why | As for the other models |
| Gun cannot be held | The model loads but the game cannot add it | Grey gun; a log line names `rider.gun` | `CarryOrPlaceholder` |
| Left controller not tracked | Controller off or lost | The gun goes with the controller node, as the reticle hides; nothing special is done | None |
| After extraction | Folder read again | The new model, or the placeholder, replaces the old one | None |

</frozen-after-approval>

## Code Map

- `game/AssetIds.cs` -- add `RiderGun = "rider.gun"`, with the placeholder named in its comment.
- `game/Main.cs` -- `LoadAssets` returns a tuple of the folder's assets (`assets.Model(...)` for each, freed on failure); `ShowAssets` carries each through `CarryOrPlaceholder(id, done, model, carry)`. Add the gun to both and to the class summary. `game/Main.Extraction.cs:45-49` unpacks the same tuple after extraction and must pass the gun too.
- `game/RiddenDragon.cs` -- the pattern for a holder: `_placeholder` built in code in `_Ready`, `Carry(Node3D?)` frees the old model, adds the new one, hides or shows the placeholder; it turns 180° about Y for +Z models. New `game/HeldGun.cs` follows it, without the seat or animation parts.
- `game/Main.tscn` -- `XROrigin3D/LeftHand` (an `XRController3D` on the `aim` pose; its -Z is the aim). Add the holder there as a child node with its script, or add it in code from `Main`; the gun must move with the controller.
- `game/Main.Reticle.cs` -- the reticle hides when the left hand isn't tracked. The gun needs nothing extra: an untracked `XRController3D` keeps its last transform, which is acceptable for now.
- `tools/make_test_model.py` (argparse; a known exception that exits 2 on a usage error) and `tests/tools/test_make_test_model.py` -- add a gun option and its test.
- Docs:
  - `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md`:
    - line 36, "four IDs", becomes five;
    - the IDs prefix table gets a `rider.` row;
    - the placeholder list gets the grey gun;
    - Models gets the gun rule beside `dragon.ride`'s origin rule (around line 247).
  - `tools/README.md`:
    - line 120, what to expect;
    - line 320, what the game uses;
    - line 468, which placeholder;
    - line 527, the IDs asked for;
    - a new "Your own gun" section after "Your own dragon", in its style.

## Tasks & Acceptance

**Execution:**
- [x] `game/AssetIds.cs` -- the new ID.
- [x] `game/HeldGun.cs` -- the holder and its grey placeholder (a grip below the origin, a barrel along +Z about 0.25 m long, a paler muzzle), turned 180° about Y.
- [x] `game/Main.tscn` and/or `game/Main.cs`, `game/Main.Extraction.cs` -- load, carry and re-carry the gun.
- [x] `tools/make_test_model.py`, `tests/tools/test_make_test_model.py` -- the test gun.
- [x] `asset-contract.md`, `tools/README.md` -- as mapped.

**Acceptance Criteria:**
- Given the headset with no `rider.gun` in the folder, when the author holds the left controller, then the grey gun is in the hand, its muzzle pointing at the reticle, and the log lists `rider.gun` among the placeholders, at 90 Hz.
- Given the test gun pushed and listed as `rider.gun`, when the app starts, then it is in the hand in the grey gun's place, its tip pointing at the reticle.
- Given `tools/test.sh`, when it runs, then every suite passes.

## Implementation Notes

- The holder is a `Gun` node, with `HeldGun.cs`, under `XROrigin3D/LeftHand` in `Main.tscn`, so it moves with the controller; `_Ready` turns it 180° about Y and builds the placeholder. Godot's import wrote `game/HeldGun.cs.uid`.
- The grey gun: a barrel 3.5 cm square on the aim line, from 3 cm behind the origin to 0.19 m in front, a paler muzzle to 0.22 m (0.25 m in all), and a grip hanging below the origin to about 11.5 cm.
- `make_test_model.py --gun` writes the test gun: the pyramid made long and thin (6 cm across), turned a quarter about X so its tip is at 0.3 m along +Z, its orange base 5 cm behind the origin, root node and mesh named `gun`. Without `--gun` the pyramid is byte-for-byte as before (the shared `good-folder` case still matches).
- A model that cannot be held logs `rider.gun: the model could not be held (...)`, through `CarryOrPlaceholder` with `done` = `held`.

## Plan Change Log

- 2026-11-17, the author's word after the first look in the headset: "the gun's pitch is a bit off, can you add a tuning variable?". A tenth tuning setting, `gun_pitch`, in degrees from -90 to 90, built-in 0, tilts the gun about its grip from the controller's aim; positive raises the muzzle. The reticle's ray now follows the gun's barrel, so the gun still points at it and the first criterion holds at any pitch. The Never forbids a setting for the gun's position or size; a pitch is neither, but it is close enough that it goes in its own commit naming this decision. Changed in all four places the built-ins are kept, and in the tuning list, which lists every setting.

## Review Triage Log

Quick lens, one pass: 2 low patched; 1 not a code finding.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| `tools/README.md:120` lost the separator before "a grey dragon under you" | low | patch | Comma added. |
| `HeldGun.Placeholder`'s summary says the handle hangs 10 cm below the origin; the box reaches 11.5 cm | low | patch | Comment corrected. |
| Headset criteria 1 and 2 not yet met | -- | author | The author's checks, asked at the end of the build. |

## Verification

Results (2026-11-16, implementation):
- `tools/test.sh`: Python 108 OK, GameLogic.Tests 362 passed, Decoding.Tests 294 passed.
- `dotnet build game/PanzaDragoonVR.csproj`: 0 warnings, 0 errors.
- `make_test_model.py --gun extracted/test-gun/models/gun.glb`, then `tools/validate-gltf.sh` on it: 0 errors, 0 warnings.
- `tools/build-quest.sh`: built, installed and started (exit 0). The APK lists `PdzDecoding.dll` and no disc data. The log read `PDZ-ASSETS supplied: e03.floor, e03.canopy, dragon.ride; placeholders: e03.flyer, rider.gun` and `PDZ-FPS target=90 avg=90.0`.
- Not yet made: the author's look in the headset at the grey gun and at the test gun under `rider.gun`.

**Commands:**
- `tools/test.sh` -- expected: all pass
- `dotnet build game/PanzaDragoonVR.csproj` -- expected: 0 warnings, 0 errors
- `python3 tools/make_test_model.py <gun option> extracted/test-gun/models/gun.glb`, then `tools/validate-gltf.sh` on it -- expected: 0 errors
- `tools/build-quest.sh` -- expected: installs and starts; the APK lists `PdzDecoding.dll` and no disc data

**Manual checks (if no CLI):**
- Headset (the author): the grey gun in the left hand pointing at the reticle; then the test gun under `rider.gun` in its place, pointing the same way.
