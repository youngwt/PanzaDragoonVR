---
id: 22
type: story
title: "The dragon beats its wings"
parent: none
covers: []
after: [18]
assignee: ""
refined: true
hitl: true
risk: medium
---

# The dragon beats its wings

## Description

The ridden dragon loops its wing beat in the headset instead of holding a still pose. The extractor reads each form's wing beat from the player's disc and writes it into the form's model as an animation. The game plays the ridden dragon's in a loop at 30 frames a second. The author checks the beat in the headset.

## Acceptance Criteria

1. **The models carry the wing beat**
   **Given** the Japanese cue or the European ISO
   **When** `pdz-extract` runs
   **Then** `dragon_ride.glb` and each `dragon_formNN.glb` hold one animation, the form's wing beat, with one key a frame for every part (`nodeNN`) the beat moves, stepped, not blended
   **And** `tools/validate-gltf.sh` reports no errors on any of them
2. **The beat matches the spike's reading**
   **Given** the extracted `dragon_ride.glb`
   **When** the author poses it in a glTF viewer at frames 9 and 26 of the wing beat
   **Then** the wings are raised at frame 9 and lowered at frame 26, as in the spike's drawings of form 9 under `extracted/spike-mtb/out/`
3. **The same bytes everywhere**
   **Given** an extraction on the PC and one on the headset
   **When** `tools/compare-headset.sh` runs
   **Then** it reports identical, and a second PC run gives the same bytes
4. **A disc without the motion still gives dragons**
   **Given** a disc whose motion table or `.MTB` cannot be read
   **When** extraction runs
   **Then** extraction succeeds and the dragons are written still, as before this story
   **And** `pdz-extract`'s dragons line on the PC and the `PDZ-ASSETS` extraction line on the headset each say why they have no wing beat
5. **The dragon flaps in the headset**
   **Given** the extracted folder in the headset
   **When** the author rides the dragon
   **Then** its wings beat in a loop at 30 frames a second, a beat of 1.8 s on form 9, while it stays under the player, facing the way of flight, and the frame rate holds 90 Hz
6. **A still model stays still**
   **Given** a `dragon.ride` with no animation, such as the placeholder, a hand-made model or an older extraction
   **When** the game shows it
   **Then** it is shown still, with no problem reported
7. **The contract and the version follow**
   **Given** the finished change
   **When** the asset contract's Models section and the extractor's version are read
   **Then** the "no animations" rule allows the wing beat and says how it is keyed, and the version has risen in every place `AGENTS.md` lists

## Boundaries

- Must not change: the still pose's shape and seat (the beat moves the parts about the same origin, and the root does not move), texture numbering, the board, the scroll, and what the game does with every other asset.
- Must not change: the contract's `format`, which stays 1; a game built for format 1 ignores animations. The tuning file gains no setting.

## References

- spike — _bmad-output/backlog/spike-where-the-dragon-s-wing-animation-comes-from-plan.md, Implementation Notes
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md, Motion: `.MTB`
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Models

## Notes

- Source: the author, on the spike's drawings, 2026-11-09: "yes animate, leave the playback rate for now".
- Known: one key a frame, stepped; the motion is three angles a node in the order `Ry*Rx*Rz` plus a move, so the turn to glTF's rotations must give the same bytes on the PC and the headset. Its moves take the model's own conversion: times 16, then the model's divisor to metres, axes `(x, y, z)` to `(-x, y, -z)`, less the seat.
- Risk: medium, because a wrong conversion shows a broken dragon in the headset, and the output must stay byte-identical on the PC and the headset.
- Known: Godot's runtime glTF import builds an animation player for a model that carries animations; the game's model check (`GlbCheck`) does not look at animations.
- Decision: every form's model carries its own wing beat, not only `dragon.ride` (author, 2026-11-10).
- Decision: the beat plays at 30 frames a second, the game's rate as the author gives it, so there is no tuning setting for it (author, 2026-11-10).
- Decision: the game always plays the beat; nothing turns it off (author, 2026-11-10).
- Decision: the models keep their structure, each part side by side under the root; each part's keys combine its whole chain of turns from the rest pose, and the contract's structure rule stays (author, 2026-11-10).
- Decision: the root's own motion (node 0's bob and pitch) is left out, so the dragon as a whole stays at the seat and only the parts move (author, 2026-11-10).
- Out of scope: the other motions (gliding, raising, lowering), the 30 swapped wing shapes, blending.
