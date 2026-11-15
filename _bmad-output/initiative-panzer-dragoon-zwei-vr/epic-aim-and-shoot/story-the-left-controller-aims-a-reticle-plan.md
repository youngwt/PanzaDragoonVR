---
title: 'The left controller aims a reticle'
type: 'feature'
ticket: '1'
created: '2026-11-14'
status: 'built'
baseline_revision: '0a11b024fb13153c68049bec174e9df8ea386191'
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

**Problem:** The player has no way to aim. The controllers exist only in development mode, made by the tuning list, so nothing in the game follows the hands.

**Approach:** Put both controllers in the scene in every mode, with the tuning list using them. Draw a reticle where the left controller's aim ray meets a sphere of a fixed, built-in radius centred on the head, as `stack.md` says ("a 2D sprite at a fixed distance from the player, as if on a sphere around them"). This is the epic's tracer bullet (entry 5.1), toward CAP-6.

## Boundaries & Constraints

**Always:**
- The aim is the left controller's OpenXR aim pose (`aim_pose`). It points along the controller, not the grip.
- The sphere's radius and the reticle's size are built-in constants, not tuning settings (epic decision, 2026-11-11).
- The reticle is drawn over everything, so the forest, the dragon and the cube never hide it. It always faces the head, and it is a shape the game draws itself, not an image file.
- The reticle maths lives in `game/Logic/`, engine-free and tested in `GameLogic.Tests`.
- The tuning list behaves as before: the right controller's stick, A and B, and its label above the left controller.

**Never:**
- No colour change, target test, gun, firing or lock-on; those are entries 5.2 to 5.7.
- No new tuning setting.
- No handedness option.
- No reticle on the flat-screen (non-VR) run.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Aiming | Left controller tracked, aim ray from inside the sphere | Reticle on the sphere where the ray leaves it, facing the head | None |
| Ray from outside the sphere | Controller farther than the radius from the head (not reachable in play) | The ray's far meeting with the sphere if it meets it in front, else no reticle | Hidden, no error |
| Left controller not tracked | Controller asleep, lost or off | Reticle hidden until tracking returns | None |
| Flat screen | OpenXR not initialised | No reticle | None |
| Development mode | Marker present | Tuning list works as before on the same two controllers | None |

</frozen-after-approval>

## Code Map

- `game/Main.TuningMenu.cs` -- `StartTuningMenu` creates both `XRController3D`s (`Tracker` `left_hand` and `right_hand`) and hangs `_tuningList` on the left one. Change it to use the scene's controllers; the rest of the list is unchanged.
- `game/Main.tscn` -- `XROrigin3D` holds only `XRCamera3D`. Add `LeftHand` and `RightHand` `XRController3D` nodes (`tracker` left/right, `pose` `aim_pose`).
- `game/openxr_action_map.tres` -- `aim_pose` is already bound for both hands. No change.
- `game/Main.cs` -- `_Ready` and `_Process`. Add a reticle start (after `StartOpenXr`, VR only) and a per-frame update; update the class summary's list of jobs. Follow the partial-class pattern: a new `game/Main.Reticle.cs`.
- `game/Logic/BoardPlacement.cs` -- the model for an engine-free placement helper: floats in, a `readonly record struct` out, null when there is no answer, consts for the built-in values.
- `tests/GameLogic.Tests/BoardPlacementTests.cs` -- the model for the new tests (`Subject_Behaviour` names).
- `game/Logic/` is compiled straight into `GameLogic.Tests` (`GameLogic.Tests.csproj`), so a new file there needs no project change.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/ReticlePlacement.cs` -- the point where a ray (origin, direction) leaves a sphere (centre, radius `Radius`), null when it doesn't meet it ahead. Add the constants `Radius` and `Size` (the reticle's width in metres). -- engine-free and testable.
- [x] `tests/GameLogic.Tests/ReticlePlacementTests.cs` -- straight ahead, sideways, up, a non-unit direction, an origin off-centre, from outside the sphere (hit ahead, miss, behind), a zero direction. -- the matrix's rows.
- [x] `game/Main.tscn` -- add the two controllers.
- [x] `game/Main.TuningMenu.cs` -- use the scene's controllers.
- [x] `game/Main.Reticle.cs` -- build the reticle: a camera-facing quad with an unshaded material drawn without a depth test, its ring drawn by a small shader or a generated image. Each frame, place it from the left hand's global transform (its -Z is the aim) and the camera's global position, or hide it.
- [x] `game/Main.cs` -- call both, and update the summary.

**Acceptance Criteria:**
- Given the headset with the left controller in hand, when the author sweeps it across the forest, the dragon and the circling cube, then the reticle follows the controller's pointing at a constant distance, over everything, at 90 Hz.
- Given development mode, when the author presses B on the right controller, then the tuning list shows above the left controller and works as before.
- Given `tools/test.sh`, when it runs, then every suite passes.
- Given the build, when the author records a fifteen-second clip of the sweep, then it is milestone 4's clip.

## Implementation Notes

- The reticle is a white ring and centre dot on a dark, part-transparent band, drawn by a shader on a `QuadMesh` (render priority 5), so the frame-rate readout and the tuning list (priority 10) still draw over it. It faces the head with its up taken from the head's own up.
- It hides when `LeftHand` is not active or has no tracking data, or when the ray gives no point.
- The tuning list's controllers used Godot's default pose before; both now use `aim_pose`, so the list sits 0.12 m above the aim pose rather than the default pose. Worth a look in the headset.
- The shader could not be compiled in the builder's environment. Compiled and drawn afterwards on the desktop build of Godot 4.7.2 in a throwaway scene under `extracted/`: the ring renders over a box in front of it.
- Verification 2026-11-14: `tools/test.sh` all pass (105 Python, 362 GameLogic, 294 Decoding); `dotnet build game/PanzaDragoonVR.csproj` 0 warnings, 0 errors; `tools/build-quest.sh --no-install` built, the APK lists `PdzDecoding.dll` and no disc data.

- Headset, 2026-11-15: the first install showed no reticle. Godot names the OpenXR action `aim_pose`'s pose `aim` on the tracker (a temporary log line showed the tracker's poses `default`, `aim`, `grip` and `palm_pose`, all tracked, while the node with `pose = aim_pose` reported no tracking), so both controllers in `Main.tscn` now use `aim`. That also put the tuning list's label back on the controller.
- Author, 2026-11-15, with the fixed build: "they all look good", one word for the four items asked: the sweep across the forest, the dragon and the cube at a constant distance and 90 Hz; the reticle hiding with the controller put down out of view; the tuning list in development mode; and the milestone 4 clip. The log showed 90 Hz on target.

## Plan Change Log

- 2026-11-15, after the author's checks: on the author's word ("can you move the tuning list onto the right hand"), the tuning list's label hangs from the right controller, the one that drives it, leaving the left hand to the reticle and the gun. This goes against the frozen Always "its label above the left controller"; it is its own commit, and the asset contract and `tools/README.md` say so.

## Review Triage Log

Quick lens, one pass: 1 low patched; 1 low rejected; 1 maybe-false deferred to the headset look; 1 not a code finding.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| A controller that has lost optical tracking but still reports an inferred pose may keep the reticle showing | maybe-false | headset | `GetHasTrackingData` is false only with no pose; whether Quest's inferred pose holds the reticle visibly frozen is settled by the author putting the controller down out of view. Hiding on low confidence could also make it flicker in normal play, so no guess is coded. |
| `OnSphere`'s comment says "any length", the code treats under 0.001 as none | low | patch | Comment now names the 0.001 threshold. |
| The tuning list's label is now placed from the aim pose, not the default pose, so it sits a little differently | low | rejected | Cosmetic, same offset above the controller and billboarded; the author's development-mode check covers it. |
| Headset criteria not yet met | -- | author | The author's checks, asked at the end of the build. |

## Design Notes

Built-in values: radius 6 m, beyond the board (2.2 m) and the cube's path (4 m), so the reticle reads as "out there" without the eyes refocusing far. Size 0.25 m, about 2.4° across. The author judges both in the headset, and they're changed in code if needed.

The head moves a little within the sphere while the controller's ray stays put, so the sphere's centre is the camera's position each frame, not the origin. This matches "around the player".

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `dotnet build game/PanzaDragoonVR.csproj` -- expected: 0 warnings, 0 errors
- `tools/build-quest.sh` -- expected: installs and starts; the APK lists `PdzDecoding.dll` and no disc data

**Manual checks (if no CLI):**
- Headset (the author): the reticle follows the left controller at a constant distance, over everything, at 90 Hz; it hides when the controller is put down out of tracking. The tuning list works in development mode. The milestone 4 clip is recorded.
