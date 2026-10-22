---
title: 'Every accepted disc image layout is read'
type: 'feature'
ticket: '4'
created: '2026-10-17'
status: done
baseline_revision: '3910800832c6cbb6ab20d617ea42253e784c2755'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The library reads only a cue sheet whose track 1 is its own MODE1/2352 `.bin`. The asset contract accepts two more layouts in `disc/`: a cue sheet with one `.bin` holding every track, and a plain ISO of 2048-byte sectors. The author now has both kinds of dump: the Japanese disc as per-track bin/cue and the European disc as an ISO.

**Approach:** Teach the library to read track 1 from a single-file cue sheet and from a plain ISO, and the disc folder search to find an ISO as well as a cue sheet. The Saturn header, already read, keeps feeding `source`. The European ISO is the real-world test of the new layout and of "attempted, not refused".

## Boundaries & Constraints

**Always:**
- The disc folder rule stays the contract's: one disc image in `disc/` (a `.cue` with the `.bin` files it names, or one `.iso`); more than one image is reported and nothing is extracted.
- The Japanese per-track dump extracts exactly as today: the same 250 files, byte for byte.
- Errors name the file and the reason, on one line, as `AGENTS.md` says.
- Tests use synthetic images built in the tests; no disc data.

**Never:**
- No compressed images (CHD and the like), no MODE2 or audio-only track 1, no change to texture decoding or numbering.
- No change to the asset contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Per-track cue | `.cue` naming one `.bin` per track (today's) | Same files and header as today | No error expected |
| Single-file cue | `.cue` naming one `.bin`; track 1 MODE1/2352 at INDEX 01, audio tracks after it in the same file | Same files and header as the per-track layout of the same disc | No error expected |
| Cue with MODE1/2048 | Track 1 declared MODE1/2048 | Read as 2048-byte sectors | No error expected |
| Plain ISO | One `.iso` of 2048-byte sectors | Files and header read from sector 16 / sector 0 | No error expected |
| ISO and cue together | `disc/` holds a `.cue` and an `.iso` | Nothing extracted | More than one disc image, naming both |
| Not ISO9660 | An `.iso` with no volume descriptor at sector 16 | Nothing extracted | Cannot be read, with the reason |
| Unsupported track 1 | Cue whose track 1 is MODE2 or AUDIO | Nothing extracted | Names the mode found |
| Another disc | The European ISO (MK-81022 V1.000) | Whatever the decoders find; `source` from its header | Missing files noted, as story 5 does |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/CueSheet.cs` -- `Parse` finds track 1's `FILE` and its `INDEX 01` start; rejects any mode but `MODE1/2352`. `DataTrack(BinPath, StartSector)`: add the sector size.
- `lib/PdzDecoding/DiscImage.cs` -- constants `RawSectorSize 2352`, `SectorHeader 16`, `SectorSize 2048`; `OpenCue`, `Open(stream, startSector, name)`, `ReadSectors` assume 2352-byte raw sectors. Generalise to a sector size and header offset (2352/16 or 2048/0); add an ISO opener.
- `lib/PdzDecoding/Extraction.cs` -- `DiscFolder.Find` looks only for `.cue` (`DiscFolderState`: None, One, Several; `DiscSearch.CuePath`); `Extraction.Run(cuePath, output)` opens with `DiscImage.OpenCue`. Generalise to an image path of either kind.
- `game/DiscExtraction.cs`, `tools/pdz-extract/Program.cs` -- pass the image path; PC usage becomes `pdz-extract <disc.cue|disc.iso> <output folder>`.
- `tests/Decoding.Tests/SyntheticDisc.cs` -- builds 2352-byte per-track images; extend for single-file and 2048-byte layouts.
- The author's dumps (git-ignored): `rom/JP/Panzer Dragoon II Zwei (Japan).cue` with four `.bin` files (track 1 MODE1/2352, 428,906,016 bytes); `rom/PAL/Panzer Dragoon II Zwei (Europe).iso`, 190,508 sectors of 2048 bytes, header MK-81022 V1.000, 1996-03-14.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/CueSheet.cs`, `lib/PdzDecoding/DiscImage.cs` -- single-file cue sheets, MODE1/2048 tracks, and plain ISOs.
- [x] `lib/PdzDecoding/Extraction.cs` -- the disc folder search finds one `.cue` or one `.iso`; extraction opens either.
- [x] `tests/Decoding.Tests/` -- one or more tests per matrix row that synthetic images can cover.
- [x] `game/DiscExtraction.cs`, `tools/pdz-extract/Program.cs`, `tools/README.md` -- accept and document both kinds of image.

**Acceptance Criteria:**
- Given the Japanese per-track dump, when `pdz-extract` runs, then its 250 files are byte-identical to today's.
- Given a single-file `.bin` and `.cue` made in the scratch folder from the Japanese dump's tracks, when `pdz-extract` runs on it, then it writes the same files as the per-track dump.
- Given the European ISO, when `pdz-extract` runs, then it reads the disc, reports MK-81022 V1.000, and writes what the decoders find, with any missing files named.
- Given the game on the desktop with `PDZ_ASSETS` holding the European ISO in `disc/`, when it starts, then it extracts as the PC command does.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

- `CueSheet.Parse` already took track 1's own `FILE` and `INDEX 01`, so a single-file cue needed no new parsing: `DataTrack` gained `SectorSize` (2352 for `MODE1/2352`, 2048 for `MODE1/2048`); any other mode is refused as `track 1 is <mode>, and only MODE1/2352 or MODE1/2048 is read`. Constant `DataMode` became `RawMode` and `CookedMode`.
- `DiscImage` keeps a sector size and data offset (2352/16 or 2048/0). New `OpenImage(path)` picks `OpenCue` or the new `OpenIso` by extension (case-insensitive) and refuses anything else (`<path>: not a disc image: only a .cue sheet or an .iso is read`). `Open(stream, startSector, name, sectorSize = 2352)` keeps its old call shape. 2048-byte images are read in one seek and read.
- `DiscSearch.CuePath` is renamed `ImagePath`; `DiscFolder.Find` matches `.cue` and `.iso` (`DiscFolder.ImageExtensions`); a `.bin` never counts as an image. `Extraction.Run(imagePath, output)` opens through `OpenImage`.
- `SyntheticDisc.Build(sectorSize)`, `WriteCue(..., mode)`, `WriteSingleFileCue` (track 1 then audio sectors in one `.bin`, `TRACK 02 AUDIO` with INDEX 00/01 after it) and `WriteIso`.
- Real-disc runs: the Japanese per-track cue gave the same 250 files as a baseline taken before the change (sha256 identical); a single-file `.bin`/`.cue` made in the scratch folder from its four tracks gave the same 250; the European ISO reported `MK-81022 V1.000` and gave 250 files byte-identical to the Japanese ones, with no file missing. The desktop game with `PDZ_ASSETS` holding the ISO (symlinked) in `disc/` logged `extracted 250 textures from 8 disc files of MK-81022 V1.000` and wrote the same bytes as the PC command.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-17: high 0, medium 0, low 4, false 0, maybe-false 0. One patched, one patched and deferred, two rejected.

- low, patched and deferred: a cue sheet beside the `.iso` it names counts as two images and extracts nothing. That follows the contract's wording; the README's "more than one disc image" row now tells the player to keep only the `.iso`, and whether such an `.iso` should count as the cue's own file is in `deferred-work.md`.
- low, patched: one README sentence still named only `rom/JP/`; it now names `rom/PAL/` too.
- low, rejected: no test covers a single-file cue whose track 1 starts after sector 0. On a Saturn disc track 1 starts at 0; the seek arithmetic was read and is right.
- low, rejected: `DiscImage.Open` with a bad sector size throws `ArgumentOutOfRangeException`, not `DecodingException`. No caller passes another size; argument checking, not a disc problem.

`tools/test.sh` passes after the README edits (no code changed by the review).

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `pdz-extract` on `rom/JP/...cue`, on a scratch single-file cue, and on `rom/PAL/...iso` -- expected: as the criteria say; scratch output only under `extracted/` or the session scratch folder.
