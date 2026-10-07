---
type: initiative
title: "Panzer Dragoon Zwei VR: a playable Episode 3 demo on Quest 3"
parent: none
covers: [CAP-1, CAP-2, CAP-3, CAP-4, CAP-5, CAP-6, CAP-7, CAP-8, CAP-9]
after: []
assignee: ""
risk: medium
---

# Panzer Dragoon Zwei VR: a playable Episode 3 demo on Quest 3

## Description

A player who owns the disc rides the dragon through Zwei's forest level in VR and shoots down its flying enemies. The spec owns the capabilities, constraints and non-goals; this initiative delivers them as six epics.

## Outcome

For the author and anyone with their own disc image: the spec's success signal, a lock-on volley destroying four real Zwei enemies from the dragon's back on a Quest 3, within a year of 2026-10-06.

## Done when

1. On a Quest 3, the player rides the extracted dragon between the extracted forest floor and canopy at a steady 90 Hz.
2. One lock-on volley destroys four extracted Episode 3 enemies.
3. Every asset on screen came from one run of the extraction tool on the player's own disc image.
4. The repository and the APK contain no copyrighted content.

## Boundaries

Two units: the PC extraction tool (`tools/`) and the Godot game. They meet only at the external asset folder and its manifest. Epics follow the spec's milestones, one demoable result each. Not in scope: the spec's non-goals and everything in `later-ideas.md`. Tracer path: epic 1 alone proves engine, headset, external folder and manifest connect; each later epic replaces placeholders with disc data.

- Touch point: Ghidra project and Mednafen save states — used to find formats, never shipped; owner: whichever epic needs the answer
- Touch point: SideQuest — copies the APK and assets to the headset; owner: epic-engine-spike

## References

- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md
- constraint — the same spec, section Constraints: no copyrighted content, Saturn knowledge stays in the tool, platform
- milestones — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/milestones.md
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md

## Notes

- Decision: six epics along the roadmap's milestones, with reticle, gun and shooting merged into one (user approved, 2026-10-07).
- Decision: build order follows the roadmap; Aim and shoot needs only epic 1 and is the epic to switch to when reverse engineering stalls (user approved, 2026-10-07).
- Decision: the manifest format and the standard model format are settled as two decision stories in epic 1, not with bmad-architecture; glTF is the proposal for models (user approved, 2026-10-07).
- Decision: Real enemies fly the corkscrew paths from Aim and shoot. The spec's open question on a behaviour reference is parked until after the demo (user approved, 2026-10-07).
- Decision: no CI or separate environments; the baseline in epic 1 is the toolchain, the project scaffold and deployment to the headset. Solo hobby project.
- Unknown: where the floor and canopy data is on the disc; epic Forest floor and canopy owns the answer.
- Unknown: the `.MDB` geometry format; epic Riding the dragon owns the answer.
- Assumption: epics 3, 4 and 5 all add to the same player scene, so they are built one at a time. Epic 2 can run beside any of them.
