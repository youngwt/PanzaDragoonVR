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

The player aims a reticle with the aiming hand, sees a gun there, and destroys corkscrewing grey cubes with rapid shots or a four-target lock-on volley. Nothing here needs the disc.

## Outcome

The game is fun to play before any Zwei asset is in it, shown by a full lock-on volley destroying four cubes.

## Done when

1. The reticle follows the aiming hand at a fixed distance around the player and changes colour over a target.
2. A gun model tracks the controller.
3. The top trigger fires rapid shots that destroy cubes.
4. Holding a face button locks up to four cubes, each marked by a cloned reticle; releasing it fires homing lasers that destroy them.
5. The scene still holds 90 Hz.

## Boundaries

Aiming, the gun, targets and both fire modes. Rez Infinite is the reference for feel. The aiming hand is the left. No hit reactions on the player, no berserk, no sound, no handedness option.

## Notes

- Waits on Engine spike because: it uses the VR scene and tracked controllers.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- later ideas — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/later-ideas.md, for what stays out
