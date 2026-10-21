---
title: 'The launch report board stands in front of the player'
type: 'feature'
ticket: '13'
created: '2026-10-21'
status: 'built'
baseline_revision: '47a56413faad33593bd2e284055fd16dd51985dc'
route: 'oneshot'
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

**Problem:** The launch report and progress board is a node at a fixed spot, (0, 1.6, -2.2) in `game/Main.tscn`, ahead of the play area's origin. A player who starts the app facing another way finds it behind them (the author, 2026-10-18).

**Approach:** Once the headset reports a tracked head pose, place the board 2.2 m in front of the head's horizontal facing direction, at the head's height, turned to face the player; do the same again when the player recentres the view. The placement is engine-free maths with a test; until a tracked pose arrives, and on a desktop run without VR, the board stays where it is today. Nothing about what the board shows changes.

</frozen-after-approval>

## Implementation Notes

Oneshot: one small engine-free class, its test, and the hook in `Main`.

- `game/Logic/BoardPlacement.cs`: `InFrontOf(head, forward)` gives the board's position 2.2 m along the head's horizontal facing direction, at the head's height, and the yaw that turns the board's front (+Z) back at the head; null when the head looks straight up or down. `tests/GameLogic.Tests/BoardPlacementTests.cs`: six tests (the old spot for a forward-facing head, facing the other way, an offset head looking along +X, looking downwards, straight up and down).
- `game/Main.cs`: while not yet placed, `_Process` asks `PlaceBoard()`, which waits for the `head` tracker's pose to have tracking data, then sets the `LaunchReport` node's transform from the camera and logs `board placed in front of the head at (x, y, z)` (invariant culture). `PoseRecentered` clears the flag so the board is placed again; unsubscribed in `_ExitTree`. Without OpenXR nothing changes, so the desktop keeps the scene's spot.
- **Headset, 2026-10-21, with the author:** first launch, development mode, a clean extraction: "board placed in front of the head" at launch and again after the author recentred, but nothing to read (the board shows only when there is something to report). Then, with `disc/` and `manifest.json` moved aside, the author started the app facing sideways: logged `board placed ... at (1.82, 2.00, -0.24)`, the "Running on placeholders" notice, and the author saw the board in front ("it works"); two recentres placed it again. The folder was restored afterwards, with development mode still on.

## Review Triage Log

Quick lens, 2026-10-21: high 0, medium 0, low 0, false 1, maybe-false 0. The reviewer checked the maths, the scene, the desktop guard, the log line and the signal cleanup as correct.

- false: after a recentre the board might be placed from the head pose in the old reference space, if the runtime applies the new space a frame late. The headset log disproves it here: after each of the author's recentres the board was placed at (0.12, 2.00, -1.63) and (0.08, 2.00, -1.60), just where a head at the new space's origin facing -Z puts it (along -Z, about 1.6 m ahead of a head standing slightly behind the origin), not at the sideways spot (1.82, 2.00, -0.24) of before. The author confirmed the board in front ("it works").

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.

**Manual checks:**
- On the headset, start the app facing away from the play area's forward direction: the board is straight ahead at eye height.
