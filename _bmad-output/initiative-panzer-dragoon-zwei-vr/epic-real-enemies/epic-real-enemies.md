---
type: epic
title: "Real enemies"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-9, CAP-3]
after: []
assignee: ""
risk: medium
---

# Real enemies

## Description

The grey cubes become the Episode 3 flying enemies taken from the disc. They fly the same corkscrew paths.

## Outcome

The demo the spec describes, shown by a lock-on volley destroying four real Zwei enemies.

## Done when

1. The extraction command writes the Episode 3 flying enemies as textured models.
2. In the headset those enemies replace the cubes.
3. One lock-on volley destroys four of them.
4. The full scene, with forest, dragon and enemies, holds 90 Hz.

## Boundaries

Enemy models in place of cubes. For CAP-3 this epic delivers the enemy models only. Real flight paths, centipedes and the boss are out.

## Notes

- Decision: enemies fly the corkscrew paths; real flight paths are post-demo (user approved, 2026-10-07).
- Unknown: which `E03*` files hold the flying enemies.
- Waits on Riding the dragon because: it reuses model decoding and model loading.
- Waits on Aim and shoot because: it reuses targets, lock-on and lasers.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md
