---
title: 'The dragon beats its wings'
type: 'feature'
ticket: '22'
created: '2026-11-10'
status: 'built'
baseline_revision: '2790eface837a9395efa57cdc90f483d893960c3'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/backlog/story-the-dragon-beats-its-wings.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The ridden dragon holds a still pose, though each form's `.MTB` holds a wing beat (spike 18).

**Approach:** Build backlog 22 as its ticket says. The ticket's seven criteria, Boundaries and dated decisions are this plan's acceptance and constraints: every form's model gets its slot-0 wing beat as one stepped glTF animation on the existing flat parts, with the root's own motion left out, and the game loops the ridden dragon's at 30 frames a second.

## Boundaries & Constraints

**Always:** The ticket's Boundaries and decisions. Output byte-identical on the PC (net10.0) and the headset (net9.0): the motion's angles become matrices without `Math.Sin`, `Cos`, `Atan2` or any other library function whose last bit may differ between runtimes. Use only `+ - * /`, `Math.Sqrt` and integer arithmetic, for example a sine table built from integers or the decoder's 16.16 products. The still model's parts, corners, origins and textures stay byte-for-byte what they are now, apart from the generator. Tests build their own data.

**Never:** No other motion slot, no swapped wing shapes, no blending, no root motion, no tuning setting, no change to texture numbering or to the contract's `format`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Both discs | JP cue or PAL ISO | Ten forms and the ride, each with one animation | None |
| Motion unreadable | No slot table found, a slot-0 pointer outside the `.MTB`, or a body tree that is not 27 nodes | Dragons written still; the dragons line says why there is no wing beat | Not a failure |
| Still `dragon.ride` | Placeholder, hand-made model, older extraction | Shown still | No problem |
| Loop | Game shows an animated ride | Loops at 30 frames a second, stepped | None |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Dragons.cs` -- `FromDisc` (finds the seat table by searching `1ST_READ` for the ten body roots, `FindSeats`, `SeatTableGap`), `Decode` (walks the tree, builds 16.16 `products`, one `ModelPart` per mesh node named `nodeNN`, origin = product translation less the seat, corners = product rotation applied to the vertices), `Multiply`, `Row`, `ToGltf`, `UnitsPerMetre`. `DragonSources.Describe` is the dragons line, joined into `ExtractionResult.Details()` and `DragonLine()` (`Extraction.cs`), which `pdz-extract` prints and the game logs.
- `zwei-disc-formats.md`, "Motion: `.MTB`" -- the layout: 300 bytes a frame, records `tx ty tz rx ry rz` (×16, 4,096 a turn), parts A/B/C and their node map, `Ry*Rx*Rz`, nodes 1, 6 and 14 keep the `.MDB` matrix, the slot table in `1ST_READ` (10 form pointers in form-index order, 12 slots of 16 bytes: A, B and C pointers and a count). How to find that table on any disc follows the seat table's way: by searching, not by a fixed offset.
- `lib/PdzDecoding/Model.cs` -- `Model(Name, Parts)`, `ModelPart`; add an optional animation (name, frame rate, per-part keys of rotation and translation). `GlbWriter.cs` -- write it: one `animations` entry, `STEP` samplers, input accessor of times `f / 30` with min and max, output `VEC4` rotations and `VEC3` translations, channels on the part nodes; keep the existing output identical when a model has no animation. `Validate` checks the keys.
- Keys for a flat part at frame f: rotation `R_f * R_rest⁻¹` and translation `t_f - seat`, where `R_f`, `t_f` are the part's chain product with the frame's local matrices (node 0 kept at its `.MDB` matrix), converted to glTF axes and metres as `ToGltf` does.
- `game/RiddenDragon.cs` `Carry` -- a carried model's `AnimationPlayer`, which Godot's runtime glTF import builds when the file has animations: set the one animation to loop and play it. A model with none is shown as now. `game/AssetFolder.cs` `Build` is unchanged.
- `tests/Decoding.Tests/SyntheticDragon.cs`, `DragonsTests.cs`, `GlbWriterTests.cs`, `expected/` -- synthetic 27-node bodies, `.MTB` and slot table; record the samples and `extraction-output.sha256` again with `PDZ_RECORD_EXPECTED=1`.
- Version: `Extraction.Version` to 0.5 and every place `AGENTS.md` lists.
- `asset-contract.md` Models ("No animations ..." line), `tools/README.md` dragons paragraph.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Model.cs`, `GlbWriter.cs` -- the optional animation and its writing, with tests for a stepped animation, its accessors, its validator result and an unchanged still model.
- [x] `lib/PdzDecoding/Dragons.cs` -- find the slot table, read slot 0 from the `.MTB`, build each frame's keys deterministically, attach them to every form and the ride; on failure write still models and say why in the dragons line.
- [x] `game/RiddenDragon.cs` -- loop a carried model's animation.
- [x] `tests/` -- the matrix's rows; record the expected files again.
- [x] version places, `asset-contract.md`, `tools/README.md` -- as mapped.

**Acceptance Criteria:**
- The ticket's criteria 1 to 7.
- Given `tools/test.sh`, when it runs, then every suite passes.

## Implementation Notes

- The motion table is found `MotionTableGap` (0xA0) before the body roots match, as the seat table is found 0x50 after it; its pointers count from `1ST_READ`'s load address 0x06008000 and the slots' from the `.MTB`'s, 0x20215000. Both discs: JP at 0x54750, PAL at 0x547EC.
- Determinism: angles go through a 1,025-entry quarter sine table built by Taylor series in 2.60 fixed point (`Int128` only), rounded to 16.16; `Ry*Rx*Rz` and the chain are the decoder's 16.16 `Multiply`. The rest turn is inverted exactly as adjugate over determinant in integers; each key element is one exact integer (checked under 2^53) divided by another once in `double`, then the trace method with `Math.Sqrt`, normalised, w made non-negative. A net9.0 x64 build of the library gave the same JP output byte for byte as net10.0, and `compare-headset.sh` on the Quest (net9.0 ARM64) reported identical.
- Parts keyed: every mesh node whose own node or an ancestor is driven by the pose, the root excluded (form 9: 23 parts, 46 channels). Nodes 0, 1 and 6 keep the `.MDB` matrix, 14 too.
- The beat is all ten forms or none; when it cannot be read the dragons are written still and `DragonSources.NoBeat` ends the dragons line with `, no wing beat: <reason>`; with it the line says `wing beats from 0x... of 1ST_READ.PRG`.
- The writer puts the animation's views and accessors after everything else, so a still model's bytes are unchanged (checked on both discs: the 0.5 models less their animation equal the 0.4 models of `extracted/a3-JP`, apart from the generator).
- Godot's runtime glTF import sets the animation's length to its last key (53/30 s for form 9) and drops tracks whose keys never leave the rest. `RiddenDragon.LoopAnimation` sets the loop and makes the length one key step longer, so the last frame is held 1/30 s and the beat is 1.8 s on form 9. Checked headless on the desktop build of Godot 4.7.2: frame 9 at 0.30 s, 26 at 0.867 s, 53 at 1.78 s, frame 0 again at 1.81 s.
- `Extraction.Version` is 0.5; `ExtractionModesTests` holds a 0.4 folder to be extracted again.

Verification run 2026-11-10: `tools/test.sh` all pass (105 Python, 353 GameLogic, 293 Decoding). `pdz-extract` into `extracted/b22-JP` and `extracted/b22-PAL`: `dragon_ride.glb` and every form the same on both discs, a second JP run identical, `validate-gltf.sh` 0 errors and 0 warnings on all 22 and on the samples (the new `animated` among them). Against the spike's float reading (`extracted/spike-mtb`), every corner of `DRA09I`, `DRA01A` and `DRA07G` posed by the keys lies within 1.3 mm of it at frames 0, 9, 26 and the last; the wing tips of form 9 are 2.56 m above the seat at frame 9 and 1.72 m below it at frame 26. `build-quest.sh` built and launched; the headset extracted in development mode and logged `wing beats from 0x54750`; `compare-headset.sh` reported identical, 434 files. The APK lists `PdzDecoding.dll` and no disc data.

Not yet checked: criterion 2 (the author posing `dragon_ride.glb` at frames 9 and 26 in a glTF viewer) and criterion 5 (the author riding in the headset, the beat and 90 Hz). Nobody wore the headset during the run.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 1 medium and 3 low patched; 1 low rejected; 1 open for the author.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| Criteria 2 and 5 not checked | medium | author | They need the author's look; asked at the end of the build. |
| A key the writer rejects, or a wrapped `Int128` in `Inverse`, loses every dragon instead of giving still ones (criterion 4) | medium | patch | Keys are now checked as they are built and `Inverse` and `KeyRotation` are `checked`, so both fall back to still dragons with the reason; test `Run_RestTurnNotARotation_DragonsStillAndTheLineSaysWhy`. |
| The contract said the animation never moves the root, though a one-part model's root carries the keys | low | patch | Contract reworded: a one-part model's root is its part. |
| The contract's and README's lists of still-dragon cases omitted some, and the README's two paragraphs disagreed | low | patch | Both now give the main cases and "or the motion cannot otherwise be read". |
| Stepped playback relied on Godot's import choosing nearest interpolation | low | patch | `LoopAnimation` sets every track to nearest. |
| A one-frame wing beat would loop with no length | low | rejected | Every form's beat on both discs is 19 frames or more; the fix adds a branch for a case not shown reachable. |

## Design Notes

The rest rotation of a part, `R_rest`, is its `.MDB` chain product. If that product is not orthonormal enough for a transpose to stand in for its inverse, invert it exactly in the chosen arithmetic. Quaternions come from the key's rotation matrix by the usual trace method, which needs only `Math.Sqrt` and division. Normalise them the same way on every runtime.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `pdz-extract` on both discs into `extracted/b22-*`; `tools/validate-gltf.sh` on every `.glb` -- expected: 0 errors; `dragon_ride.glb` the same on both discs
- `tools/build-quest.sh`, then `tools/compare-headset.sh` on the JP cue -- expected: identical
- `git diff --cached --name-only` before commit -- expected: no game data

**Manual checks (if no CLI):**
- Criterion 2: the author poses `dragon_ride.glb` at frames 9 and 26 in a glTF viewer.
- Criterion 5: the author rides the dragon in the headset.
