---
type: epic
title: "In-game extraction"
parent: initiative-panzer-dragoon-zwei-vr
covers: [CAP-3]
after: []
assignee: ""
risk: medium
---

# In-game extraction

## Description

The Saturn decoders, ported from the Python prototypes in `tools/`, become one C# library. The game runs it at launch on the player's own disc image in the app's folder and writes the Episode 3 textures in colour, with the manifest, into that folder. A PC command runs the same library for reverse engineering and checking by eye.

## Outcome

A player who copies their own disc image to the headset gets the same textures the author has, with no PC step, shown by browsing the headset's asset folder.

## Done when

1. With the Japanese disc as bin/cue (one `.bin` for all tracks or one per track) or as a plain ISO in the app's folder, launching the game extracts the Episode 3 textures as PNG, with a manifest that passes `tools/write_manifest.py --check`.
2. The 256-colour textures come out in colour and look right by eye.
3. Development mode keeps the disc image and re-extracts on every launch; release mode deletes it after a successful extraction and does not extract again.
4. The PC command, given the same disc image, writes byte-for-byte the same files as the headset.
5. The headset shows progress while extracting; a failed extraction is reported and the game carries on with placeholders.
6. The decoders are unit-tested on the PC, and nothing extracted lands in git or the APK.

## Boundaries

The C# decoding library, the in-game extraction step and the PC command, for textures (CAP-3). Planes belong to Forest floor and canopy; models to Riding the dragon and Real enemies, each adding its own decoder to the library. The Python scripts stay as prototypes. Compressed disc images such as CHD are out.

## Notes

- Unknown: where the 256-colour palettes are on the disc. Today the likely source is a save state's colour RAM.
- Waits on Engine spike because: it writes the manifest in the format agreed there, and story 1.4 settles how the game reaches its asset folder.
- Unknown: how other discs than the Japanese GS-9049 V1.001 behave; they are attempted, not refused.
- Decision: re-scoped on 2026-10-14 from a PC extraction tool to in-game extraction with a shared C# decoding library, following the spec update; Done when 4 (identical output on PC and headset) added so checking by eye on the PC holds for the headset (user approved, 2026-10-14).

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md, section Extraction
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md
- manifest checker — tools/write_manifest.py (story 1.9)
