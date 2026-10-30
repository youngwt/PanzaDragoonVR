---
title: 'The ridden dragon sits under the head'
type: 'bugfix'
ticket: ''
created: '2026-10-30'
status: 'built'
baseline_revision: '301e5f907ffa8a1ad45e5f901ab8114de33b9212'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** In the headset on 2026-10-30 the author found the ridden dragon off centre, to their right. The dragon is fixed below the play area's centre and nothing aligns it with the head; the log's board positions show the head was at least 0.52 m from that centre.

**Approach:** Once the head is tracked at launch, and again after a recentre, the game puts the seat directly below the head and leaves it there, for both the placeholder and a supplied model. The seat's height, the XR origin, the scroll, the flyer and the board stay as they are. The asset contract and `tools/README.md` follow.

</frozen-after-approval>

## Implementation Notes

Oneshot: a few lines in two game files and two wording changes.

The seat follows the head in X and Z, not only X, so the rider is also half-way along the back; the tuning story (4.4) offsets from there. Checks: `tools/test.sh` 105 Python, 304 GameLogic, 204 Decoding, all pass; `build-quest.sh` installed and started, with the APK holding no disc data. The one launch log, with nobody wearing the headset, shows the seat at (0.00, 0.00), so the fix is not yet seen in use: a launch with the headset off may place the seat from a default pose.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `tools/build-quest.sh` -- expected: installs and starts, and the log shows the dragon's seat line

**Manual checks (if no CLI):**
- In the headset, the dragon's centre line is under the author at launch and after a recentre, and it does not follow the head between them.

## Review Triage Log

- low, patched: `game/Main.cs`'s `_Process` summary did not say the seat is placed there. Comment updated.
- low, patched in part: `game/Main.Board.cs`'s summary described only the board. Summary updated; `PlaceBoard`, `_boardPlaced` and `OnPoseRecentered` keep their names, as a rename is not worth the churn for two callers.

## Follow-up, 2026-10-30

The author found a recentre did not turn the way of flight, which then ran at an angle to the player. The project used Godot's default reference space, Stage, which is fixed to the play area. `game/project.godot` now sets `xr/openxr/reference_space` to 2, Local Floor, whose origin and facing a recentre resets. One setting and two wording changes, so no separate review.

Headset, 2026-10-30: the author tested the recentre after this change and reported "that works".
