---
title: 'Every allowed tuning value gives a sane picture'
type: 'feature'
ticket: '17'
created: '2026-10-27'
status: 'built'
baseline_revision: 'da0b6b536efeb1d384a3a50e0c02c89d8071299e'
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

**Problem:** The tuning ranges accept values that break the scene (forest retrospective R2, R3): a canopy at 2 m cuts the flyer (whose path reaches about 2.65 m) and sits at the head; tile sizes of 0.1 m make 20000 tiles on the 2000 m planes, where the image jitters; 100 m/s moves the image so many tiles a frame at 90 Hz that it reads as still or backwards; a floor 1000 m down shows the planes' edge 45 degrees below the horizon.

**Approach:** Narrow the ranges in `game/Logic/Tuning.cs`, the asset contract's Tuning table and the README table, keeping the author's built-ins inside: `floor_depth` 3 to 50 (the planes' edge at most about 3 degrees below the horizon), `canopy_height` 4 to 100 (above the flyer and a standing head), `flight_speed` 0 to 30, `canopy_speed_scale` 0 to 2, `floor_tile` 4 to 500, `canopy_tile` 8 to 500. "Sane" is judged for each setting at its range end with the others at the built-ins, and for two files that put every setting at its low end and every setting at its high end together; both stay well under a quarter of a tile a frame at 90 Hz.

</frozen-after-approval>

## Implementation Notes

Oneshot: six range values, their tests and the two tables.

- `Tuning.cs` ranges: `floor_depth` 3-50, `canopy_height` 4-100, `flight_speed` 0-30, `canopy_speed_scale` 0-2, `floor_tile` 4-500, `canopy_tile` 8-500. The contract's Tuning table and its "Ranges include their ends" line, and the README table, follow.
- Tests: `RangeEnds_AreAllowedAndJustBeyondAreNot`'s rows take the new ends; the out-of-range messages quote them; `AllGood_UsesEveryValue` used a `floor_tile` of 3, now out of range, so it uses 5.
- Worst case at the high ends together: the canopy at 30 x (100 - 1.6) / (50 + 1.6) x 2 = 114 m/s, 1.3 m a frame at 90 Hz against a 500 m tile; the floor 0.33 m a frame. At the low ends with the built-in speed: the floor 0.067 m a frame against 4 m, 500 tiles across the plane.
- Range-end files for the headset: `extracted/b17/low.json` (every setting at its low end) and `extracted/b17/high.json` (every setting at its high end).
- `tools/test.sh`: 105 Python, 303 GameLogic, 158 Decoding, all pass; the game builds with 0 warnings.
- **Headset, 2026-10-27, with the author**, each file confirmed in the log's tuning line: `low.json` (3, 4, 0, 0, 4, 8) "looks good"; `small-fast.json` (6, 10, 30, 1, 4, 8) reads as forward motion ("yes"); `high.json` (50, 100, 30, 2, 500, 500) "looks ok". The headset's `tuning.json` was then removed, so it runs on the built-ins.

## Review Triage Log

Quick lens, 2026-10-27: high 0, medium 0, low 4, false 0, maybe-false 1. The reviewer checked the six ranges match across `Tuning.cs`, the two tables and the tests, every built-in inside its range, and each single end and the all-high file under a quarter of a tile a frame.

- low, patch: the contract's "mixing ends" example claimed more than a tile a frame without the canopy speed scale at 2 (at 1 it is 0.89 of an 8 m tile). It now names the scale.
- low, patch: the low-ends file holds both speeds at 0, so it never shows the small tiles moving. A third file, `extracted/b17/small-fast.json` (the smallest tiles at the top speed, the rest built-in: the floor 0.33 m and the canopy 0.37 m a frame), joins the headset check.
- maybe-false, recorded: at `canopy_height` 100 the canopy's edge is about 5.6 degrees above the horizon, against about 3 degrees for the floor at 50. Whether that is in view is settled by the author with the high-ends file; the contract now says it.
- low, patch: `LaunchReportTests.cs:307` quoted the old range and built-in in a pass-through string. Updated.
- low, rejected: the tuning story's plan still states the old ranges; it is the finished story's record.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass with the new range ends.

**Manual checks:**
- The author in the headset with a low-ends and a high-ends tuning file: the canopy above the head and the flyer, no jitter, the scroll reading as forward motion, no plane edge in view.
