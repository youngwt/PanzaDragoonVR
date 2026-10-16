---
id: 4
type: spike
title: "How the build moves off .NET 9 before its support ends on 2026-11-10"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: medium
---

# How the build moves off .NET 9 before its support ends on 2026-11-10

## Description

Answers which Godot and .NET versions the build should move to before .NET 9 support ends on 2026-11-10, and what the move costs. Godot 4.7.2's Android export needs .NET 9, and Godot plans .NET 10 export templates for 4.8. The author decides; the answer is recorded in `tools/README.md` and the stack notes, and the move becomes a story.

## Acceptance Criteria

Verify: the chosen versions, their release status, and a trial build on the Quest 3 (or the reason none is possible yet) are recorded, and the author has decided.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md, action A7
- toolchain — tools/README.md, What it installs
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md, Game

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Hitl: the author decides, and a trial build needs the headset.
- Open question: whether Godot 4.8 with .NET 10 export templates is released by 2026-11-10.
