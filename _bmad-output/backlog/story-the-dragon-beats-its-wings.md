---
id: 22
type: story
title: "The dragon beats its wings"
parent: none
covers: []
after: [18]
assignee: ""
refined: false
hitl: true
risk: medium
---

# The dragon beats its wings

## Description

The ridden dragon loops its wing beat in the headset. The extractor reads form 9's wing beat (motion slot 0) from the player's disc and writes it into `dragon_ride.glb` as a glTF animation, and the game plays it in a loop at a rate set in the tuning file.

## Acceptance Criteria

- Given the Japanese and the European disc, when `pdz-extract` runs, then `dragon_ride.glb` holds one looping animation of the body nodes, `tools/validate-gltf.sh` reports no errors, and the PC and headset copies are byte-for-byte the same (`tools/compare-headset.sh`).
- Given the headset, when the author rides the dragon, then its wings beat in a loop, raised and lowered as in the spike's drawings, while the dragon stays under the player.
- Given the tuning file, when the author changes the new wing-beat rate setting, then the beat speeds up or slows down; its built-in value, range and four copies follow `AGENTS.md`'s rules on tuning.
- Given the asset contract's Models section, then its "no animations" rule allows this animation, and says how it is keyed.

## References

- spike — _bmad-output/backlog/spike-where-the-dragon-s-wing-animation-comes-from-plan.md (the answers and the story's shape)
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, Motion: `.MTB`
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Models
- code — lib/PdzDecoding/Dragons.cs, GlbWriter.cs; game/RiddenDragon.cs; game/Logic/Tuning.cs

## Notes

- Source: the author, on the spike's drawings, 2026-11-09: "yes animate, leave the playback rate for now".
- Known: one key a frame, step interpolation; the frames are whole, with no blending (spike 18). Node rotations come from three angles in the order `Ry*Rx*Rz`, so they must become quaternions the same way on the PC and the headset.
- Known: the game's playback rate is not traced; at 30 frames a second a beat takes 1.8 s, at 60, 0.9 s. The tuning setting stands in until it is.
- Out of scope: the other motion slots (gliding, raising, lowering), the 30 swapped wing shapes, blending, and the other nine forms' animations unless the writer gives them for free.
- Unknown: whether the Godot loader plays a glTF animation on a model loaded at runtime from the asset folder without extra code.
