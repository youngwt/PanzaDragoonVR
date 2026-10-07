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

The player sits mid-way along the back of the dragon taken from the disc. The tool gains model extraction; the game gains model loading.

## Outcome

The author is on the dragon, shown by looking down and around at it in the headset.

## Done when

1. The extraction command writes the dragon as a textured model in the agreed standard format, and it opens in an ordinary viewer.
2. The author has viewed the ten dragon forms and chosen one for the demo.
3. In the headset the wings are on either side, the head in front and the tail behind.
4. The dragon stays in place under the player while the forest scrolls.

## Boundaries

The dragon model and the player's seat on it. For CAP-3 this epic delivers model decoding, used first on the dragon. No steering, no health or berserk effects.

## Notes

- Unknown: the `.MDB` geometry format. Every story here waits on it.
- Open question: which of the ten dragon forms the demo uses; decided once they can be viewed.
- Open question: where the wing animation comes from (`.MTB` files are unexamined), and whether still wings are acceptable for the demo.
- Waits on Engine spike because: it adopts the standard model format and uses the asset loader.
- Waits on Extraction tool because: model extraction is added to that command, and it needs coloured textures.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, section Textures
