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

## Disc analysis: first findings (2026-10-06)

Worked out directly from the disc image with the scripts in `tools/`. This supersedes "No documented formats" above for textures.

### Disc

- Standard Saturn disc: `PANZER DRAGOON ZWEI`, GS-9049, V1.001, 1996-02-27. Track 1 is MODE1/2352 with an ordinary ISO9660 file system; tracks 2 to 4 are short CD audio.
- 308 files, named by episode. Episode 3 is `EPISODE3.{PRG,DAT,SND}`, `E03CMN`, `E03B0N0`..`E03B1N2`, `E03BOSS`, `E03END` (each an `.MDB` + `.CGB` pair), `E3BOSS.SND`, and `DRAnnE03.GRB`. `EPISODE3.PRG` loads the `E03*` and `COMMON` pairs by name.
- The ten dragon forms are `DRA01A`..`DRA10J` (`.MDB`, `.CGB`, `.MTB`).

### Textures (`.CGB` + `.MDB`): decoded

- `.CGB` is raw VDP1 pixel data with no header.
- The paired `.MDB` holds, per polygon, four big-endian words in VDP1 command order: CMDPMOD, CMDCOLR, CMDSRCA, CMDSIZE. Width is `(CMDSIZE >> 8) * 8`, height is `CMDSIZE & 0xFF`.
- CMDSRCA and CMDCOLR are VRAM addresses divided by 8. The address the game loads each `.CGB` at is not in the file; `tools/cgb_textures.py` infers it from the records (for example 0x49000 for `E03B0N0`, 0x20000 for `E03CMN`).
- Colour mode 1 (16 colours through a lookup table): the 32-byte RGB555 table is stored inside the `.CGB`, so these files are self-contained. Confirmed by eye on `E03B0N0`, `E03BOSS` and `DRA01A`.
- 9,020 textures found across all 77 pairs: 4,873 in mode 1 (decoded in colour), 4,033 in mode 4 (256 colours; palette lives elsewhere, not found, output is grey and unverified), 100 in mode 0, 13 in mode 5.

### Other files

- `.GRB`: 16-bit words with the top bit set, so RGB555 colour data. Guess: Gouraud shading tables for the dragon, one set per episode's lighting.
- `EPISODE3.DAT`: not images. Repeating 3x4 matrices in 4.12 fixed point (rotation plus position). Looks like pre-recorded motion; whose motion (camera rail, enemies, animation) is not yet known.
- Floor and canopy planes: not found yet. Not in the `.CGB` files. Lead: the last quarter of `EPISODE3.PRG` has very high entropy, consistent with packed image data. `EPISODE2.SCB` (131,072 bytes) is the only `.SCB` on the disc and may be a scroll plane for Episode 2.
- `.MDB` geometry: not yet decoded beyond the texture records. Vertices appear to be big-endian signed 16-bit.

### Local tool setup (not in git)

- Ghidra 12.1.4 and JDK 21 are in `~/.local/opt`, launched with `ghidra` and `ghidra-headless`. The community Saturn loader (VGKintsugi, 12.0.0) is installed in Ghidra's `Extensions` folder. It needs a plain 2048-byte-sector ISO and in ISO mode loads only `1ST_READ.PRG`; for `EPISODE3.PRG` use an emulator save state.
- Mednafen 1.32.1 is in `tools/mednafen/` (git-ignored); `run.sh` boots the Zwei disc. Save states go to `tools/mednafen/home/mcs/` and are gzip-compressed; unzip before loading into Ghidra.

### Next

1. Find the floor and canopy planes (milestone 2).
2. Find the 256-colour palettes for mode 4 textures.
3. Decode `.MDB` geometry.
4. Load an Episode 3 save state into Ghidra and identify what `EPISODE3.DAT` drives.
