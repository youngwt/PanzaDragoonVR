---
id: 10
type: spike
title: "Why a repeating texture comes out clamped on the Quest 3"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: low
---

# Why a repeating texture comes out clamped on the Quest 3

## Description

Answers why a texture set to repeat on the ground and sky planes was drawn clamped to its edge on the Quest 3 but not on the desktop, which the game works around with its own tiling shader. The answer is recorded in `tools/README.md` and decides whether the workaround stays.

## Acceptance Criteria

Verify: the cause, or the experiments that ruled causes out, is recorded, and the author has decided whether to keep the workaround.

## References

- parent — none
- plan — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-test-scene-at-90-hz-plan.md, 2026-10-09 notes (Sky fixed)
- readout — tools/README.md, the tiling note

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Hitl: needs the headset.
- Low priority: the workaround holds; it matters if later epics need Godot's stock materials to repeat.
