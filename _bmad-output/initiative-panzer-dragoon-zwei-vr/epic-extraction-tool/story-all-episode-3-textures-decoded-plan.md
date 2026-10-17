---
title: 'All Episode 3 textures decoded'
type: 'feature'
ticket: '5'
created: '2026-10-17'
status: 'built'
baseline_revision: '364670722ce857269c1101db81f97c7a3eab3bc7'
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
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extractor decodes only `E03B0N0`, and only its mode 1 textures. Episode 3 has nine texture files, and the textures that carry their own colours (mode 1, and mode 5 true colour) should all come out on the PC and the headset.

**Approach:** Extend the library's extraction from one file to all nine Episode 3 files the asset contract names, and add mode 5 decoding beside mode 1. Modes 4 and 0 keep their numbers but are not written; story 6 colours them. The game and the PC command call the same entry point, so both get every file.

## Boundaries & Constraints

**Always:**
- Numbering, IDs and paths stay exactly as the asset contract's "Extracted textures" says; `E03B0N0`'s 28 files come out byte-identical to today's.
- PNGs come from the existing deterministic writer.
- A file the disc lacks is left out with a note in the result and the log, and the others are still extracted; a disc with none of the nine is an error, as a disc without `E03B0N0` is today. Other discs are attempted, not refused.
- Decoding errors write nothing; files are written only after every texture has decoded.

**Never:**
- No colour-bank decoding (modes 4 and 0: story 6), no manifest (story 7), no new disc layouts (story 4), no staging or threading (story 9).
- No change to the asset contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Author's disc | The nine Episode 3 files present | Mode 1 and mode 5 PNGs for each file under `images/e03/<file>/`; modes 4 and 0 counted as left out | No error expected |
| Mode 5 | A record in mode 5 | 16-bit pixels as 15-bit RGB, alpha 255 when bit 15 is set, else 0 (as the prototype) | — |
| A file missing | Disc lacks one of the nine | The rest extracted; the result and log name the missing file | Not an error |
| None present | Disc holds none of the nine | Nothing written | Error naming the files looked for |
| File with no textures | A `.MDB` with no records that fit | No folder for that file | Noted in the result |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Extraction.cs` -- `Run(cue, output)` reads `TextureFile = "E03B0N0"` only; `Decode(mdb, cgb, name)` numbers every record (`Textures.Number`) and writes mode 1, counting the rest as skipped; writes only after all decode; creates a folder only with something to write. `ExtractionResult` carries `Files`, `Skipped`, `Source`, `DiscFileName`. Extend to a list of files.
- `lib/PdzDecoding/Textures.cs` -- `FindRecords`, `Fits`, `InferBase`, `Number`, `DecodeMode1`, `Rgb555`. Add mode 5 beside `DecodeMode1`; `BytesNeeded` already sizes mode 5 at two bytes per pixel.
- `tools/cgb_textures.py` `decode` -- mode 5: `rgb555(word)` and alpha 255 if `word & 0x8000` else 0.
- `game/DiscExtraction.cs` -- one `PDZ-ASSETS` line per outcome; update its wording for several files and any missing ones.
- `tools/pdz-extract/Program.cs` -- prints the result; update for several files.
- `tests/Decoding.Tests/` -- `SyntheticDisc.cs` builds discs with given files; extend for several files and a mode 5 record.
- Expected counts on the author's disc, by the prototype's log (whose de-duplication differs slightly from the contract's four-word rule): mode 1 250 (`E03B0N0` 28, `E03B0N1` 10, `E03B0N2` 28, `E03B1N0` 26, `E03B1N1` 43, `E03B1N2` 30, `E03BOSS` 77, `E03CMN` 8); mode 5 none; mode 4 161 and mode 0 9 left out.
- The prototype's per-file PNGs for comparison: `extracted/textures/<FILE>/` (git-ignored).

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Textures.cs` -- mode 5 decoding.
- [x] `lib/PdzDecoding/Extraction.cs` -- extract the nine files the contract names, modes 1 and 5, with missing files and empty files recorded in the result.
- [x] `tests/Decoding.Tests/` -- one or more tests per matrix row, and that `E03B0N0`'s output is unchanged.
- [x] `game/DiscExtraction.cs`, `tools/pdz-extract/Program.cs` -- log and print the new result: textures per file, missing files, left-out modes.
- [x] `tools/README.md` -- what the extractor now writes.

**Acceptance Criteria:**
- Given the author's disc, when `pdz-extract` runs, then each Episode 3 file's mode 1 textures are written and their pixels match the prototype's PNGs for the same textures.
- Given the same disc, when the game runs extraction on the desktop with `PDZ_ASSETS`, then it writes the same files as the PC command, byte for byte.
- Given a contact sheet of every texture written, when the author looks, then they look right.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

- `ExtractionResult` is now `(Source, DiscFiles, Missing, Folder)`: one `DiscFileResult` per pair found (name, folder, PNGs, left-out counts by mode), `MissingDiscFile(Name, Absent)` per pair the disc lacks (the first of `.MDB`/`.CGB` absent), `Folder` the full `images/e03` path. `Files`, `Skipped`, `LeftOut` and `Empty` are derived. `Details()` gives the one-line summary the game logs.
- A pair with only one of its two files counts as missing. The none-present error names the nine files: `the disc has none of the Episode 3 texture files (E03CMN, ..., E03END, each a .MDB and a .CGB)`.
- `Extraction.TextureFile` is replaced by `TextureFiles` (contract order) and `DecodedModes`; `Decode` returns a `DecodedFile` record.
- Mode 5 ignores CMDPMOD's transparency bit, as the prototype does.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-17: high 0, medium 0, low 5, false 0, maybe-false 0. Four patched by the implementer, one rejected, one carried.

- low, patched: the "E03B0N0 unchanged" test compared the new code with itself. A new test extracts a fixed synthetic disc and checks every written path and SHA-256 against constants recorded from the baseline build (364670722c), which gave the same values.
- low, patched: `Extraction.DecodedModes` was never read; the switch hard-codes modes 1 and 5. Removed.
- low, patched: "from N disc files" counted files that wrote nothing (9 where 8 wrote). Now counts files with at least one texture, in the game, the PC command and the README.
- low, patched: the README said a missing file names its `.CGB`; the code names the `.MDB` first. Reworded.
- low, rejected: `Decode` returns mutable collections that a caller could change after the result is built. No caller does, and wrapping every collection adds code for a state nobody reaches.
- carried: the "they look right" criterion was the author's. Met on 2026-10-17: the author looked at the contact sheets and judged them right ("looks exciting").

Re-verified after the patches: `tools/test.sh` (94 Python, 192 GameLogic, 54 Decoding tests) passes; `pdz-extract` on the author's disc writes 250 textures from 8 files, with per-file counts as planned, and 161 mode 4 and 9 mode 0 left out.

## Verification

Run 2026-10-17: `tools/test.sh` all pass (94 Python, 192 GameLogic, 53 Decoding). `pdz-extract` on the author's disc: 250 PNGs in eight folders, exit 0; mode 4 161 and mode 0 9 left out; `E03END` 0 written, no folder. `E03B0N0`'s 28 PNGs byte-identical to the baseline build's. All 250 equal the prototype's decode and its PNGs in `extracted/textures/` pixel for pixel. The desktop game (`--headless`, `PDZ_ASSETS`) wrote the same `images/` tree, byte for byte (`diff -r`). Contact sheets in `extracted/pdz-extract-5/sheet-*.png`.


**Commands:**
- `tools/test.sh` -- expected: all pass.
- `dotnet run --project tools/pdz-extract -- "<cue>" <scratch>` -- expected: about 250 PNGs across eight folders, exit 0.
- A script comparing each written PNG's pixels with the prototype's matching file -- expected: all equal.
