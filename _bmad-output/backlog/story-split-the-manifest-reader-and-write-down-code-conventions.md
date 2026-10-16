---
id: 3
type: story
title: "The manifest reader is split by job and the code conventions are written down"
parent: none
covers: []
after: [2]
assignee: ""
refined: false
hitl: false
risk: low
---

# The manifest reader is split by job and the code conventions are written down

## Description

`AssetManifest.cs` (about 550 lines doing six jobs) is split into reading, the rules and the file checks, with no change in behaviour, and the conventions the epic settled are written down: the error prefix and exit codes of the PC tools, and the `PDZ-` tag with no closing full stop on game log lines.

## Acceptance Criteria

Verify: every test passes unchanged, the launch report and log lines are the same as before on a desktop run, and the conventions are recorded where agents will read them.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md, findings A-size and A-pattern; action A6

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Waits on story 2, so the shared cases guard the split.
