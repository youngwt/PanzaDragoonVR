---
id: 12
type: story
title: "Extraction edge cases are reported truthfully and match across PC and Quest"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: false
risk: low
---

# Extraction edge cases are reported truthfully and match across PC and Quest

## Description

Four small fixes from the in-game extraction retrospective: the board's heading is true when moving files into place fails part way (not "Nothing was extracted"); a display error after a successful extraction is not reported as a failed extraction; a cue sheet's `FILE` names fall back to a case-insensitive match in the cue's folder, so the Quest finds `Game.bin` for `GAME.BIN` as Windows does (and deletes it in release mode); and `tools/compare-headset.sh` removes the folders it empties when setting stray files aside.

## Acceptance Criteria

Verify: tests show each of the four cases fixed; `compare-headset.sh` reports identical with a stray file in a subfolder of the headset's `images/`.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool-retrospective.md, findings R2, R3, R4, R9; actions A1 and A5

## Notes

- Source: carried out of the in-game extraction epic at its close (author, 2026-10-21); kept in the backlog.
- Added from the forest floor and canopy retrospective (F6), carried by the riding the dragon retrospective (A6), 2026-11-04: split `lib/PdzDecoding/Extraction.cs` when this ticket edits it. It has grown past 450 lines, through epics 3 and 4.
