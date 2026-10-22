---
title: 'The colour-bank textures come out in colour'
type: 'feature'
ticket: '6'
created: '2026-10-18'
status: done
baseline_revision: '688159410156e6ac8de974ad8a6a92f6b50acc4e'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 170 of Episode 3's textures are numbered but not written: 161 in mode 4 (256 colours) and 9 in mode 0 (16 colours from a colour bank). Their colours come from VDP2 colour RAM, which spike 3 found that Episode 3 fills from colour upload tables in `EPISODE3.PRG`.

**Approach:** Read the colour upload tables from `EPISODE3.PRG`, found by their pattern rather than fixed offsets, and build colour RAM from the second table the author's disc holds (at 0xE23C), as the author chose on 2026-10-17. Decode the mode 4 and mode 0 textures from it under the numbers they already have. A disc with no such table leaves them out with a notice.

## Boundaries & Constraints

**Always:**
- Every rule follows `zwei-disc-formats.md`, "Palettes for colour banks": a table is pairs of a `0x25Fxxxxx` destination and a `0x060xxxxx` source, running from destination `0x25F00000` to the first other destination; each pair copies 0x200 bytes; sources are load addresses with `EPISODE3.PRG` loaded at 0x06080000; a mode 4 pixel's colour code is `((CMDCOLR & 0xFF00) | pixel) & 0x7FF`, a mode 0 pixel's `((CMDCOLR & 0xFFF0) | pixel) & 0x7FF`; pixel value 0 is transparent unless CMDPMOD bit 0x40 is set.
- Numbering, IDs and paths do not change: modes 1 and 5 output stays byte-identical, and the new PNGs take the numbers story 5 already reserved.
- Colours are copied, not taken from any save state.
- One-line errors and log lines, as `AGENTS.md` says.

**Never:**
- No use of the 0x288 table (a recorded lead), no VDP2 plane colours, no manifest (story 7).
- No change to the asset contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Author's disc | Two tables in `EPISODE3.PRG` | Colour RAM from the second; 161 mode 4 and 9 mode 0 PNGs written; nothing left out | No error expected |
| One table | Only one table found | Colour RAM from it | No error expected |
| No table | No table, or no `EPISODE3.PRG` | Modes 4 and 0 left out, as today; modes 1 and 5 written | One notice naming why |
| Bad pair | A pair whose source lies outside the file | That pair skipped; the rest applied | Noted in the result |
| Code past the filled blocks | A colour code no pair filled | That colour is black (0) | — |
| Transparency | Mode 4 and mode 0 records with and without CMDPMOD 0x40 | Pixel 0 transparent unless the bit is set | — |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Extraction.cs` -- `Decode(mdb, cgb, name)` switches on colour mode: writes 1 and 5, counts the rest in `leftOut`. `Run(imagePath, output)` opens the disc, loops `TextureFiles`, writes only after all decode. `DiscImage.Find(path)` and `ReadFile` give `EPISODE3.PRG`. Colour RAM is built once per extraction and passed to `Decode`.
- `lib/PdzDecoding/Textures.cs` -- `DecodeMode1`, `DecodeMode5`, `Rgb555`, `TextureRecord.IndexZeroTransparent`, `BytesNeeded` (mode 4 one byte per pixel, mode 0 half). Add mode 4 and mode 0 beside them.
- `game/DiscExtraction.cs`, `tools/pdz-extract/Program.cs` -- print left-out modes per file today; they should show the colour table used, or the notice when there is none.
- `tests/Decoding.Tests/SyntheticDisc.cs` -- builds discs from named files; add an `EPISODE3.PRG` with tables.
- Spike evidence (git-ignored): `extracted/spike-e03cmn-mode4-disc.png`, `extracted/spike-e03end-mode4-disc.png` and `extracted/spike-E03BOSS-mode0-disc.png`, drawn from the 0xE23C table by the same rules; the spike's scripts are in the session scratch folder `spike/`. The European ISO's Episode 3 files are byte-identical to the Japanese disc's.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/` -- find the upload tables, build colour RAM from the chosen one, decode modes 4 and 0, and record in the result which table was used or why none.
- [x] `tests/Decoding.Tests/` -- one or more tests per matrix row, and that modes 1 and 5 output is unchanged.
- [x] `game/DiscExtraction.cs`, `tools/pdz-extract/Program.cs`, `tools/README.md` -- report the table used, or the notice; nothing left out on the author's disc.

**Acceptance Criteria:**
- Given the author's disc, when `pdz-extract` runs, then it writes 420 textures from 9 files, and the 250 from before are byte-identical.
- Given the mode 4 PNGs of `E03CMN` and `E03END`, when their pixels are compared with the spike's colouring from the same table, then they match; and `E03BOSS`'s mode 0 shadows are one dark colour.
- Given a contact sheet of the new textures, when the author looks, then they look right.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

- New `lib/PdzDecoding/ColourBanks.cs`: `FindTables` scans even offsets for destination `0x25F00000` followed by a `0x060xxxxx` source, takes pairs until the first destination that is not `0x25Fxxxxx`, and resumes after the table. `Chosen` picks the second of two or more, else the only one. `Build` applies pairs in order to all-black colour RAM (0x1000 bytes); a pair whose source block lies outside the file, or whose destination block lies outside colour RAM, is skipped and listed. `ColourRam.Describe()` gives the one-line report.
- `Textures.DecodeMode4` / `DecodeMode0` beside modes 1 and 5. `Extraction.Run` builds colour RAM once per run; `ExtractionResult` gains `Colours` and `ColourNotice`, and `Details()` always carries the colour line. The left-out wording is now `left out: mode 0 9, ...`; the reason is in the colour line.
- Author's disc: 420 PNGs from 9 files, exit 0, nothing left out; the 250 from before byte-identical (compared with a run of the baseline build); the European ISO's 420 are identical to the Japanese ones. All 161 mode 4 PNGs of `E03CMN`/`E03END` match the spike's colouring pixel for pixel. The 9 mode 0 textures (CMDCOLR 0x2400, bank 4) each have one opaque colour, RGB555 0x0022. The spike script drew codes 0x400 up as black, so it does not match the mode 0 PNGs, which use the 0xE23C table's bank 4 block (0x34030).

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-18: high 0, medium 1, low 2, false 0, maybe-false 0. Three patched by the implementer, one carried.

- medium, patched: a damaged `EPISODE3.PRG` threw out of `Run` before any texture decoded, so nothing was written where modes 1 and 5 were written before; against the "No table" row. Reading it is now caught and treated as a missing program file, with a notice; test added.
- low, patched: `ColourRam.Describe` named the table from the choice rule, not from the table it was built from. `ColourRam` now stores the table's index; test added.
- low, patched: "1 blocks" now reads "1 block".
- carried: the author's look at the contact sheet of the new textures. Met on 2026-10-18: the author judged them right ("lgtm").

Also: `zwei-disc-formats.md`'s colour modes table no longer says mode 4 is "not yet in the extractor".

Re-verified after the patches: `tools/test.sh` (94 Python, 192 GameLogic, 92 Decoding tests) passes; `pdz-extract` on the Japanese disc reports the 0xE23C table (table 2 of 2, 5 blocks) and writes 420 textures from 9 files, identical to the implementer's run.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `pdz-extract` on `rom/JP/...cue` -- expected: 420 textures from 9 files, nothing left out, exit 0.
