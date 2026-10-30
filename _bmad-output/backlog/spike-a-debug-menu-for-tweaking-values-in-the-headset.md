---
id: 20
type: spike
title: "A debug menu for tweaking values in the headset"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: medium
---

# A debug menu for tweaking values in the headset

## Description

Answers how the author can change tuning values in the headset with a controller, seeing the effect at once, in place of editing a file on the PC, pushing it and relaunching. It settles how the menu is drawn and driven, which settings can change live, how a good set of values gets back to the PC, and whether players ever see it.

## Acceptance Criteria

Verify: a recorded answer on each of those points, with a throwaway trial in the headset of moving one setting with a controller; the author has decided whether to build it, and any story that follows is on the backlog.

## References

- parent — none
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Tuning (says the file is never changed in the headset, and is read once at launch)
- code — game/Logic/Tuning.cs, game/Main.Tuning.cs

## Notes

- Source: the author, on setting the seat for the ridden dragon, 2026-10-30.
- Unknown: whether the game has any controller input yet, and whether the settings read only at launch can change live.
