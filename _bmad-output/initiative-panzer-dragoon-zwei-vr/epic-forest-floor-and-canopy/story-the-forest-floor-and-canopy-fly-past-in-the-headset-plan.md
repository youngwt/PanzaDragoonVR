---
title: 'The forest floor and canopy fly past in the headset'
type: 'feature'
ticket: '5'
created: '2026-10-26'
status: done
baseline_revision: '67aeece3862025a799d61e2f2c85e4f760506bbf'
route: 'oneshot'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extracted floor and canopy now show in the headset, but at the placeholders' sizes (2 m for the whole 512-pixel floor page, 20 m for the canopy), so they look tiny and busy; nobody has yet set the depth, height, sizes and speed that make the scene look like Zwei's Episode 3 (CAP-4).

**Approach:** A tuning session with the author in the headset: push candidate tuning files with `tools/push-tuning.sh`, relaunch, and adjust from what the author sees until the floor loops with no seam, the canopy sits overhead as a flat backdrop, and it looks like Episode 3. Then make the author's chosen values the built-ins (`game/Logic/Tuning.cs`, `tools/tuning.example.json`, the README table, the asset contract's Tuning section), so a player with no tuning file gets them.

</frozen-after-approval>

## Implementation Notes

Oneshot: the work is the author's judgement in the headset; the code change is the built-in values and their documentation.

- **Headset session, 2026-10-26, with the author:** try A (floor 6 m down, canopy 30 m up, 6 m/s, floor image 24 m, canopy image 48 m): the floor "looks great", the canopy "very repetitive". The author compared with Mednafen's Episode 3 save state: the level is claustrophobic and the canopy much lower. Try B, the canopy at 10 m: "much better"; the author chose B as the built-ins ("yes").
- Built-ins now 6, 10, 6, 1, 24, 48 in `game/Logic/Tuning.cs`, `tools/tuning.example.json`, the README's table and log example, and the asset contract's example and table; the tests that quote the built-ins (`Describe`, the out-of-range messages, the canopy speed 6 x 8.4 / 7.6) follow, and the some-fields test now sets 8 m/s so it still differs from the built-in.
- The headset's `tuning.json` was removed and the build installed: the log reads `tuning (built-in, no tuning.json): floor_depth 6, canopy_height 10, flight_speed 6, canopy_speed_scale 1, floor_tile 24, canopy_tile 48`.
- Seen in Mednafen: the forest's sides are walls of trunks and foliage, most likely VDP1 models (the `E03CMN` textures are trunks, vines and foliage); out of this epic's Boundaries ("no other scenery"), noted for a later epic.
- `tools/test.sh`: 105 Python, 296 GameLogic, 152 Decoding, all pass; the game builds with 0 warnings.

## Review Triage Log

Quick lens, 2026-10-26: high 0, medium 0, low 2, false 1, maybe-false 0. The reviewer checked the six values agree everywhere, the test strings, the canopy speed (6 x 8.4 / 7.6), that the range-end and some-fields tests still differ from the built-ins, and that the flyer stays below the 10 m canopy.

- low, patch: `tools/README.md` "Your own images in the headset" still gave the old default tile sizes (2 m and 20 m). Now 24 m and 48 m.
- low, patch: `Tuning.Describe`'s summary quoted the old built-ins. Now the new ones.
- false: the plan's test count read 97 Python in the reviewed diff; the working copy, which is what is committed, already says 105.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass after the built-ins change.

**Manual checks:**
- The author in the headset: the floor scrolls endlessly with no seam, the canopy overhead, both looking like Episode 3; a pushed tuning file changes the motion without a rebuild.
