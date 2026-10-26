---
title: 'A colour table with no usable block writes the colour-bank textures black'
type: 'bugfix'
ticket: '8'
created: '2026-10-25'
status: done
baseline_revision: '4005694932af361a7223c435c56d55fbcda4b653'
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

**Problem:** When `EPISODE3.PRG`'s chosen colour upload table has pairs but every one is skipped (source outside the file, or destination outside colour RAM), `ColourBanks.Build` still returns an all-black colour RAM, so the mode 4 and mode 0 textures are written black and listed in the manifest, and in release mode the disc counts as extracted and its image is deleted.

**Approach:** In `ColourBanks.FromProgram`, treat a table with no pair applied as no table: return no colour RAM and a one-line notice naming the table and why, so extraction leaves modes 0 and 4 out with that notice, as it already does for "no table". A table with some pairs applied keeps colouring from those, as today.

</frozen-after-approval>

## Implementation Notes

Oneshot: a check of a few lines in `lib/PdzDecoding/ColourBanks.cs` plus tests; the extraction path already handles a null colour RAM with a notice.

- `ColourBanks.FromProgram` returns no colour RAM and the notice "EPISODE3.PRG's colour table at 0x<offset> has no block inside the file and colour RAM" when every pair of the chosen table was skipped. A table always has at least one pair (`FindTables` needs one to match), so the check never mistakes an empty table.
- Tests: `ColourBanksTests.FromProgram_EveryPairSkipped_GivesNoColoursAndANotice` and `ExtractionTests.Run_TableWithEveryPairSkipped_LeavesModes4And0OutAsWithNoTable`; both fail on the old code (checked by stashing the fix) and pass with it. The partly skipped case keeps its existing tests (`Build_PairOutsideTheFileOrColourRam_IsSkippedAndTheRestApplied`, `Run_BadPair_IsSkippedTheRestAppliedAndNotedInTheResult`), unchanged and passing.
- The real discs are unaffected: their chosen table applies all five pairs.
- `tools/test.sh`: 97 Python, 296 GameLogic, 120 Decoding, all pass.

## Review Triage Log

Quick lens, 2026-10-25: high 0, medium 0, low 3, false 0, maybe-false 0. The reviewer confirmed the check, the notice's form and both new tests.

- low, patch: `tools/README.md` listed every no-colours notice for `pdz-extract` and the game's `extracted` line but not the new one. Added in both places.
- low, patch: the `Extraction.Run` summary and `DiscFileResult`'s `LeftOut` doc named only a missing file or table. Both now include a table with no pair that can be applied.
- low, accepted: the ticket's Reproduction expects "the 'no table' notice", while the fix gives its own notice naming the table. Acceptance criterion 1 asks for "a notice naming why, as with no table", which the more specific notice meets, and the plan's Intent chose it; recorded here rather than in the ticket file, which the build does not edit.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass, with a test that fails on the old code.
