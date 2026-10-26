---
title: 'Extraction writes the floor and canopy images'
type: 'feature'
ticket: '4'
created: '2026-10-25'
status: done
baseline_revision: '2c6233e2cb2ca803cfd0662aac6e859b93b2169a'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Spike 3.2 found the floor and canopy in `EPISODE3.PRG` (cells and maps packed with an LZ77 variant, colours plain) and proved it with a Python probe, but extraction does not write them, so the game still shows placeholders for `e03.floor` and `e03.canopy` (CAP-3, CAP-4).

**Approach:** Port the probe's unpacking, source finding and rendering into the decoding library, and have `Extraction.Run` write the two planes as PNGs listed in the manifest under `e03.floor` and `e03.canopy`, on the PC and the headset alike. Per the author's decisions (2026-10-25): each image is the whole 512x512 page as VDP2 holds it, and the VDP2 plane settings (16x16 cells, 256 colours, one-word names with PNCR 0x8008's supplement) are constants in the library, documented as read from the save state.

## Boundaries & Constraints

**Always:**
- Sources are found as the probe finds them (the literal pool: 0x25E40000 then the cells' pointer, 0x25E60000 then the map list, the colour literals 0x3C and 0x30 bytes before), never fixed offsets, so the JP and PAL discs both work.
- The planes are an addition: a disc whose planes cannot be found or unpacked still has its textures written, with a one-line notice naming why (as colours do today); a malformed packed stream inside an otherwise found source is a notice too, not an error.
- Pattern names are honoured as VDP2 reads them (cell, palette, flips), not assumed sequential.
- Output byte-identical on the PC and the headset (the existing deterministic PNG writer); the manifest stays sorted by ID.
- `Extraction.Version` rises to 0.2, as the output changed.
- Unit tests build their own small `EPISODE3.PRG` and packed streams; they never read the disc.

**Never:**
- No change to texture numbering or IDs, the game's code, the tuning file, or the asset contract's existing rules beyond adding where the two plane images are written.
- No new progress stages: the planes are decoded with the colours and written with the textures, so the game's board lines stay as they are.
- Nothing from the disc is committed; output only under `extracted/`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| JP or PAL disc | real disc | `e03.floor` and `e03.canopy` 512x512 PNGs plus every texture as before | none |
| No literal pool | `EPISODE3.PRG` without the pattern | textures written; notice "no floor and canopy: ..." | no exception |
| No `EPISODE3.PRG` | disc lacks it | textures as today; plane notice names the missing file | no exception |
| Bad stream | pointer outside the file, or a stream that runs off the end | textures written; notice names the stream | no exception |
| Flipped names | synthetic map with H/V flip bits | the cell drawn flipped | none |

</frozen-after-approval>

## Code Map

- `tools/plane_probe.py` -- the confirmed reference: `unpack` (LZ77: flags LSB first; short copy back 1-256, long copy back 1-8192; word 0 ends), `find_sources`, the rendering loop and colour lookup. Port it; do not change it.
- `tests/tools/test_plane_probe.py` -- hand-built streams to port as C# tests.
- `lib/PdzDecoding/Extraction.cs` -- `Run` (reads `EPISODE3.PRG` through `ColourBanks.FromDisc`; read it once and share it), `ImageFolder` "images/e03", staging and moving into place, `ExtractionResult` (`Details()`, `ColourLine()`), `Version` "0.1".
- `lib/PdzDecoding/ColourBanks.cs` -- `ProgramFile`, `LoadAddress`, `FromDisc`/`FromProgram`; the same file feeds the planes.
- `lib/PdzDecoding/Textures.cs` -- `Rgb555`/`ColourPixel`: reuse the library's RGB555 to 8-bit conversion.
- `lib/PdzDecoding/PngWriter.cs` -- `Encode(width, height, rgba)`; `Manifest.cs` -- `Write` sorts entries.
- `tools/pdz-extract/Program.cs` and `game/DiscExtraction.cs` -- print/log `Details()`; check that the plane line reads well in both.
- `tests/Decoding.Tests/ExtractionTests.cs` -- `SyntheticDisc`, `DiscWith`, `Program(...)` helpers; the expected manifest JSON with "pdz-extract 0.1" (line ~359) changes with the version.
- `asset-contract.md` -- "Extracted textures" / "What extraction writes": add where the plane images go (suggest `images/e03/floor.png`, `images/e03/canopy.png`); `tools/README.md` -- the extraction sections and log table.
- `tools/compare-headset.sh` -- PC against headset, byte for byte.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Planes.cs` -- unpacker, source finding, rendering to 512x512 RGBA, the VDP2 constants documented; a result or a one-line notice.
- [x] `lib/PdzDecoding/Extraction.cs`, `ColourBanks.cs` -- read `EPISODE3.PRG` once; add the planes to the staged files, the manifest and the result (`Details()` names them or the notice); Version 0.2.
- [x] `tests/Decoding.Tests/PlanesTests.cs` + `ExtractionTests.cs` -- the matrix rows and the ported probe tests.
- [x] `asset-contract.md`, `tools/README.md` -- the plane paths, IDs and notice.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then all pass; the game builds with 0 warnings.
- Given `pdz-extract` on the JP cue and the PAL ISO into `extracted/`, when compared with `plane_probe.py`'s renders, then the floor and canopy pixels are equal, and every texture PNG is byte-identical to a run at the baseline.
- Given the headset after `build-quest.sh` extracts in development mode, when `compare-headset.sh` runs, then it reports identical, and the author sees the forest floor and canopy in place of the chequers.

## Implementation Notes

- `ColourBanks.ReadProgram` reads `EPISODE3.PRG` once; `Extraction.Run` passes the bytes to `ColourBanks.FromProgram` and `Planes.FromProgram`, so a missing or unreadable file gives the same reason to both notices.
- `ExtractionResult` gains `PlaneFiles`, `PlaneSources`, `PlaneNotice` and `PlaneLine()`; `Files` stays textures only, so the game's and the PC command's texture counts are unchanged. In `Details()` the plane line comes after "left out", so the colour line and the modes it leaves out stay together.
- Every plane pixel is opaque, as the probe draws them (pixel 0 is not made transparent).
- A stream is unpacked with a limit of the VRAM left after its destination, so a malformed stream cannot grow without bound; going over is a notice.
- `PdzDecoding.csproj`'s `Version` rose to 0.2.0 with `Extraction.Version`.
- Verified on the PC: JP and PAL floor and canopy pixels equal `plane_probe.py`'s renders; every texture PNG is byte-identical to a baseline (2c6233e) run; `write_manifest.py --check` passes with 422 assets. The headset step (`compare-headset.sh`, and the author looking) is not done: no headset was connected.
- **Headset, 2026-10-26, with the author:** `build-quest.sh` installed and launched in development mode; the log showed "extracted 420 textures ... floor and canopy from EPISODE3.PRG: cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244", then "e03.floor: using .../images/e03/floor.png (512 x 512)" and "supplied: e03.floor, e03.canopy". `compare-headset.sh` reported "identical: 423 files, the manifest included". The author saw the forest floor and canopy in place of the chequers ("it works"), at the placeholders' tile sizes until story 3.5.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-25: high 0, medium 0, low 4, false 0, maybe-false 0. The reviewer re-ran the decoding tests and the builds and checked the port against `plane_probe.py` and `mednafen_state.py` (flags, copies, character supplement, palette, flips, colour index, opaque alpha).

- low, patch: a literal pool whose cells or map-list pointer lay outside the file was skipped, so the notice said there was no pool, against the matrix row "notice names the stream". The pool is now matched on its two VRAM literals and the notice names the bad pointer; two tests added.
- low, patch: `tools/README.md`'s list of plane notices missed the map-past-VRAM notice and misquoted the one-map form. Now complete.
- low, patch: the contract said "the folder then lacks the two IDs"; an earlier run's PNGs stay, unlisted. Now says the manifest lacks them; the general manifest example also reads 0.2.
- low, rejected: the game's "nothing written" log line (and `pdz-extract`'s "wrote 0 textures") would be wrong if every texture file gave no textures but the planes were written. No real disc does this, and the fix is in game code the plan keeps unchanged.
- Acceptance criterion 3 (the headset) is checked with the author before the plan is presented.

After the patches: `tools/test.sh` 105 Python, 296 GameLogic, 152 Decoding, all pass; the game builds with 0 warnings; `pdz-extract` on the JP cue gives output identical to the run before the patches.

## Design Notes

- The planes come from `EPISODE3.PRG`, not a texture file, so they are not a `TextureFiles` entry; they are listed in the result beside the disc files, like the colour table.
- The game needs no change: it already asks for `e03.floor` and `e03.canopy`. At the built-in `floor_tile` of 2 m a 512-pixel page will look tiny; story 3.5 sets the real sizes in the tuning file.
- Pixel comparison with the probe: decode both PNGs' image data with Python's `zlib` (the library stores uncompressed deflate) rather than comparing files, since the two encoders differ.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `dotnet run --project tools/pdz-extract -- "rom/JP/Panzer Dragoon II Zwei (Japan).cue" extracted/pdz-extract-3-4` (and the PAL ISO) -- expected: the plane line and both PNGs.
- `tools/build-quest.sh`, then `tools/compare-headset.sh "rom/JP/Panzer Dragoon II Zwei (Japan).cue"` -- expected: identical.
