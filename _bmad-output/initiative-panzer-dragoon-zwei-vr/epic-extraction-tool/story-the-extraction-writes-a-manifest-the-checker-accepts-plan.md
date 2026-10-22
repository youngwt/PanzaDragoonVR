---
title: 'The extraction writes a manifest the checker accepts'
type: 'feature'
ticket: '7'
created: '2026-10-18'
status: done
baseline_revision: '73adf96e3f46a45b7813bb185820c43e708717cf'
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
  - '{project-root}/_bmad-output/backlog/story-treat-an-empty-asset-list-as-an-error.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extraction writes 420 PNGs but no `manifest.json`, so the game cannot see them and `write_manifest.py --check` has nothing to check. Separately, the asset contract now says an empty asset list is a problem (author, 2026-10-16), but neither the game nor the PC check reports it.

**Approach:** The library writes `manifest.json` at the top of the output folder after the PNGs, exactly as the asset contract's "What extraction writes" fixes it, listing every texture written. The game and `tools/manifest.py` both report an empty asset list as a problem with the same line, proven by a shared asset-rule case.

## Boundaries & Constraints

**Always:**
- The manifest follows the contract to the byte: `format` 1; `generator` `pdz-extract ` and the library's version; `source` the disc's product and version; entries sorted by `id` in ordinal order, each with `id`, `kind` `image`, `path`, `bytes` and `sha256`, keys in the contract example's order; one member per line, two-space indents, `": "` after each key, `\n` line ends, a final newline, the same output as Python's `json.dumps(indent=2)` for the characters a manifest holds.
- It replaces any manifest already in the folder; files the extraction did not write stay but are no longer listed.
- It is written after every PNG, and only when at least one texture was written.
- The empty-list line is the same on both sides, and comes after the unsupported-format check, so a newer `format` is still reported as such.
- The PC command and the game write byte-identical manifests for the same disc.

**Never:**
- No staging folder, modes or progress board (stories 9 and 10).
- No change to texture decoding, numbering or the asset contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Author's disc | The Japanese dump or the European ISO | `manifest.json` listing 420 textures; `write_manifest.py --check` passes on the folder | No error expected |
| Old manifest | A folder already holding a `manifest.json` | Replaced by the extraction's | Not an error |
| Nothing written | A disc whose files yield no texture | No manifest written | As today |
| Empty asset list, game | `manifest.json` with `"assets": []` | Launch report: the manifest cannot be read, it lists no assets; placeholders | Shown and logged |
| Empty asset list, PC | The same folder through `write_manifest.py --check` | The same problem line; exit 1 | One line |
| Unsupported format, empty list | `format` 2 with `"assets": []` | Reported as the unsupported format, not the empty list | As today |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Extraction.cs` -- `Run(imagePath, outputFolder)` decodes everything, then creates folders and writes PNGs (`File.WriteAllBytes`, around line 216). `Version = "0.1"`. `ExtractionResult.Source` holds the Saturn header (`Product`, `Version`). Write the manifest after the PNGs.
- `tools/manifest.py` -- `dumps(manifest)` (the PC's writer, `json.dumps(indent=2)` plus newline), `check_report` (no empty-list problem today; `build` refuses an empty folder). `MAX_JSON_DEPTH`, wording rules mirror the game.
- `game/Logic/AssetManifest.cs` -- `Parse`: top-level field checks, then the unsupported-format check, then `ReadEntries(root.GetProperty("assets"), folder)` (line 150). Add the empty-list problem between those two.
- `game/Logic/LaunchReport.cs` -- turns an unreadable manifest into the "cannot be read" board.
- `tests/fixtures/asset-rules/make_cases.py` -- the shared cases both suites run; add one for the empty list.
- Tests using `"assets": []` as a readable manifest that must change: `tests/GameLogic.Tests/GlbCheckTests.cs:414`. Those at `AssetManifestTests.cs:280, 299` and `LaunchReportTests.cs:190` use an unsupported format, so they stay.
- `tools/pdz-extract/Program.cs`, `game/DiscExtraction.cs` -- report the manifest written.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/` -- write `manifest.json` after the PNGs, as the contract fixes it.
- [x] `game/Logic/AssetManifest.cs`, `tools/manifest.py` -- an empty asset list is a problem, same line on both sides.
- [x] `tests/fixtures/asset-rules/make_cases.py` and its case folders -- the empty-list case; regenerate.
- [x] `tests/Decoding.Tests/`, `tests/GameLogic.Tests/`, `tests/tools/` -- one or more tests per matrix row; the manifest's bytes against a recorded expected file for a synthetic disc; update the tests that treated an empty list as readable.
- [x] `tools/pdz-extract/Program.cs`, `game/DiscExtraction.cs`, `tools/README.md` -- report and document the manifest.

**Acceptance Criteria:**
- Given the PC command's output folder for the author's disc, when `tools/write_manifest.py --check` runs on it, then it passes with 420 assets checked.
- Given the same disc, when the game runs extraction on the desktop with `PDZ_ASSETS`, then its `manifest.json` is byte-identical to the PC command's and the game reads it with no problem.
- Given the shared asset-rule cases, when `tools/test.sh` runs, then the empty-list case gives the same line in both suites, and every test passes.

## Implementation Notes

- The library's writer is `lib/PdzDecoding/Manifest.cs` (`Manifest.Write`), a hand-rolled emitter of `json.dumps(indent=2, ensure_ascii=False)` text, so no .NET serializer differences between runtimes can reach the bytes. `ExtractionResult.ManifestPath` is the file written, or null when no texture was.
- The empty-list line is `manifest.json: lists no assets` (`AssetManifest.NoAssets`, `manifest.NO_ASSETS`). In the game it is `ManifestStatus.Unreadable`, so the board is "The asset manifest cannot be read".
- The shared case `empty-asset-list` is an `unreadable_manifest` case with a new `"whole_line": true` flag, so both suites compare the whole line, not only its start.
- The recorded manifest is `tests/Decoding.Tests/expected/fixed-disc-manifest.json`, written by `tools/manifest.py`'s `dumps`; `tests/tools/test_manifest.py` checks it stays that writer's text.
- Besides `GlbCheckTests.cs:414`, `AssetManifestTests.EmptyAssetList_IsReadAndListsNothing` and `test_manifest.py`'s `test_empty_assets_is_allowed` treated an empty list as readable; both were changed.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-18: high 0, medium 0, low 2, false 0, maybe-false 0. One patched, one rejected. The reviewer re-ran the acceptance checks on the real output: `--check` passes with 420 assets for both discs, and the game's manifest is byte-identical to the PC command's.

- low, patched: `Decoding.Tests.csproj` added the expected file with `None Include`, duplicating the SDK's default item; now `None Update`. Tests re-run and pass.
- low, rejected: `Run_Manifest_IsWrittenAfterEveryPng` compares file times, which cannot fail on a coarse-timestamp file system. The order is plain in `Extraction.Run` (manifest after the PNG loop), and story 9's staging moves files into place with the manifest last, which is where the order gets a real test.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `pdz-extract` on `rom/JP/...cue`, then `python3 tools/write_manifest.py --check <output>` -- expected: exit 0, 420 assets checked.
- `python3 tests/fixtures/asset-rules/make_cases.py` twice -- expected: no change the second time.
