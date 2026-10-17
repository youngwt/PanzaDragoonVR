---
title: 'One texture file extracted on the headset by the C# library'
type: 'feature'
ticket: '2'
created: '2026-10-17'
status: 'built'
baseline_revision: 'f70902f4e1a21c1bbb60866d0e84d2dd96115fe8'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Nothing in C# can read a Saturn disc. The Python prototypes in `tools/` decode textures on the PC only, and the epic needs one C# library that both the game (on the headset) and a PC command run.

**Approach:** The tracer bullet. A new C# decoding library reads the ISO9660 file system from a per-track bin/cue disc image in `disc/`, and decodes the mode 1 (16-colour lookup table) textures of `E03B0N0` to PNG, numbered and placed as the asset contract's "Extracted textures" says. The game runs it at launch and writes the PNGs into the asset folder; a PC command writes the same files to a folder on the PC.

## Boundaries & Constraints

**Always:**
- All Saturn-format knowledge lives in the library; the game and the PC command only call it. The library uses no Godot types.
- IDs, file numbering (all four record words) and paths follow the asset contract exactly.
- Nothing from the disc lands in git or the APK.
- The game still always runs: no disc image, or any extraction error, logs a `PDZ-ASSETS` line and carries on.
- Conventions in `AGENTS.md`: tool errors, log lines, test names.

**Never:**
- No manifest writing (story 7), no other layouts than a cue sheet with a MODE1/2352 track 1 (story 4), no other files or colour modes (story 5), no staging, modes or progress board (stories 9 and 10).
- No change to how the game loads assets from the manifest.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Disc present | `disc/` holds the author's `.cue` and its `.bin` files | `images/e03/e03b0n0/0000.png` onwards written; one log line with the count | No error expected |
| No disc | `disc/` absent or empty | Nothing written | One notice line |
| Bad image | Cue names a missing `.bin`, or no ISO9660 volume at sector 16 | Nothing written | One warning line with the reason; the scene runs |
| Two records, same four words | `.MDB` repeats a record | One texture, one number | — |
| Transparency | Mode 1 record with and without the CMDPMOD 0x40 bit | Index 0 transparent unless the bit is set | — |

</frozen-after-approval>

## Code Map

- `tools/saturn_disc.py` -- 2352-byte sectors, user data at byte 16, PVD at sector 16, recursive ISO9660 directory walk, Saturn header at sector 0. Port the reading, not the CLI.
- `tools/cgb_textures.py` -- `find_records` (every 2-byte offset, `pmod & 0xFF00 == 0x0400`), `infer_base`, `decode` (mode 1: 4-bit indices into a 32-byte RGB555 table at `(colr - base) * 8`; index 0 transparent unless `pmod & 0x40`), `rgb555`. Its de-duplication key and file names are superseded by the contract (all four words; `e03.tex.<file>.<n>` numbered by first record offset).
- `tools/png.py` -- the PNG layout to mirror: signature, IHDR (8-bit RGBA), one IDAT, IEND.
- `game/AssetFolder.cs` -- `Locate()` (private) finds the folder (fixed path on the Quest, `PDZ_ASSETS` on the desktop). Expose the folder path so extraction can run before `AssetFolder.Open()`.
- `game/Main.cs` `LoadAssets()` -- extraction goes before `AssetFolder.Open()`, synchronously for now (story 9 moves it off the main thread).
- `game/PanzaDragoonVR.csproj` -- desktop `net10.0`, Android `net9.0`. `game/PanzaDragoonVR.sln` holds only the game.
- `tests/GameLogic.Tests` -- compiles `game/Logic` directly; `tools/test.sh` runs Python then `dotnet test` on it.
- The author's dump: `rom/Panzer Dragoon II Zwei (Japan)/`, a `.cue` naming four `.bin` files (track 1 MODE1/2352, 428,906,016 bytes; tracks 2-4 audio). Git-ignored.
- On the headset the asset folder is `/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/`; host `adb` through `tools/quest-adb.sh`.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/` -- the library project, targeting `net9.0` and `net10.0`: cue sheet reading (track 1 file), 2352-byte sector reading, ISO9660 directory walk and file read, Saturn header, `.MDB` record finding and numbering, mode 1 decoding, a deterministic RGBA PNG writer, and one entry point that extracts `E03B0N0`'s mode 1 textures into a folder.
- [x] `tests/Decoding.Tests/` -- xUnit tests on small synthetic images and `.MDB`/`.CGB` pairs built in the tests (no disc data): one or more per matrix row, plus the PNG writer and numbering; `tools/test.sh` runs them too.
- [x] `tools/pdz-extract/` -- the PC command: `pdz-extract <disc.cue> <output folder>`, following the `tools/` error and exit rules.
- [x] `game/PanzaDragoonVR.csproj`, `game/AssetFolder.cs`, `game/Main.cs` -- reference the library; at launch, extract when `disc/` holds a `.cue`, then load as before.
- [x] `tools/README.md`, `AGENTS.md` -- the library, the PC command, the new test project, and how to copy a disc image to the headset.

**Acceptance Criteria:**
- Given the author's disc image copied into `disc/` on the headset, when the app starts, then `images/e03/e03b0n0/` holds the mode 1 PNGs and the log names how many.
- Given the same disc image, when the PC command runs, then it writes the same file names, and the files compare identical to those pulled from the headset.
- Given the built APK, when its file list is read, then it holds the library and no disc data or extracted image.
- Given `tools/test.sh`, when it runs, then every test passes.
- Given the PNGs, when the author looks at them, then they match the prototype's `E03B0N0` mode 1 output by eye.

## Implementation Notes

- **Numbering rule as built.** A texture is a record `Textures.FindRecords` finds (the prototype's scan, including its quirk that the last 8 bytes never start a record) that fits the `.CGB` at the inferred base (the prototype's own filter). Records in every colour mode are numbered, not only mode 1, so story 5 adds modes without renumbering; non-mode-1 textures are numbered but not written, so a file may have gaps. For `E03B0N0` every texture is mode 1: 28 textures, `0000`-`0027`, the same count as the prototype.
- **Prototype match.** All 28 PNGs decode to pixels identical to `extracted/textures/E03B0N0/*.png` from `cgb_textures.py` (compared raw RGBA, every one matched one-to-one).
- **Identical PNGs: the fallback was taken.** zlib output differs between runtimes: the same library on the PC under .NET 9 (zlib-ng 2.2.1) and .NET 10 (zlib-ng 2.2.5) gave different bytes for 3 of 28 PNGs, and the APK's `libSystem.IO.Compression.Native.so` links Android's system `libz.so` (classic zlib), a third compressor. So IDAT is now stored deflate blocks written by the library itself (`PngWriter.Compress`), which made .NET 9 and .NET 10 output identical. Size cost for `E03B0N0`: 110 KB instead of 15 KB (about 7x).
- **Project reference works for Android.** The Godot export packed `PdzDecoding.dll` (net9.0 build) from the `ProjectReference`; the compile-the-sources fallback was not needed. `game/PanzaDragoonVR.sln` still holds only the game.
- **No-write on failure.** `Extraction.Run` decodes everything in memory before writing, so a decoding error (a bad image or a missing disc file) writes nothing, and with no PNG to write the folder is not created; a write failing part way may leave some PNGs until staging (story 9).
- **Disc folder.** `DiscFolder.Find` looks only for `.cue` sheets in `disc/`; more than one is a warning and nothing is extracted, as the contract says. An `.iso` is ignored until story 4.
- **Not done here: the headset.** No headset was connected, so the first two acceptance criteria (extraction on the headset, PC files identical to those pulled from it) and the author's look at the PNGs are still to be run. Desktop Godot runs covered the disc present, no disc and bad image rows.

- **Headset checks, 2026-10-17.** The author's disc image (the `.cue` and four `.bin` files, 475 MB) was pushed into `files/disc/` with host `adb`, which needed `adb kill-server` and `start-server` before it saw the headset. After `tools/build-quest.sh`, the log read `extracted 28 textures from E03B0N0 of GS-9049 V1.001 ... in 0.1 s`, and `files/images/e03/e03b0n0/` held 28 PNGs. Pulled back, they compared identical to `pdz-extract`'s output (`diff -r`), so stored deflate holds on the device's .NET 9. The author looked at a contact sheet of all 28 and judged them right ("looks like it could be from an enemy"). The disc image is left in `files/disc/` on the headset for the next stories.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-17: high 0, medium 1, low 5, false 0, maybe-false 0. Five patched by the implementer, one deferred, one carried.

- low, patched: `DecodingException` messages and the PC command's IO errors ended with .NET's own text and its full stop (e.g. "...is denied.; nothing extracted"). Now passed through `DecodingException.OneLine`; test added.
- low, patched: the game logged every decoding problem as "the disc image cannot be read", which is wrong for a readable disc without `E03B0N0`. Now "nothing extracted from the disc image: <reason>".
- low, patched: the output folder was created and "extracted 0 textures" logged when no mode 1 texture was found. The folder is now created only with something to write, and the log says nothing was written; test added.
- low, patched: the docs said a disc that cannot be read writes nothing, which reads as covering write failures. Now: decoding errors write nothing; a write failing part way may leave files until story 9's staging.
- low, patched: the APK check's "no `.bin`" failed on every build because of Godot's `uid_cache.bin`. It now names the disc's track files, `.cue`, `disc/` and `images/e03/`.
- medium, deferred to story 11: the tests run only on .NET 10, so the APK's .NET 9 build of the library is never tested; only a one-off manual run showed .NET 9 and 10 write identical PNGs. Story 11 (PC command matches the headset byte for byte) is where that is proven on the device; the toolchain has no .NET 9 runtime to run tests on.
- carried: acceptance criteria 1, 2 and 5 needed the headset, which was not connected at review. Since met: see the headset checks in Implementation Notes.

Re-verified after the patches: `tools/test.sh` (94 Python, 192 GameLogic, 44 Decoding tests) passes; `pdz-extract` on the author's disc writes 28 PNGs; `build-quest.sh --no-install` builds an APK holding `PdzDecoding.dll` and no `.cue`, disc track, `disc/` or `images/e03` file.

## Design Notes

- **A real library project, with a fallback.** A `ProjectReference` from the Godot project to `lib/PdzDecoding` keeps one library for game and PC command. If Godot's Android export cannot pack a referenced project, compile the library's sources into the game instead (as the tests compile `game/Logic`) and record it; the PC command still references the project.
- **Identical PNGs.** .NET's zlib on Android (.NET 9) and the PC (.NET 10) might not compress identically. The second criterion checks this now. If the bytes differ, write IDAT with uncompressed deflate blocks, which is identical everywhere, and record the size cost.
- **Copying the disc.** The image is about 475 MB; `adb push` of the `.cue` and its four `.bin` files into `files/disc/` through host `adb` (the home folder is shared with the host). Never through `push-assets.sh`, which checks a manifest.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all tests pass, including `tests/Decoding.Tests`.
- `dotnet run --project tools/pdz-extract -- "rom/Panzer Dragoon II Zwei (Japan)/Panzer Dragoon II Zwei (Japan).cue" <scratch>` -- expected: the PNGs written, exit 0.
- `tools/build-quest.sh`, then `unzip -l game/build/PanzaDragoonVR.apk` -- expected: the library's DLL present; no `.cue`, no track `.bin` from the disc (`assets/.godot/uid_cache.bin` is Godot's own), nothing under `disc/` or `images/e03/`.

**Manual checks (if no CLI):**
- On the headset after launch, `adb shell ls` of `files/images/e03/e03b0n0/` and `adb pull` of it, compared with the PC output; the author looks at a few PNGs.
