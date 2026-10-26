---
id: 17
type: story
title: "Every allowed tuning value gives a sane picture"
parent: none
covers: [CAP-4]
after: []
assignee: ""
refined: false
hitl: true
risk: low
---

# Every allowed tuning value gives a sane picture

## Description

The tuning file's ranges (`game/Logic/Tuning.cs`, the asset contract's Tuning table) accept values that break the scene: a `canopy_height` of 2 m cuts through the flyer, whose path reaches about 2.65 m, and sits 0.4 m above the assumed eye; a `floor_tile` or `canopy_tile` of 0.1 on the 2000 m planes makes 20000 tiles, where the image jitters; a `flight_speed` of 100 moves the image about 11 tiles a frame at 90 Hz, so it reads as still or backwards; and a `floor_depth` of 1000 shows the planes' edge 45 degrees below the horizon. Narrow each range so every allowed value gives a sane picture, keeping the author's built-ins (6, 10, 6, 1, 24, 48) inside them.

## Acceptance Criteria

Verify: the contract's Tuning table and `Tuning.cs` give the new ranges and the author has agreed them; the range-end tests follow; in the headset, tuning files at each new range end show a sane scene (the canopy above the head and the flyer, no jitter, the scroll reading as forward motion, no plane edge in view).

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-forest-floor-and-canopy/epic-forest-floor-and-canopy-retrospective.md, findings R2 and R3, action A2
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Tuning

## Notes

- Source: the forest floor and canopy retrospective (author, 2026-10-26).
- Hitl: the contract's ranges need the author's yes; headset check at the range ends.
