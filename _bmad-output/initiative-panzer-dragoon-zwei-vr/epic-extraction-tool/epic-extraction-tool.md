---
type: epic
title: "Extraction tool"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-3]
after: []
assignee: ""
risk: medium
---

# Extraction tool

## Description

The scripts in `tools/` become one command a user runs on their own disc image. It writes the Episode 3 textures in colour, and the manifest the game checks.

## Outcome

Anyone with the disc gets the same files the author has, shown by browsing the output folder.

## Done when

1. One command, given the disc image, writes the Episode 3 textures as PNG with no manual steps.
2. The 256-colour textures come out in colour and look right by eye.
3. The command writes a manifest in the agreed format listing every file it wrote.
4. Nothing it writes lands inside git.

## Boundaries

The PC tool only. This epic delivers the tool, the textures and the manifest for CAP-3. Planes belong to Forest floor and canopy; models to Riding the dragon and Real enemies.

## Notes

- Unknown: where the 256-colour palettes are on the disc. Today the likely source is a save state's colour RAM.
- Waits on Engine spike because: it writes the manifest in the format agreed there.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md, section Extraction tool
