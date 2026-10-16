---
type: epic
title: "Engine spike on Quest 3"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-1, CAP-2]
after: []
assignee: ""
risk: medium
status: done
---

# Engine spike on Quest 3

## Description

A C# Godot app runs on the Quest 3 and takes its images from a folder outside the APK. This is the platform baseline: toolchain, project scaffold and deployment to the headset. It also settles the two decisions later epics adopt.

## Outcome

The author knows that C# on Quest and loading loose files both work, shown by the test scene running in the headset.

## Requirements

- CAP-1: The player can look all around a 3D scene in the headset: ground plane, sky and a grey cube flying around them, at a steady 90 Hz.
- CAP-2: The game ships with only the author's own placeholder assets and nothing copyrighted. At runtime it replaces them, ID by ID, with assets from the app's own files folder, and reports at launch any file the manifest lists but the folder lacks.

## Done when

1. An APK built from C# runs on a Quest 3 and holds 90 Hz while the player looks around.
2. The ground and sky show the author's own images, copied into the app's files folder and not packed in the APK, in place of the built-in placeholders.
3. Removing a file the manifest lists produces a visible report at launch, and its placeholder is shown.
4. With an empty files folder the scene still runs, on placeholders.
5. The manifest format, folder layout and standard model format are written down and agreed.

## Boundaries

The Godot project and its build and deploy path. No Zwei content, no controllers beyond what the platform gives for free, no extraction tool work.

## Notes

- Settled: C# on Android holds up on Quest; story 1 showed it in the headset (2026-10-08).
- Decision: eight stories. Story 1 is the tracer bullet: toolchain, scaffold, build-and-deploy command and a C# scene in the headset, settling C# on Quest first (user approved, 2026-10-07).
- Decision: the Steam Deck is the build machine (user, 2026-10-07).
- Decision: story 7, loading the grey cube from a model file, is in this epic, so the dragon epic inherits a model loader proven on the headset (user approved, 2026-10-07).
- Decision: sequencing. Stories 2 and 6 are decisions on paper and can run beside story 1. Stories 3, 4, 5 and 7 are one lane in the same scene and loader code. Story 6 waits on story 2 because both write the one asset contract document.
- Assumption: the home partition had 18 GB free on 2026-10-07; the toolchain is expected to fit.
- The draft check was run in the planning conversation, not by an independent agent.
- Decision: re-sliced on 2026-10-13 after the asset contract. The game ships built-in placeholders that folder assets replace ID by ID, so stories 4, 5 and 7 were reworded, story 6 also names the cube's asset ID, and story 9 (a manifest writer for the author's own files) was added as a prerequisite of story 4 (user approved, 2026-10-13).
- Decision: story 10 added to find why the display stays at 72 Hz, so Done when 1 keeps the spec's 90 Hz. It does not block stories 4 to 7 (user, 2026-10-13).
- Decision: stories 1, 2 and 3 marked done at the user's word (2026-10-13). Story 3 was closed at 72 Hz with its last readout changes not yet seen on the headset; both are recorded in deferred-work.md and story 10 carries the 90 Hz question.
- The re-slice check was run in the planning conversation, not by an independent agent.
- The epic ended with ten stories: 9 and 10 were added at the re-slice of 2026-10-13, and story 8 was the refactor sweep.
- Decision: epic closed (author, 2026-10-16). The closure check found CAP-1, CAP-2 and Done when 1 to 5 met. The retrospective verdict is accepted-with-open-items; the open items are tickets 1 to 10 in `_bmad-output/backlog/`, kept there for a possible backlog epic. The retrospective's action A2 was fixed in commit a414cc0.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md, section Game
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md
