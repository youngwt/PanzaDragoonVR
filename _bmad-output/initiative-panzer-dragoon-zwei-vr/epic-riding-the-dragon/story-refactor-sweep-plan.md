---
title: 'Refactor sweep'
type: 'refactor'
ticket: '8'
created: '2026-11-01'
status: 'built'
baseline_revision: 'f7a2f4555ea7bf98554e27b27a41c92a529c0461'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The epic grew model decoding, the ride, the seat and the tuning list a story at a time. A survey on 2026-11-01 found constants and helpers repeated across files, names that no longer say what they do, stale documents, and values from the disc in tracked text against `AGENTS.md`.

**Approach:** Cleanup only: move, merge, rename private names and delete, without changing what extraction writes or what the game shows or logs; bring the documents up to date and take the disc's values out of them; then check the epic's Done when still holds on the headset.

## Boundaries & Constraints

**Always:**
- Behaviour stays: the same files, manifest, log lines and scene. `pdz-extract` output is byte-identical to a run at the baseline on the JP cue and the PAL ISO, written under `extracted/`.
- Every existing test passes with its assertions unchanged; moved tests keep their names.
- No value measured from the disc is added anywhere, including in this plan or commit messages.

**Never:**
- No rewriting of git history; values already in history stay there.
- No renaming of public library members that callers or tests rely on, and no change to how textures are found or numbered.
- No change to any built-in value, range, asset ID or rule, or `Extraction.Version`.
- No change to the deliberately kept `spec-panzer-dragoon-zwei-vr.md` wording on pushing the file, nor to `deferred-work.md`'s old entries.

</frozen-after-approval>

## Code Map

Paths from the survey; re-find lines when editing. `C` is `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/`.

- `tools/README.md` -- :535-546, 556, 559, 566-571 sample output; :510, 524, 629, 642 prose and log samples: disc values. The PAL sample lacks the dragon lines. :382 stale sample log. :320 "Each is tiled" also follows models. :579, 642, 649, 654 omit models.
- `lib/PdzDecoding/Dragons.cs:26`, `Extraction.cs:82-83`, `C/zwei-disc-formats.md:31-32, 172` -- disc values in comments and notes. Layouts and offsets that say where to look stay; counts go.
- `AGENTS.md:39` -- the library's list omits geometry and `.glb`. `C/asset-contract.md:381` -- names done story 1.4 as future. Epic file, Notes -- story 5 defined no config file, because the seat goes into the model's origin.
- `game/AssetIds.cs:22` -- literal beside `Planes.FloorId`. `Extraction.cs` `ModelFolder`, `Dragons.cs:63, 93` -- "models" twice. `tools/pdz-extract/Program.cs:25` -- hard-coded form paths.
- `game/DiscExtraction.cs:152, 197`, `Program.cs:56, 73` -- one count of three lists, four times.
- `game/Main.Tuning.cs:41-42`, `Tuning.cs:212` -- one problem line twice. `Main.cs:90-121` -- two near-identical carry blocks. `Main.cs`, `Main.TuningMenu.cs`, `Main.Tuning.cs`, `Main.Board.cs` -- node paths repeated. `RiddenDragon.cs:33, 53` -- facing built twice.
- `Main.Tuning.cs:57` `PlacePlanes`, `Main.Board.cs` `PlaceBoard`, `_boardPlaced` -- also seat the dragon. `TuningMenu.cs:25, 28, 123` -- unused public constants, one odd name. `Tuning.cs:133-142` -- two identical returns. `Dragons.cs:90` `FormId` -- private if nothing outside uses it. `Dragons.cs:140, 148, 180, 188` -- `Split` twice, a bare 12.
- `tests/GameLogic.Tests/TuningTests.cs:325`, `AssetRulesTests.cs:115`, `ExtractionModesTests.cs:330` -- three repo walkers. `tests/Decoding.Tests/GlbWriterTests.cs:591` -- one test exports, records and asserts. `DragonsTests.cs:22, 30` -- `NoSeat` beside `default`.

## Tasks & Acceptance

**Execution:**
- [x] `tools/README.md`, `lib/PdzDecoding/Dragons.cs`, `Extraction.cs`, `C/zwei-disc-formats.md` -- replace disc values with placeholders such as `<n>` and `0x...`; correct the samples and the model wording -- `AGENTS.md`'s hard rule; closes the deferred item from story 7, with a closing entry appended to `deferred-work.md`.
- [x] `AGENTS.md`, `C/asset-contract.md`, epic file -- correct as mapped.
- [x] `lib/PdzDecoding/`, `tools/pdz-extract/Program.cs` -- one constant for each repeated path and ID; add `WroteAnything` and `AssetCount` to the result and use them; `SeatSize` and a small root-name helper; `FormId` private if unused elsewhere.
- [x] `game/` -- use the library's ride ID; one carry helper that gives the same warnings; private node properties; one facing; one problem line; rename `PlacePlanes` and `PlaceBoard` so they say the seat is placed too; tidy `TuningMenu` and `Tuning.cs` as mapped.
- [x] `tests/` -- one repo walker per test project; split the export and record helpers; one no-seat value. Assertions unchanged.

**Acceptance Criteria:**
- Given the baseline and the swept code, when `pdz-extract` runs on the JP cue and the PAL ISO, then the output folders are identical.
- Given the tracked documents and comments, when searched for the survey's locations, then no count or sample value from the disc remains there.
- Given the headset, when the author looks down and around, then the epic's Done when items 3 and 4 still hold: wings either side, head in front, tail behind, steady over the scrolling forest.

## Implementation Notes

- The headset criterion, `tools/build-quest.sh` with an install and `tools/compare-headset.sh` are not done: no headset was connected. They stay open at `built` until the author has looked.
- Beyond the survey's lines, also taken from the two listed documents: the colour-mode table's count column in `zwei-disc-formats.md`, and in `tools/README.md` the image sizes, the sample line for a left-out mode and the damaged image's sector.
- Left as the plan's boundaries say or outside its locations, for the author to decide: sample values in the comments of `ColourBanks.cs` and `Planes.cs`; the same literals in `DragonsTests.cs` and `PlanesTests.cs`, whose assertions stay unchanged; the offsets and tables in `zwei-disc-formats.md` beyond the mapped lines; older planning documents.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 1 low patched; 2 rejected; 1 deferred (found by the implementer).

- low, patch: two measured values remained in the colour-mode table in `zwei-disc-formats.md`. Removed.
- medium, rejected: the headset checks are not done. True, but no code change settles them; they stay open for the author.
- low, rejected: the plan's note and status disagreed. A fix to this plan only; the note is reworded.
- defer: other disc values remain outside the sweep's locations, in comments, test literals and notes.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `pdz-extract` at the baseline and after, on both discs, under `extracted/`, then `diff -r` -- expected: identical
- `tools/validate-gltf.sh` on `dragon_ride.glb` -- expected: 0 errors
- `tools/build-quest.sh`, then `tools/compare-headset.sh` -- expected: identical
- `unzip -l game/build/PanzaDragoonVR.apk` -- expected: `PdzDecoding.dll` and no disc data
- `git diff --cached --name-only` before commit -- expected: no game data

**Manual checks (if no CLI):**
- In the headset, the third acceptance criterion, and the tuning list still works.
