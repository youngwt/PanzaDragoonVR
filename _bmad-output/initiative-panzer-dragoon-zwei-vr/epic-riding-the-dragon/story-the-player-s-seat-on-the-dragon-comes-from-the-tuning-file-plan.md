---
title: "The player's seat on the dragon comes from the tuning file"
type: 'feature'
ticket: '4'
created: '2026-10-30'
status: 'built'
baseline_revision: '020dfa5e8b222471459cd14b6a7120bde1b1f8fd'
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

**Problem:** The player's place on the dragon and the dragon's size are fixed in the game, so the author cannot adjust them in the headset without a rebuild.

**Approach:** Add `seat_forward`, `seat_height` and `dragon_scale` to the tuning file, in all four places its built-ins are kept, read at launch with the same fallback as the other settings. The first two are offsets in metres from the seat and the third a multiple of the model's size, so the built-ins (0, 0, 1) hold no value from the disc.

</frozen-after-approval>

## Implementation Notes

Oneshot: three settings in `Tuning`, a small change in `RiddenDragon`, tests and the three documents.

Positive `seat_forward` moves the player towards the head and positive `seat_height` raises the player, so the dragon itself moves the other way. The scale turns about the seat, so the seat stays under the player at any scale.

## Review Triage Log

Quick lens, one pass: 2 medium and 3 low, all patched.

- medium, patch: the log line said the seat was under the head at the head's position, wrong with `seat_forward`. It now logs the node's position.
- medium, patch: the contract's range guarantee read as covering the seat settings, though `seat_forward` at its ends leaves the player clear of the placeholder. It now excludes them; the wide range is kept because the longest form needs it.
- low, patch: the contract's and README's descriptions of the seat did not mention the settings. Added.
- low, patch: `Tuning`'s summary said all heights were from the floor, and left out the dragon's size. Reworded, there and in the contract.
- low, patch: `RiddenDragon`'s summary did not mention the offsets. Reworded.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `tools/build-quest.sh` -- expected: installs and starts, and the log's tuning line lists the new settings

**Manual checks (if no CLI):**
- With a pushed tuning file, the player moves along the placeholder's back, rises and the dragon rescales; an out-of-range value is on the launch board and the built-in is used.
