---
id: 11
type: bug
title: "A colour table with no usable block writes the colour-bank textures black"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: false
risk: medium
severity: P2
---

# A colour table with no usable block writes the colour-bank textures black

## Description

When `EPISODE3.PRG` holds a colour upload table but every pair in it is skipped (a source outside the file, or a destination outside colour RAM), the extractor builds an all-black colour RAM with no notice, writes the mode 4 and mode 0 textures black and lists them in the manifest. In release mode the disc then counts as extracted, so it is never extracted again and its image is deleted. It should behave as when there is no table: those textures left out, with a notice.

## Reproduction

1. Build a synthetic `EPISODE3.PRG` whose only table's pairs all point outside the file (as `tests/Decoding.Tests` builds test discs).
2. Run `Extraction.Run` on a disc holding it and an Episode 3 file with mode 4 textures.
3. Actual: the mode 4 PNGs are written, all black, and the result has a colour RAM with every pair under skipped. Expected: no colour RAM, the "no table" notice, the mode 4 and mode 0 textures left out.

## Cause Hypothesis

`ColourBanks.Build` returns a `ColourRam` whatever the number of pairs applied, and nothing checks for zero.

## Acceptance Criteria

1. **The expected behavior holds**
   **Given** a table whose every pair is skipped
   **When** the disc is extracted
   **Then** the colour-bank textures are left out with a notice naming why, as with no table
2. **Tests cover the condition found and fixed**
   **Given** the decoding tests
   **When** they run
   **Then** a test fails on the defect and passes on the fix, and the partly skipped case still colours from the pairs that applied
3. **Or: no change is needed, with proof**
   **Given** the reproduction
   **When** it is run on the current code
   **Then** the expected behavior already holds, with the evidence in Notes — this supersedes 1 and 2

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool-retrospective.md, finding R1; action A1

## Notes

- Source: carried out of the in-game extraction epic at its close (author, 2026-10-21); kept in the backlog.
