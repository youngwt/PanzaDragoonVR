# Stack

## Game (flight track)

- Godot 4.7 stable, C#. Godot has dedicated Quest 3 export on Horizon OS, and a universal OpenXR APK since 4.5.
- C# on Android has been supported since 4.2 and is still labelled experimental in the 4.7 docs.
- Frame rate target: 90 Hz.
- Runtime images: `Image.load_from_file`, then `ImageTexture`. Call `generate_mipmaps()` for the tiling floor and canopy images; model textures have no mipmaps (`asset-contract.md`).
- Runtime models: binary glTF loaded with `GLTFDocument` and `GLTFState` (`append_from_file` or `append_from_buffer`, then `generate_scene`), no editor import. Not yet run on the headset; story 1.7 is the first test.
- Asset folder: `/sdcard/Android/data/<package>/files`. SideQuest can copy into it. Quest moved to Android 12 scoped storage around OS v51; `MANAGE_EXTERNAL_STORAGE` is allowed only for file-manager apps.
- Reticle: a 2D sprite at a fixed distance from the player, as if on a sphere around them.
- Floor: one looping plane, moved by scrolling UVs, not geometry. Canopy: a backdrop at effective infinity.

## Extraction (pipeline track)

The decoders become one C# library, run by the game on the headset at first launch and by a PC command for reverse engineering; not built yet. The Python scripts in `tools/` are the prototypes it is ported from, standard library only:

| Script | Does |
|--------|------|
| `saturn_disc.py` | Reads the ISO9660 file system from the MODE1/2352 disc image |
| `cgb_textures.py` | Decodes `.CGB` + `.MDB` texture pairs to PNG |
| `png.py` | Minimal PNG writer |
| `mednafen_state.py` | Unpacks a Mednafen save state into RAM, VRAM and CRAM dumps |

Git-ignored working folders: `rom/` (disc image), `extracted/` (all output), `tools/mednafen/`.

## Reverse-engineering tools (local, not in git)

- Ghidra 12.1.4 with JDK 21 in `~/.local/opt`; commands `ghidra` and `ghidra-headless`. SH-2 support is built in.
- Saturn loader extension (VGKintsugi, 12.0.0). It needs a plain 2048-byte-sector ISO. In ISO mode it loads only `1ST_READ.PRG`; for `EPISODE3.PRG` load an emulator save state.
- Mednafen 1.32.1 in `tools/mednafen/`; `run.sh` boots the disc. Save states land in `tools/mednafen/home/mcs/`, gzip-compressed; unzip before loading into Ghidra. On the bound DualSense, L3 saves a state.
- Ghidra project: `extracted/ghidra/zwei`.

## Build toolchain (local, not in git)

Installed by `tools/setup-toolchain.sh` under `~/.local/opt`; `tools/toolchain-env.sh` sets the paths. See `tools/README.md`.

- Godot 4.7.2-stable (.NET build), self-contained, with export templates: `godot-4.7.2-stable-mono`
- .NET 9 SDK: `dotnet-9`
- Temurin JDK 17: `jdk-17`
- Android SDK with `adb` (`platform-tools`, `build-tools;36.1.0`, `platforms;android-36`): `android-sdk`

## References

- [Godot C# documentation](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/index.html)
- [Godot: runtime file loading](https://docs.godotengine.org/en/4.6/tutorials/io/runtime_file_loading_and_saving.html)
- [Meta: unsupported permissions on Horizon OS](https://developers.meta.com/horizon/documentation/android-apps/unsupported-permissions)
- [Ghidra Sega Saturn loader](https://github.com/00mjk/ghidra-segasaturn-processor)
