---
id: 19
type: story
title: "The dragon shows the disc's per-corner shading"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: medium
---

# The dragon shows the disc's per-corner shading

## Description

The extracted dragons carry no light: the game's per-corner shading tables, which vary by episode, are left out of the models. This writes them as `COLOR_0` and has the game apply them, if the author wants the dragon closer to the original.

## Acceptance Criteria

Verify: in the headset the dragon's back is bright and its belly dark as the disc's table says; the asset contract's rule on `COLOR_0` and its note that the game ignores it are updated; output stays byte-for-byte the same on the PC and the headset.

## References

- parent — none
- epic — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-riding-the-dragon/epic-riding-the-dragon.md
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, Shading
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Models, Geometry

## Notes

- Source: left out of the riding the dragon epic in its story 6; the author liked the unshaded dragons and chose to leave it (author, 2026-10-30).
- Unknown: which episode's table the demo would use, and whether a form's table covers it in all episodes the demo needs.
