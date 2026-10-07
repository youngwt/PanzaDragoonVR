---
type: epic
title: "Forest floor and canopy"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-4, CAP-3]
after: []
assignee: ""
risk: medium
---

# Forest floor and canopy

## Description

The player flies through the Episode 3 forest between the floor and canopy taken from the disc. The tool gains plane extraction; the game gains a looping floor and a canopy backdrop.

## Outcome

The first moment that looks like Zwei in the headset, shown by the forest scrolling past with nothing else in it.

## Done when

1. The extraction command writes the floor and canopy images from the disc image.
2. In the headset the floor loops endlessly beneath the player with no visible seam.
3. The canopy sits overhead with no parallax.
4. Floor depth, canopy height and scroll speed can be tuned without rebuilding the tool.

## Boundaries

The two planes and their motion. For CAP-3 this epic delivers plane extraction only. No dragon, no enemies, no other scenery.

## Notes

- Unknown: which disc file holds the plane tiles, maps and palette. They are only reachable through a save state today; every story here waits on it.
- Waits on Engine spike because: it uses the VR scene and the asset loader.
- Waits on Extraction tool because: plane extraction is added to that command and its manifest.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, section Floor and canopy
