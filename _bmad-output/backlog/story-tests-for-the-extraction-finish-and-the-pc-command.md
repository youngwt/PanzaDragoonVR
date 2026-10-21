---
id: 13
type: story
title: "The extraction's finish decision and the PC command are tested"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: false
risk: medium
---

# The extraction's finish decision and the PC command are tested

## Description

The decision at the end of an extraction (which report line to show, and whether release mode deletes the disc image) moves from `game/DiscExtraction.cs` into engine-free `game/Logic` with tests, so a wrong flag cannot delete an image after a failure unnoticed; `pdz-extract` catches every exception into its `error:` line and exit 1, with tests for its arguments and exit codes; and the extractor's recorded manifest is run through the shared asset-rule checks of the game and the PC.

## Acceptance Criteria

Verify: tests fail if the finish decision deletes after a failure, if `pdz-extract` exits other than 0 or 1, or if the extractor's manifest breaks a shared rule; behaviour on the headset is unchanged.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool-retrospective.md, findings V1, V2 and A-pattern; action A2

## Notes

- Source: carried out of the in-game extraction epic at its close (author, 2026-10-21); kept in the backlog.
