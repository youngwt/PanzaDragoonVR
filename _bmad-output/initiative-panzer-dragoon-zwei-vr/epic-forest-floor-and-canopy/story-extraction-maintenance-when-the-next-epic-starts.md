---
id: 7
type: story
title: "Extraction code duplicates are pinned and Main.cs is split"
parent: epic-forest-floor-and-canopy
covers: []
after: []
assignee: ""
refined: false
hitl: false
risk: low
---

# Extraction code duplicates are pinned and Main.cs is split

## Description

Cleanup best done when the next epic touches this code: a test pinning `ExtractionModes.ExtractorPrefix` to the library's generator, one shared one-line message helper inside `game/Logic` in place of three, and `game/Main.cs` (444 lines: scene, extraction watch, board placement, OpenXR session, refresh rate) split by job, with no change in behaviour.

## Acceptance Criteria

Verify: every test passes unchanged; the prefix test fails if the two strings drift; `Main.cs` holds one job per file.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-forest-floor-and-canopy/epic-forest-floor-and-canopy.md
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool-retrospective.md, findings A-dup and A-size; action A4

## Notes

- Source: carried out of the in-game extraction epic at its close (author, 2026-10-21); kept in the backlog.
- Moved into the forest floor and canopy epic (author, 2026-10-22).
