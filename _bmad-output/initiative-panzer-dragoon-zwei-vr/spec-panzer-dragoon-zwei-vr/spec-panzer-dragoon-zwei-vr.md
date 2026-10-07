---
id: SPEC-panzer-dragoon-zwei-vr
companions:
  - milestones.md
  - stack.md
  - zwei-disc-formats.md
  - later-ideas.md
sources:
  - ../../brainstorm-panzer-dragoon-zwei-vr/milestone-roadmap.md
  - ../../brainstorm-panzer-dragoon-zwei-vr/research-notes.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Panzer Dragoon Zwei VR

## Why

A vision to realize: riding the dragon through Panzer Dragoon Zwei's forest level (Episode 3) in VR, with a full 360-degree view. It is a solo passion project and the author's first VR project. The target is a playable demo on Quest 3 within a year of 2026-10-06, shared on SideQuest on a bring-your-own-ROM basis, with progress worth a short clip at every step.

The work runs on two tracks. The **pipeline** track is a PC tool that turns the player's own disc image into ordinary files. The **flight** track is the game on the headset. They meet only at an external asset folder and its manifest.

## Capabilities

- **CAP-1: VR scene on Quest 3**
  - **intent:** The player can look all around a 3D scene in the headset.
  - **success:** A C# Godot APK runs on a Quest 3 at a steady 90 Hz, showing a ground plane, a sky and a grey cube flying around the player.
- **CAP-2: External assets**
  - **intent:** The game ships with no assets, loads everything at runtime from a known folder on the headset, and checks that folder against a manifest at launch.
  - **success:** The ground and sky of the CAP-1 scene are textured from the user's own example images copied into the folder. A file the manifest lists but the folder lacks is reported at launch.
- **CAP-3: Extraction tool**
  - **intent:** A user runs a PC tool on their own Zwei disc image and gets the demo's assets as ordinary files, plus the manifest.
  - **success:** One run writes the Episode 3 images, and they are recognisable by eye in a file explorer.
- **CAP-4: Forest floor and canopy**
  - **intent:** The player flies through the Episode 3 forest between the extracted floor and canopy.
  - **success:** In the headset the floor loops endlessly beneath the player and the canopy sits overhead with no parallax.
- **CAP-5: Riding the dragon**
  - **intent:** The player sits mid-way along the extracted dragon's back.
  - **success:** In the headset the wings are on either side, the head in front and the tail behind.
- **CAP-6: Reticle**
  - **intent:** The player aims a reticle by pointing the aiming-hand controller.
  - **success:** The reticle follows the hand at a fixed distance around the player and changes colour over a target.
- **CAP-7: Gun in hand**
  - **intent:** The player sees a gun where the aiming hand is.
  - **success:** A gun model tracks the controller. The Quest controller model satisfies this; a bone-like Panzer Dragoon gun is the later form.
- **CAP-8: Shooting**
  - **intent:** The player destroys targets with rapid shots or lock-on lasers.
  - **success:** Grey cubes corkscrew around the player. The top trigger fires rapid shots. Holding a face button locks up to four targets, each marked by a cloned reticle; releasing it fires homing lasers that destroy them.
- **CAP-9: Real enemies**
  - **intent:** The targets are the Episode 3 flying enemies extracted from the disc.
  - **success:** The CAP-8 lock-on volley destroys four extracted Zwei enemies in the headset.

Build order and track for each capability are in `milestones.md`.

## Constraints

- Nothing copyrighted goes into git or the APK: the disc image, extracted or decoded assets, BIOS files, the soundtrack.
- All Saturn-format knowledge lives in the PC extraction tool. The game reads only standard formats (PNG and similar).
- The two tracks share only the external asset folder and manifest. Flight work runs on own example images and grey cubes, so it never waits on extraction.
- Extraction runs on the PC, once. Nothing is extracted on the headset.
- Platform is standalone Quest 3, Godot 4.7, with C# from the first line (`stack.md`).
- The known folder is the app's own `Android/data/<package>/files`. Quest's scoped storage rules out arbitrary `/sdcard` paths.
- The game stays on rails as in the original: the dragon always moves forward.
- No full-body IK. Hands are floating controllers or guns.
- Information lives in the world, not on a HUD. The radar is the one permitted HUD element.
- Original assets are used as extracted, however rough they look in VR. No upscaled or redrawn art.
- Floor depth and canopy height are tuning values set in the headset, not fixed up front.
- Extracted images are checked by eye; there is no formal reference comparison.
- Rez Infinite is the reference for reticle and aiming feel.
- Every milestone ends in something that can be recorded in fifteen seconds.

## Non-goals

- Any episode other than Episode 3.
- Everything in `later-ideas.md`: health, berserk, steering, view turns, building the radar, sound, real enemy flight paths, centipedes, the boss, a bundled release.
- A custom asset viewer. The file explorer is the viewer.
- Dates for individual milestones.

## Success signal

- Within a year of 2026-10-06, a player who has run the extractor on their own disc rides the dragon through the Episode 3 forest on a Quest 3 and destroys four real Zwei enemies with one lock-on volley. The repository and the APK contain no copyrighted content.

## Assumptions

- The scripts in `tools/` are the start of the shipping extraction tool, not throwaway prototypes.
- Only the Japanese disc (GS-9049, V1.001) is supported.
- The aiming hand defaults to left; the left/right option is post-demo.
- The no-HUD rule was the brainstorm coach's synthesis of the author's ideas and is treated as a constraint.
- The demo ends at milestone 7.

## Open Questions

- Where on the disc are the floor and canopy tiles, maps and palette? Today they can only be taken from an emulator save state, which CAP-3 does not allow.
- Is the save state's colour RAM the right palette for the 256-colour textures, and where is that palette on the disc?
- `.MDB` geometry is undecoded, which blocks CAP-5 and CAP-9. Which standard model format does the extractor write?
- Which of the ten dragon forms does the demo use, and where does its wing animation come from?
- What do the `EPISODE3.DAT` motion matrices drive, and are enemy flight paths recorded data or an algorithm?
- What is the reference for behaviour and timing before CAP-9, given that checking by eye only works for images?
- What is the manifest format and folder layout? It is the contract between the tracks and is undefined.
- Does SideQuest accept a bring-your-own-ROM project?
