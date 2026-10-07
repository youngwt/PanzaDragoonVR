# Milestones

Build order. Each ends in a fifteen-second clip. No dates are set.

| # | Milestone | Track | Capabilities | Needs from the disc | Clip |
|---|-----------|-------|--------------|---------------------|------|
| 0 | Engine spike | Flight | CAP-1, CAP-2 | Nothing | Looking around the test scene in the headset |
| 1 | Visualise the original assets | Pipeline | CAP-3 | Episode 3 textures | Scrolling through a folder of extracted textures |
| 2 | Ground and canopy in VR | Both | CAP-4 | Floor and canopy planes | Flying through the forest with nothing else in it |
| 3 | The dragon in VR | Both | CAP-5 | Dragon model and textures | Looking down and around at the dragon |
| 4 | The reticle | Flight | CAP-6 | Nothing | Sweeping the reticle across the scene |
| 5 | Hand-tracked gun | Flight | CAP-7 | Nothing | Close-up of the gun in hand |
| 6 | Shooting down flying cubes | Flight | CAP-8 | Nothing | A full lock-on volley destroying four cubes |
| 7 | Real models in the game | Both | CAP-9 | Enemy models and textures | The same volley against real Zwei enemies |

Milestones 4, 5 and 6 need nothing from the disc and can be built while extraction is in progress.

## Status at 2026-10-07

- Milestone 0: not started.
- Milestone 1: Episode 3 textures decode with `tools/cgb_textures.py`; 16-colour textures are in colour, 256-colour ones are grey (palette not applied).
- Milestone 2: floor and canopy located in video memory from a save state; their source on the disc is unknown.
- Milestones 3 and 7: blocked on `.MDB` geometry.

## Risks

| Risk | Settled by |
|------|------------|
| C# on Android is labelled experimental in Godot 4.7 | Milestone 0 |
| Loading loose files from outside the APK on Quest | Milestone 0, using the app's own files folder |
| Plane data only reachable through a save state | Finding its source on the disc |
| `.MDB` geometry undecoded | Reverse engineering, before milestone 3 |
| Enemy flight paths: recorded data or algorithm | Ghidra work on the Episode 3 code |
| Checking by eye does not cover behaviour or timing | Choosing a behaviour reference before milestone 7 |
| SideQuest's stance on bring-your-own-ROM | Checking its rules before the first release |
