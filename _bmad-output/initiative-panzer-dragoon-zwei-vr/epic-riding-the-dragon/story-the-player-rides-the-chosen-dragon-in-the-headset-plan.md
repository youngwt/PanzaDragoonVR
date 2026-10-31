---
title: 'The player rides the chosen dragon in the headset'
type: 'feature'
ticket: '7'
created: '2026-10-30'
status: done
baseline_revision: '41699e34afa94a36c4a972d7a9cabda67645c073'
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

**Problem:** The extraction writes ten dragon forms, but the game asks only for `dragon.ride`, so with a disc extracted the player still rides the grey placeholder.

**Approach:** The author chooses a form. The extraction also writes it as `dragon.ride` in `models/dragon_ride.glb`, on the PC and the headset, with the root node named `ride`. The author sets the seat in the headset through the tuning file, and those offsets become the built-ins.

</frozen-after-approval>

## Implementation Notes

Oneshot: a constant for the chosen form, one more model written through staging and the manifest, tests, and the contract, README and built-ins.

Author, 2026-10-30: chose form 9, `DRA09I`. In the headset, "happy for now" with the seat at the rider's position, so the built-ins stay 0, 0 and 1; the seat may be revisited once values can be changed in the headset (backlog 20).

`Extraction.Version` went to 0.4, so a folder from 0.3 gains `dragon.ride` in release mode. The expected files were recorded again; only the version changed.

Checks: `tools/test.sh` all pass; `tools/validate-gltf.sh` 0 errors on `dragon_ride.glb`; both discs give the same `dragon_ride.glb`; `tools/compare-headset.sh` identical, before the review's comment and wording patches.

Open at commit: the author said only "happy for now", so the wings, head, tail and steady flight on form 9 want the author's explicit word before this is marked done.

Author, 2026-10-31: asked for the wings, head, tail and steady flight to be confirmed, replied by marking the story done.

## Review Triage Log

Quick lens, one pass: 8 low and 1 medium patched; 1 deferred.

- low, patch: README, `-h` text and doc comments stale on `dragon.ride` and the version (five findings). Corrected.
- low, patch: README's sample output did not match the program. Corrected.
- low, patch: no test case for a 0.3 folder in release mode. Added.
- medium, patch: README's total of assets, a count from the disc, was re-recorded by this diff against `AGENTS.md`. That sentence of counts is removed, and the sample's total is now `<n>`.
- low, patch: this plan did not record its checks. Recorded above; the author's look is left open.
- defer: other values from the disc remain in README's sample output, from before this change.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `tools/validate-gltf.sh` on `dragon_ride.glb` -- expected: 0 errors
- `tools/build-quest.sh`, then `tools/compare-headset.sh` -- expected: identical

**Manual checks (if no CLI):**
- In the headset, the chosen dragon sits under the author, wings either side, head in front and tail behind, and stays in place while the forest scrolls.
