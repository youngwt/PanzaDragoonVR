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

- Settled by spike 3 (2026-10-17): the palettes for mode 4 and mode 0 textures come from VDP2 colour RAM, which Episode 3 fills from colour upload tables in `EPISODE3.PRG` (at 0x288 and 0xE23C); see zwei-disc-formats.md, "Palettes for colour banks".
- Waits on Engine spike because: it writes the manifest in the format agreed there, and story 1.4 settles how the game reaches its asset folder.
- Unknown: how other discs than the Japanese GS-9049 V1.001 behave; they are attempted, not refused.
- Decision: re-scoped on 2026-10-14 from a PC extraction tool to in-game extraction with a shared C# decoding library, following the spec update; Done when 4 (identical output on PC and headset) added so checking by eye on the PC holds for the headset (user approved, 2026-10-14).
- Source conflict: Done when 1 — "textures ... with a manifest" vs the asset contract, which defines no per-texture IDs and says loose PNGs need no manifest entry; only `e03.floor`, `e03.canopy` and `e03.flyer` exist.
- Source conflict: spec CAP-3 and Constraints — disc image location (assumed `disc/`), development and release modes, the "do not extract again" marker, and progress and failure reporting vs the asset contract, which defines none of them.
- Decision: the 256-colour palette location is settled by an early hitl spike, right after the tracer bullet; the colour story waits on its answer (user, 2026-10-16).
- Decision: extracted textures get per-texture IDs and are all listed in the manifest; a contract decision story defines the ID scheme (user, 2026-10-16).
- Decision: the same contract story also fixes the disc image location, development and release modes, the extraction marker, and progress and failure reporting (user, 2026-10-16).
- Decision: backlog tickets 1 (an empty asset list is an error) and 6 (the Android folder lookup returns nothing on the Quest 3) fold into this epic (user, 2026-10-16).
- Decision: story 2 is the tracer bullet: the C# library reading `E03B0N0`'s mode 1 textures from a per-track bin/cue, run by the game at launch on the headset and by the PC command. The contract story (1) comes before it because the tracer needs the disc image location it fixes; the palette spike (3) can run beside both (proposed at inception, 2026-10-16).
- Decision: breakdown of 12 entries approved (user, 2026-10-16).
- Decision: if spike 3 finds no palette on the disc, whether Done when 2 moves to a later epic is decided then, on the spike's evidence; story 6 waits on the answer (user, 2026-10-16).
- Decision: spike 3 also looks for the palette of the colour-bank (mode 0) textures (user, 2026-10-16).
- Note: backlog ticket 7 (stack notes and asset contract) must not run alongside story 1; both edit the asset contract.
- Decision: re-sliced after spike 3. Story 6 colours mode 4 and mode 0 from the 0xE23C table, found by its pattern, leaving them out with a notice on a disc with no table; mode 0 moves from story 5 (greyscale) to story 6; the 0x288 table stays a lead; story 7's manifest lists whatever the extraction writes. The 2026-10-16 decision on what to do if the spike found no palette no longer applies (user approved, 2026-10-17).
- Decision: story 13 added (board placed in front of the head's starting direction), from the deferred-work item of 2026-10-18; it waits on story 10 to avoid concurrent edits to the game's launch code, and the refactor sweep waits on it (user, 2026-10-18).

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/initiative-panzer-dragoon-zwei-vr.md
- spec — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md, section Capabilities
- constraint — the same spec, section Constraints
- formats — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md, section Extraction
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md
- manifest checker — tools/write_manifest.py (story 1.9)
