---
type: epic
title: "Aim and shoot"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-6, CAP-7, CAP-8]
after: []
assignee: ""
risk: low
---

# Aim and shoot

## Description

The player aims a reticle with the aiming hand, sees a gun there, and destroys corkscrewing grey cubes with rapid shots or a four-target lock-on volley. Only the gun comes from the disc, with a grey placeholder in its place until it is extracted; the rest needs nothing from it.

## Outcome

The game is fun to play before any Zwei asset is in it, shown by a full lock-on volley destroying four cubes.

## Done when

1. The reticle, the game's own graphic, follows the aiming hand at a fixed distance around the player and changes colour over a target.
2. A gun model tracks the controller.
3. The top trigger fires rapid shots that destroy cubes.
4. Holding a face button locks up to four cubes, each marked by a cloned reticle; releasing it fires homing lasers that destroy them.
5. Each rapid shot sounds from the gun.
6. The scene still holds 90 Hz.

## Boundaries

Aiming, the gun, targets and both fire modes. Rez Infinite is the reference for feel. The aiming hand is the left. No hit reactions on the player, no berserk, no sound but the gunshot, no handedness option.

## Notes

- Waits on Engine spike because: it uses the VR scene and tracked controllers.
- Decision (author, 2026-11-11): the gun is built from the game's own assets, the gun the rider holds in `COMMON.MDB`, extracted from the player's disc under a new asset ID; a grey gun built in code stands in until then. This replaces "Nothing here needs the disc" for the gun only, and the spec's "the Quest controller model satisfies this".
- Decision (author, 2026-11-11): six cubes corkscrew around the player, and a destroyed cube returns to its path a few seconds later.
- Decision (author, 2026-11-11): rapid shots are fast, visible bolts, not instant hits.
- Waits on Riding the dragon because: the gun is decoded and written with that epic's model decoder and `.glb` writer (4.3, 4.5).
- Tracer bullet: entry 1, the left controller aims a reticle.
- Decision (author, 2026-11-14): the rapid shots sound from the gun, the game's own gunshot extracted from the disc where possible, else a royalty-free (CC0) one kept in the repo. This replaces "no sound" for the gunshot only; the lock-on lasers stay silent.
- Decision (author, 2026-11-14): lock-on is held on X of the left controller; the development tuning list keeps the right controller's A, B and stick.
- Decision (author, 2026-11-14): each milestone's clip is recorded by the story that finishes it: milestone 4 by entry 1, 5 by entry 3, 6 by entry 7.
- Decision (author, 2026-11-15): the reticle is the game's own graphic, a round white ring shaped like bone, extracted from the disc (spike 12, story 13); the ring the game draws stays as its placeholder. The tuning list hangs from the right controller (5.1's Plan Change Log).
- Decision (author, 2026-11-11): the feel values (the reticle's distance, the fire rate, the lock-on, the lasers' speed) are built-in constants, not tuning settings.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- later ideas — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/later-ideas.md, for what stays out
