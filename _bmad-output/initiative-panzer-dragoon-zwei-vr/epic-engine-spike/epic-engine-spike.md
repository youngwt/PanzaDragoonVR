---
type: epic
title: "Engine spike on Quest 3"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-1, CAP-2]
after: []
assignee: ""
risk: medium
---

# Engine spike on Quest 3

## Description

A C# Godot app runs on the Quest 3 and takes its images from a folder outside the APK. This is the platform baseline: toolchain, project scaffold and deployment to the headset. It also settles the two decisions later epics adopt.

## Outcome

The author knows that C# on Quest and loading loose files both work, shown by the test scene running in the headset.

## Requirements

- CAP-1: The player can look all around a 3D scene in the headset: ground plane, sky and a grey cube flying around them, at a steady 90 Hz.
- CAP-2: The game ships with no assets, loads everything at runtime from the app's own files folder, and reports at launch any file the manifest lists but the folder lacks.

## Done when

1. An APK built from C# runs on a Quest 3 and holds 90 Hz while the player looks around.
2. The ground and sky show the author's own images, copied into the app's files folder, not packed in the APK.
3. Removing a file the manifest lists produces a visible report at launch.
4. The manifest format, folder layout and standard model format are written down and agreed.

## Boundaries

The Godot project and its build and deploy path. No Zwei content, no controllers beyond what the platform gives for free, no extraction tool work.

## Notes

- Unknown: whether C# on Android holds up on Quest; the first story answers it.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md, section Game
