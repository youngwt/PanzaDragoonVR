# Research notes: first pass (2026-10-06)

A quick pass of a handful of web searches made at the end of the brainstorming session. Not a deep study. Findings that affect the plan are also summarised in the risk table of `milestone-roadmap.md`.

## Findings

### Godot on Quest 3

- Godot 4.7 is the current stable release and has dedicated export support for Quest 3 on Horizon OS. Meta has funded Godot's OpenXR and Quest work since 2024.
- Godot 4.5 added a universal OpenXR APK and Application SpaceWarp on Quest.
- The 4.7 documentation still labels C# on Android as **experimental** (supported since 4.2). This is the main engine risk; milestone 0 should be written in C# from the start to test it.

### Loading assets from outside the APK

- Godot loads images at runtime with `Image.load_from_file`, then `ImageTexture`. Call `generate_mipmaps()` for textures shown on 3D surfaces.
- Quest moved to Android 12 scoped storage around OS v51. Apps can no longer freely read arbitrary folders under `/sdcard`.
- The reliable location is the app's own folder, `/sdcard/Android/data/<package>/files`, which SideQuest can copy into. Use this as the "known folder".
- `MANAGE_EXTERNAL_STORAGE` is permitted on Quest only for apps with file-management features, so it is not a route for this project.

### Ghidra and the Saturn

- SuperH SH-1/SH-2 processor support has been built into Ghidra since 9.1.
- A community Sega Saturn loader for Ghidra opens ISO images plus Mednafen and Yabause save states. It describes itself as a work in progress.

### Zwei's data formats

- No documented formats or extraction tools for Zwei's models or textures were found. Expect original reverse-engineering work.
- A fan wiki describes the series' Saturn model and texture formats as "very strange".
- The disc's intro videos are `.cpk` files (OPENINGA/B/C.cpk).
- Someone on the Panzer Dragoon Legacy forum has extracted MIDI files from Zwei and Saga; worth reading for the audio side.
- One researcher on a related title extracted greyscale images with a small program and used the Yabause emulator to inject images and palettes into memory, which suggests palettes are stored separately from image data.

### Prior Panzer Dragoon reverse engineering

- yaz0r's **Azel** is an open-source reverse-engineered PC port of Panzer Dragoon Saga, started around 2015 and open-sourced on GitHub. It runs up to the Arachnoth boss. Same studio, later engine; it may share formats with Zwei. **Not confirmed.**

## To verify

- A forum thread says the ground plane was drawn by VDP1 and objects by VDP2. From general knowledge of Saturn hardware it is the other way round: VDP2 draws the rotating scroll planes (floor and canopy), VDP1 draws quads and sprites (models). Confirm early, because it decides where to look for each kind of data.
- The plane image resolution (512 or 1024).

## Not yet researched

- The disc's own file layout and file list.
- The Azel source code, for format clues.
- The SegaXtreme forums, where Saturn format knowledge tends to live.
- SideQuest's rules on bring-your-own-ROM projects.
- Whether enemy flight paths are recorded data or algorithmic (needs Ghidra).

## Sources

- [Godot C# documentation (4.7)](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
- [Godot 4.7 release](https://godotengine.org/download/archive/4.7-stable)
- [UploadVR: Godot XR features and universal OpenXR APK](https://uploadvr.com/godot-now-supports-more-xr-features-builds-a-universal-openxr-apk)
- [Godot: runtime file loading and saving](https://docs.godotengine.org/en/4.6/tutorials/io/runtime_file_loading_and_saving.html)
- [Meta: unsupported permissions on Horizon OS](https://developers.meta.com/horizon/documentation/android-apps/unsupported-permissions)
- [Meta community forum: file access after Quest moved to Android 12](https://communityforums.atmeta.com/discussions/dev-unity/video-files-in-quest2-are-no-longer-readable-/1053821)
- [Ghidra Sega Saturn loader](https://github.com/00mjk/ghidra-segasaturn-processor)
- [Ghidra SuperH processor pull request](https://github.com/NationalSecurityAgency/ghidra/pull/715)
- [Time Extension: Panzer Dragoon Saga PC port](https://www.timeextension.com/news/2026/02/panzer-dragoon-sagas-pc-port-was-abandoned-in-2020-but-its-developer-is-motivated-to-work-on-it-again)
- [Retro Handhelds: Panzer Dragoon Saga recomp](https://retrohandhelds.gg/panzer-dragoon-saga-recomp-in-the-works/)
- [Panzer Dragoon Legacy: the technology behind PD and PD Zwei](https://discuss.panzerdragoonlegacy.com/t/the-technology-behind-pd-and-pd-zwei/8142)
- [Panzer Dragoon Legacy: MIDI files extracted from Zwei and Saga](https://discuss.panzerdragoonlegacy.com/t/i-have-extracted-the-midi-files-from-panzer-dragoon-zwei-and-saga/9013)
- [Panzer Dragoon wiki: Emulation](https://panzerdragoon.fandom.com/wiki/Emulation)
