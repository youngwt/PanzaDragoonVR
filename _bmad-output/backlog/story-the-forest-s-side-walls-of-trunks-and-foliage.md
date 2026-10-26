---
id: 16
type: story
title: "The forest's side walls of trunks and foliage"
parent: none
covers: [CAP-4]
after: []
assignee: ""
refined: false
hitl: true
risk: high
---

# The forest's side walls of trunks and foliage

## Description

In Episode 3 the forest closes in at the sides: walls of tree trunks with vines, and mossy mounds along the path, seen in Mednafen's Episode 3 save state while the floor and canopy were tuned (2026-10-26). They are solid, lit shapes at different depths, so most likely VDP1 models textured with `E03CMN`'s trunk, vine and foliage textures, not VDP2 planes; the state also has VDP2's NBG0 and NBG3 on, which may give the dark green distance and are not yet examined. The scene has the floor and canopy only, so it lacks this closed-in feel.

## Acceptance Criteria

Verify: in the headset the forest's sides close in with trunks and foliage taken from the disc, as in Mednafen's Episode 3, and NBG0 and NBG3's part in it is known.

## References

- parent — none
- epic — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-forest-floor-and-canopy/epic-forest-floor-and-canopy.md, Notes (closure)
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md

## Notes

- Source: the forest floor and canopy epic's closure (author, 2026-10-26); it needs `.MDB` geometry decoded, which the spec lists as open, so it belongs to a later epic on the level's scenery.
