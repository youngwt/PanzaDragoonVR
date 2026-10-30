---
title: 'The provisional model rules are confirmed against the dragon'
type: 'chore'
ticket: '6'
created: '2026-10-30'
status: done
baseline_revision: '67d0077298c097ed9d35ebd31d9a5b4e0a137a3a'
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

**Problem:** The asset contract's Models section still marks its rules Provisional and says nothing has been run on the headset, though real dragons now exist. The divisor of 4,000 is a lead from a rider's assumed height, and the dragons have not been seen in the headset.

**Approach:** Check each Provisional rule against the extracted dragons and, where only the author can judge, in the headset with a form listed as `dragon.ride`. Remove every Provisional mark, changing a rule, the writer, its tests and the expected files only where the author agrees to a change.

</frozen-after-approval>

## Implementation Notes

Oneshot: expected to be wording in the contract, plus a constant and its tests if the divisor changes.

Evidence from the extracted forms (kept out of git beyond this summary): every form fits one atlas well under the limit; three-cornered polygons draw as the repeat rule says; only `opaque` and `cutout` occur on the dragons.

Author, 2026-10-30, in the headset with form 1 as `dragon.ride`: the wing edges look great, with no black rectangles, so the `cutout` rule holds in the game and the black was F3D's. Shading stays out: no vertex colours, `COLOR_0` reserved, as backlog 19.

Author, same day: the size is good, so the divisor stays 4,000, and the hard-edged pixels are fine. `half` and `mesh` lose the mark with a note that no dragon uses them (`E03CMN` has `mesh`). Materials stay double-sided, the reason corrected now that the facing is known.

No rule changed in effect, so the writer, its tests and the expected files are unchanged. Only form 1 was seen in the headset. The preview used a hand-written manifest with the disc image moved aside; the image is back in `disc/`, and the next launch re-extracts in development mode.

## Review Triage Log

Quick lens, one pass: 1 medium and 1 low patched; 1 rejected.

- medium, patch: the contract said `mesh` was not seen on a model, but `zwei-disc-formats.md` finds it in `E03CMN`. Reworded in the contract, epic and this plan.
- low, patch: the contract's intro said all rules were checked, though no dragon exercises the second atlas, `half` or `mesh`. The intro now says so.
- low, rejected: `deferred-work.md`'s entry on the Models section is now out of date. That list is append-only, and the ticket and this plan record the outcome.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `grep -c Provisional asset-contract.md` -- expected: 0

**Manual checks (if no CLI):**
- In the headset, the author sees the dragon's size, its wing transparency and its texture filtering, and agrees to each rule.
