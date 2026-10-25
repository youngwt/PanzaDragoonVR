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

The player flies through the Episode 3 forest between the floor and canopy taken from the disc. The decoding library gains plane extraction; the game gains a looping floor and a canopy backdrop.

## Outcome

The first moment that looks like Zwei in the headset, shown by the forest scrolling past with nothing else in it.

## Done when

1. Extraction writes the floor and canopy images from the disc image.
2. In the headset the floor loops endlessly beneath the player with no visible seam.
3. The canopy sits overhead with no parallax.
4. Floor depth, canopy height and scroll speed can be tuned without changing the decoders.

## Boundaries

The two planes and their motion. For CAP-3 this epic delivers plane extraction only. No dragon, no enemies, no other scenery.

## Notes

- Settled by spike 2 (2026-10-25): the cells, maps and colours are in `EPISODE3.PRG`, the cells and maps packed with an LZ77 variant; offsets, the packing and the PAL differences are in zwei-disc-formats.md, section Floor and canopy.
- Waits on Engine spike because: it uses the VR scene and the asset loader.
- Waits on In-game extraction because: plane extraction is added to its decoding library and manifest.
- Source conflict: spec Constraints and the asset contract's "Not in the contract" — tuning values "set in the headset" vs no controller input until Aim and shoot (epic 5).
- Decision: the plane data's location is settled by an early hitl spike; the plane extraction story waits on its answer, and if it finds nothing the author decides then (user, 2026-10-22).
- Decision: tuning is a small file in the asset folder, edited on the PC and pushed with adb, read by the game; this settles the source conflict above for this epic, and the spec's Constraints line and the asset contract's "Not in the contract" tuning line are both reconciled to it (user, 2026-10-22).
- Decision: the canopy scrolls past at the floor's on-screen speed, as a flat backdrop, for now; how it moves is itself a tuning value, as it may need tweaking (user, 2026-10-22).

- Tracer bullet: story 1, the planes scrolling on placeholders; spike 2 runs alongside 1 and 3, and story 5 joins the tuning (3) and the extracted planes (4).
- Related: backlog spike 10 (repeating textures clamped on the Quest); stories 1 and 5 rely on the tiling shader that works around it today.
- Decision: backlog 15 (now 7, the Main.cs split) runs before story 1, and backlog 11 (now 8, the black colour-bank bug) before story 4, so neither collides with this epic's edits (user, 2026-10-22).
- Decision: each extracted plane image is the whole 512x512 page as VDP2 holds it, not its repeating unit; the tile size in the tuning file covers the page (user, 2026-10-25).
- Decision: story 4 fixes the VDP2 plane settings (16x16 cells, 256 colours, the pattern-name bits) as constants in the decoding library, documented as read from the save state; the code that sets the registers is not traced first (user, 2026-10-25).

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, section Floor and canopy
