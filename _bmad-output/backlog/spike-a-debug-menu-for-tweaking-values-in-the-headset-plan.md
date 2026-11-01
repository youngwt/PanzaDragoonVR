---
title: 'A debug menu for tweaking values in the headset'
type: 'chore'
ticket: '20'
created: '2026-10-31'
status: 'built'
baseline_revision: 'a5470f7fdf526879c976d9f15260e865cfc7e4c8'
route: 'oneshot'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Changing a tuning value means editing a file on the PC, pushing it and relaunching, which is slow when judging things like the seat by eye. It is not known how a menu in the headset should be drawn and driven, which settings can change live, how values get back to the PC, or whether players see it.

**Approach:** Answer those four from the code and a throwaway trial in the headset that moves settings with a controller and saves them. The trial is never committed. The author then decides whether to build it, and any story that follows goes on the backlog.

</frozen-after-approval>

## Implementation Notes

Oneshot: the trial is one uncommitted partial class of about 100 lines, removed afterwards. What is committed is this plan's answers and any story.

What the code says, before the trial:

- Controller input: none in the game, and no controller nodes in the scene, but `game/openxr_action_map.tres` already binds `primary`, `ax_button` and `by_button` on the Touch controller, so no action map change is needed.
- Live settings: `flight_speed` and `canopy_speed_scale` are read every frame in `Main.Scroll`. `floor_depth`, `canopy_height` and the seat's three go through `PlacePlanes`, which can run at any time. `floor_tile` and `canopy_tile` are read every frame for the scroll but given to the shader only in `Dress`, so they also need the `tiles` parameter set again.
- Writing: the game already writes to the asset folder when extracting, and `tuning.json` sits at its top. `Tuning`'s setting table is private, so a real menu would make it public rather than copy it.

Trial: `game/Main.DebugTrial.cs`, with two lines in `Main.cs`. Right stick up and down picks a setting, left and right changes it, A saves `tuning.json` to the asset folder, B shows or hides the list, which is held on the left controller. It covers six settings, one or more of each kind above.

## Verification

**Manual checks (if no CLI):**
- In the headset, each setting in the trial changes the scene at once; A writes a `tuning.json` that the next launch reads; the list can be read and does not get in the way.
- After the trial: `tools/push-tuning.sh --remove`, the trial's code gone, a clean build installed.

## Answers

Author, 2026-11-01: in the headset the trial's controls worked as expected and A saved. Build it, in the development build only (backlog 21).

- **Drawn and driven:** a text list held above the left controller, driven by the right stick and A and B. The author found it worked; the alternative of a board in the world was not needed.
- **What can change live:** all nine settings. The trial moved six, covering each way a setting reaches the scene; `canopy_height`, `canopy_speed_scale` and `canopy_tile` take the same routes as `floor_depth`, `flight_speed` and `floor_tile`, and were not moved.
- **Back to the PC:** the game saves `tuning.json` in the asset folder, and a new tool pulls it, not yet written or tried. Saving was seen in the headset. Not seen: the next launch reading a saved file, or a pull, as the headset was not connected when this was written. The story's checks cover both.
- **Who sees it:** the author asked for the development build. That is taken to mean the asset folder's `development` mode, which is decided by a marker file whatever the build, so anyone who adds the file gets the menu, and using it means extracting again on each launch when a disc image is present. This stays open until the author confirms it; the story records it.

A surprise on the way: early controller readings are empty until the controllers wake (the profile reads `none`), which may be why the first trial did nothing. The cause of that first failure was not established.

The trial's code is removed from the working tree and was never committed. Still to do on the headset when it is next connected: install a clean build, and remove the trial's `tuning.json` once the author has no use for its values.

Of the checks above, these were not met when this was written: the next launch reading a saved file, the removal of `tuning.json` and the clean build. The checks of each setting's effect and of the list's readability rest on the author's "worked as expected", with no separate word on each.

## Review Triage Log

Quick lens, one pass: 6 medium and 1 low patched.

- medium, patch: the plan's manual checks are not all met. Recorded above; the ticket's own criterion is met.
- medium, patch: "a tool pulls it" read as tried. Now says it is not written; the story notes the sandbox limit.
- medium, patch: the story's criterion on `push-tuning.sh`'s check could not be run without pushing. Replaced with a byte-for-byte copy and a round-trip test through `Tuning.Parse`.
- medium, patch: the story missed the contract's second "never changed" and `AGENTS.md`'s section. Added.
- medium, patch: "players never see it" was stronger than the marker file delivers, and left out the extraction cost. Reworded in both.
- medium, patch: no writer test required. Added with the round trip.
- low, patch: the trial's rates and stick behaviour were not recorded. Added to the story.
