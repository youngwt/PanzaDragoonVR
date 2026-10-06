# Panzer Dragoon Zwei VR: milestone roadmap

Built from the brainstorming session of 2026-10-06. Target: a playable demo of the forest level (Episode 3) on Quest 3 within a year, shared on SideQuest on a bring-your-own-ROM basis.

No dates are set. Each milestone ends in something you can record in fifteen seconds and post.

## Two tracks, one meeting point

- **Pipeline track:** a PC tool that reads your own disc image and writes ordinary files (PNG and similar). All Saturn knowledge lives here. Extraction is one-and-done.
- **Flight track:** the Godot game on the headset. It ships with no assets and reads everything from a known external folder, checked against a manifest.

The two tracks meet only at that folder. Until real assets exist, the flight track runs on your own example images and grey cubes, so neither track blocks the other.

**Standing rule:** nothing copyrighted is ever committed to git. That covers extracted assets, the disc image and the soundtrack MP3.

## Milestones

| # | Milestone | Track | Done when | Clip to post |
|---|-----------|-------|-----------|--------------|
| 0 | Engine spike | Flight | A C# Godot APK runs on the Quest 3 at a steady frame rate, showing a ground plane and sky textured from your own images loaded out of the external folder, with a grey cube flying around you | Looking around the test scene in the headset |
| 1 | Visualise the original assets | Pipeline | The extractor writes Episode 3 images from the disc and you can browse them in the file explorer | Scrolling through a folder of extracted textures |
| 2 | Ground and canopy in VR | Both | The extracted forest floor loops beneath you by scrolling UVs and the canopy sits overhead at effective infinity | Flying through the forest with nothing else in it |
| 3 | The dragon in VR | Both | You sit mid-way along the dragon's back with wings either side, head in front and tail behind | Looking down and around at the dragon |
| 4 | The reticle | Flight | A sprite reticle on an imaginary sphere follows your aiming hand and changes colour over a target | Sweeping the reticle across the scene |
| 5 | Hand-tracked gun | Flight | A gun model follows the controller (Quest controller model first, bone-like Panzer Dragoon gun later) | Close-up of the gun in hand |
| 6 | Shooting down flying cubes | Flight | Cubes corkscrew around you; the top trigger fires rapid shots, a held face button locks up to four targets with cloned reticles, and release fires lasers | A full lock-on volley destroying four cubes |
| 7 | Real models in the game | Both | Extracted flying enemies replace the cubes | The same volley against real Zwei enemies |

Milestone 0 is the engine spike from the session; milestones 1 to 7 are the seven showable moments in the order you gave them. Milestones 4, 5 and 6 need nothing from the disc, so they can be built while the extractor is still in progress.

## After milestone 7

Ideas from the session with no milestone yet, roughly in the order they came up:

- Health shown as the dragon's colour or aura, and a controller vibration on each hit.
- Berserk as energy fizzing around the controllers, released by a button press.
- Right thumbstick guiding the dragon on its rail; left stick for smooth quarter turns; aiming hand selectable.
- Radar (kept, but the one HUD element left; revisit in the headset).
- Sound: soundtrack MP3 supplied by the player, effects extracted from the disc if possible, each sound placed at its source.
- Real enemy flight paths in place of corkscrews.
- Giant centipedes, then the final boss.
- A release that bundles the APK with the extraction tool.

## Open risks

| Risk | Why it matters | Settled by |
|------|----------------|------------|
| Godot C# on Quest | Godot 4.7's docs still label C# on Android "experimental"; Quest export itself is well supported | Milestone 0, built in C# from the first line |
| Loading loose files from outside the APK | Godot can load images at runtime, but Quest's Android 12+ scoped storage blocks arbitrary `/sdcard` folders; the app's own `Android/data/<package>/files` folder is the reliable location | Milestone 0, using that folder as the "known folder" |
| How Zwei stores textures, planes and models on the disc | First-pass research found no documented formats or extraction tools for Zwei; expect original reverse-engineering work. Blocks milestone 1 | Deeper research (SegaXtreme, Panzer Dragoon Legacy forums, yaz0r's open-source Saga project), then Ghidra |
| Enemy flight paths: recorded data or an algorithm | Decides how the real level choreography is recovered | Ghidra (SH-2 support is built in; a community Saturn loader reads ISOs and emulator save states), with an agent analysing the output |
| Checking by eye only | Works for images, not for behaviour or timing | Decide a reference for behaviour before milestone 7 |
| Plane image resolution (512 or 1024) | Affects how the planes look at life size | Milestone 1 |
| SideQuest acceptance of a bring-your-own-ROM project | Decides how the demo is shared | Check SideQuest's rules before the first release |
