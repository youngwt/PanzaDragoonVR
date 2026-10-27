---
title: 'The disc image and release-mode rules follow the retrospective decisions'
type: 'feature'
ticket: '14'
created: '2026-10-27'
status: 'in-review'
baseline_revision: '7f292972c82fc5b01ed13a42f3db9dc56095fb5f'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Three rules the author decided at the extraction epic's retrospective (2026-10-21) are not built, and the forest epic made one of them urgent: the release-mode skip ignores the extractor's version, so a folder extracted by 0.1 never gains the floor and canopy that 0.2 writes. Also, a `.cue` that names an `.iso` beside it counts as two disc images and nothing extracts, and release mode deletes the disc image after an extraction that wrote nothing.

**Approach:** Change the three rules in the code and the asset contract, as decided:
1. A `.cue` sheet that names an `.iso` in `disc/` counts as one disc image with it (the cue is the image; the ISO is its file).
2. In release mode, an extraction that succeeds but writes nothing (no texture and no plane) keeps the disc image, with a log line saying why.
3. The release-mode skip requires the manifest's whole `generator` to equal the current extractor's (`pdz-extract` and the library's version), as well as the same product and version of disc. A newer extractor therefore extracts again, if the player has copied the image back; release mode still deletes the image after a successful extraction (decided 2026-10-21).

## Boundaries & Constraints

**Always:**
- The rules' decisions stay engine-free in `game/Logic/` with xUnit tests; `game/Logic` stays free of the library, so the current generator is passed in by the game (from `PdzDecoding.Manifest.Generator`), and a test pins what the game passes to the library's value.
- The disc-folder rule lives in the library (`DiscFolder`), so the PC command and the game agree; a cue that cannot be read leaves the count as it is today.
- Development mode is unchanged. Log lines keep the `PDZ-ASSETS` tag, one line, no full stop.
- The asset contract's "The disc image" and "Development and release modes" sections state the three rules (the author agrees them by approving this plan).

**Never:**
- No keeping of the disc image after a successful release-mode extraction; no automatic re-extraction without the image.
- No change to what extraction writes, the manifest's format, or `Extraction.Version`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Cue names the ISO | `disc/` holds `a.cue` naming `a.iso` | one image, the cue | none |
| Cue and an unrelated ISO | `a.cue` naming `a.bin`, plus `b.iso` | several images, as today | "more than one disc image" |
| Unreadable cue beside an ISO | broken `a.cue`, `b.iso` | several images, as today | reported as today |
| Older extractor | release; manifest `pdz-extract 0.1`, same disc | extract; delete image after success | none |
| Same extractor | release; manifest `pdz-extract 0.2`, same disc | skip; delete image | none |
| Other generator | release; hand-written manifest | extract, as today | none |
| Nothing written | release; extraction succeeds with no PNG | image kept; log line says nothing was written | none |
| Failed | release; extraction throws | image kept, as today | as today |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/DiscFolder.cs:37-57` -- `Find`: counts every `.cue`/`.iso` in `disc/`; drop an `.iso` that a `.cue` there names (`CueSheet.ReadFiles`), case as the file system finds it. `ImageFiles` (`:66`) already lists the ISO among a cue's files, so release mode deletes both.
- `lib/PdzDecoding/CueSheet.cs` -- `ReadFiles`; MODE1/2048 tracks are already read (`CookedMode`).
- `game/Logic/ExtractionMode.cs` -- `ExtractorPrefix` (`:56`), `ExtractedFrom` (`:114-119`), `Decide` (`:127-131`), `DeleteAfterExtraction` (`:137-138`), `DecisionLine`, line builders (`:200-210`).
- `game/DiscExtraction.cs` -- `Start` (`:77-100`) calls `Decide`; `Finish` (`:144-153`) calls `DeleteAfterExtraction`; `Succeeded` (`:187`) has the result (`Files.Count`, `PlaneFiles.Count`).
- `tests/GameLogic.Tests/ExtractionModesTests.cs` -- the decision matrix and `ExtractorPrefix_StartsTheLibrarysGenerator` (`:277`), which becomes a pin of the generator the game passes.
- `tests/Decoding.Tests/` -- `DiscFolder` tests (find them with grep) for the cue-and-ISO rows.
- `asset-contract.md` "The disc image" (~136) and "Development and release modes" (~142); `tools/README.md` "Development and release modes" on the headset.
- Headset: `tools/quest-adb.sh` (`REMOTE`), `tools/build-quest.sh`, `tools/compare-headset.sh`. The European ISO is `rom/PAL/Panzer Dragoon II Zwei (Europe).iso`; a cue naming it is the author's own text (`FILE "<name>.iso" BINARY`, `TRACK 01 MODE1/2048`, `INDEX 01 00:00:00`), written under `extracted/`.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/DiscFolder.cs` + tests -- the cue-names-ISO rule.
- [x] `game/Logic/ExtractionMode.cs` + tests -- the whole-generator skip (generator passed in) and keeping the image when nothing was written, with its log line.
- [x] `game/DiscExtraction.cs` -- pass `Manifest.Generator`; pass whether anything was written.
- [x] `asset-contract.md`, `tools/README.md` -- the three rules.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then all pass, every matrix row among them; the game builds with 0 warnings.
- Given the headset in release mode with a `.cue` naming the European ISO in `disc/` and a manifest whose generator is `pdz-extract 0.1`, when the app launches, then it extracts both planes and the textures, deletes the cue and the ISO, and `compare-headset.sh` against the European ISO reports identical; launched again with the same image copied back, it skips and deletes the image.
- Given the headset back in development mode with the JP cue afterwards, when the app launches, then it extracts as before.

## Implementation Notes

- `DiscFolder.Find` reads every `.cue` in `disc/` (`CueSheet.ReadFiles`) and drops an `.iso` whose full path one of them names, matched ignoring case (`OrdinalIgnoreCase`). This replaces the Code Map's "case as the file system finds it": the Quest's `/sdcard` ignores case, so a cue naming `game.iso` beside `Game.iso` reads it there and must count as one image. A sheet that throws while being read names nothing. `ImageFiles` was unchanged: it already lists the ISO after the cue.
- `ExtractionModes.ExtractorPrefix` and its test are gone. `ExtractedFrom` and `Decide` take the current `generator`, compared whole and ordinal. `DeleteAfterExtraction` takes `wroteAnything`. The new `NothingWrittenLine` gives `release mode: the extraction wrote nothing, so the disc image <image> is kept`, and only in that case.
- `game/DiscExtraction.cs` passes `generator: Manifest.Generator`. `wroteAnything` is `Files.Count > 0 || PlaneFiles.Count > 0`. The pin `Generator_TheGamePassesIsTheLibrarys` reads `game/DiscExtraction.cs` and checks that call's text, because `GameLogic.Tests` cannot compile the Godot file. `Decide_ReleaseManifestTheLibraryWrote_SkipsOnTheSameDisc` round-trips `Manifest.Write` through `ParseOrigin` and `Decide`.
- Headset run steps (for the author, not yet run):
  1. `tools/build-quest.sh`, then confirm `unzip -l game/build/PanzaDragoonVR.apk` holds no disc data.
  2. Write `extracted/pal-cue/Panzer Dragoon II Zwei (Europe).cue` with `FILE "Panzer Dragoon II Zwei (Europe).iso" BINARY`, `  TRACK 01 MODE1/2048`, `    INDEX 01 00:00:00`.
  3. Release mode: `adb shell rm $REMOTE/development`. Clear `$REMOTE/disc/`. Push the cue and `rom/PAL/Panzer Dragoon II Zwei (Europe).iso` into `$REMOTE/disc/`. Edit `$REMOTE/manifest.json` so `generator` is `pdz-extract 0.1` and `source` is `MK-81022` `V1.000`: pull it, edit it, push it back.
  4. Launch. Expect `release mode: extracting ...`, the floor and canopy and the textures, then `release mode: deleted the disc image ... (<name>.cue, <name>.iso)`. Run `tools/compare-headset.sh "extracted/pal-cue/Panzer Dragoon II Zwei (Europe).cue"`, which needs the ISO beside the cue (symlink or copy under `extracted/`); expect identical.
  5. Push the cue and ISO again and launch. Expect `already extracted from MK-81022 V1.000: not extracting again`, then the deleted line.
  6. Restore: `adb shell touch $REMOTE/development`. Clear `disc/`, push the JP cue and bins from `rom/JP/`, launch, and expect development-mode extraction as before.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-27: high 0, medium 1, low 4, false 0, maybe-false 0. The reviewer re-ran `tools/test.sh` and the game build and checked `Find`'s path compare, `ImageFiles`, the generator pin and that no disc data is in the change.

- medium, patch: the ISO a cue names was matched ordinally, but the Quest's `/sdcard` ignores case, so a cue naming `game.iso` beside `Game.iso` would read there yet count as two images. Now matched ignoring case; the test asserts one image on every file system.
- low, patch: the broken-cue test's cue named nothing and its ISO was never named, so the unreadable-cue path was checked only by a test that returns early as root or on Windows. Its cue now names the ISO and then throws when read.
- low, patch: `DiscExtraction.Succeeded` logged "nothing written" when only the planes were written, while the image was then deleted. Now "nothing written" only when no texture and no plane was written.
- low, patch: `DecisionLine` and the README said the image is deleted after any successful extraction. Now "that writes something".
- low, patch: the spec's Constraints line said extraction never runs again for the same disc; now "the same disc and the same extractor version".
- Acceptance criteria 2 and 3 (the headset runs) are checked with the author before the plan is presented.

## Design Notes

- The headset run is the author's: release mode needs the `development` marker removed, and the image (about 390 MB for the ISO) pushed twice. The steps go in the plan's notes before the run, and the folder is restored to development mode with the JP disc afterwards.
- `ExtractorPrefix` may become unused once the skip compares the whole generator; remove it and its test if so, keeping a test that pins the generator the game passes to `Manifest.Generator`.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `dotnet build game/PanzaDragoonVR.csproj --no-incremental` -- expected: 0 warnings.
- `tools/build-quest.sh`, the release-mode run above, `tools/compare-headset.sh "<the cue>"` -- expected: identical.
