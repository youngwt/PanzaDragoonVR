---
type: epic
title: "Riding the dragon"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-5, CAP-3]
after: []
assignee: ""
risk: medium
---

# Riding the dragon

## Description

The player sits on the dragon taken from the disc, where the game places its rider. The decoding library gains model extraction; the game gains model loading.

## Outcome

The author is on the dragon, shown by looking down and around at it in the headset.

## Done when

1. Extraction writes the dragon as a textured model in the agreed standard format, and the PC command's copy opens in an ordinary viewer.
2. The author has viewed the ten dragon forms and chosen one for the demo.
3. In the headset the wings are on either side, the head in front and the tail behind.
4. The dragon stays in place under the player while the forest scrolls.

## Boundaries

The dragon model and the player's seat on it. For CAP-3 this epic delivers model decoding, used first on the dragon. No steering, no health or berserk effects.

## Notes

- Settled by spike 2 (2026-10-30): the `.MDB` geometry format, in `zwei-disc-formats.md`, Models.
- Open question: which of the ten dragon forms the demo uses; it now waits only on the author's choice, in story 7.
- Decision: each form's asset ID is `dragon.form01` to `dragon.form10`, numbered from `DRA01A` to `DRA10J` (user, 2026-10-30).
- Decision: constants the game needs from the disc are never committed. The extraction reads them from the player's disc and writes them to a config file in the asset folder, and the game reads that file at launch. Where to look on the disc stays in the code, because the extraction needs it to read the disc. Story 5 defines the file when something first needs it (user, 2026-10-30).
- Decision: the seat is the game's rider position for that form, on `DRA01A` astride the neck near the head, not mid-way along the back; story 5 reads it from the table in `1ST_READ` on the player's disc, as `zwei-disc-formats.md`, The rider, says, and the tuning settings of story 4 adjust from there (user, 2026-10-30).
- Open question: where the wing animation comes from (`.MTB` files are unexamined), and whether still wings are acceptable for the demo.
- Decision: story 6 confirmed the model rules with no change to the files: the divisor stays 4,000, hard-edged pixels stay, materials stay double-sided, `half` and `mesh` stand, used by no dragon, and per-corner shading is left out, as backlog 19 (user, 2026-10-30).
- Waits on Engine spike because: it adopts the standard model format and uses the asset loader.
- Waits on In-game extraction because: model extraction is added to its decoding library, and it needs coloured textures.
- Waits on Forest floor and canopy because: the dragon stays in place while its floor and canopy scroll (story 3.5).
- Decision: the `.MDB` geometry format is settled by an early hitl spike, as spike 3.2 settled the planes; the decoding stories wait on its answer (user, 2026-10-29).
- Decision: the dragon holds a still pose in this epic; the wing animation (`.MTB`) is deferred to a backlog spike (user, 2026-10-29).
- Decision: the player's seat on the dragon (its place along the back, its height, the model's scale) is a set of tuning-file settings, as the floor and canopy are; the author's chosen values become the built-ins (user, 2026-10-29).
- Decision: backlog 8, confirming the asset contract's provisional model rules, moves into this epic beside model decoding (user, 2026-10-29).
- Decision: the tracer bullet is story 1, a grey placeholder dragon under the player while the forest scrolls; the `.MDB` spike comes second, and the `.glb` writer is built on synthetic models beside it (user, 2026-10-29).
- Decision: the game asks for one ride ID, `dragon.ride`; each form gets its own ID under `dragon.`, and story 7 has the extraction also write the chosen form as `dragon.ride` (user, 2026-10-29).
- Decision: the extraction always writes all ten dragon forms, on the PC and the headset (user, 2026-10-29).
- Decision: story 6 (was backlog 8) goes to the build without `refine`; its criteria are written during the build (user, 2026-10-29).
- Decision: the wing animation spike is backlog 18 (user, 2026-10-29).

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, sections Textures, Models and The rider
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, sections Models and asset IDs
