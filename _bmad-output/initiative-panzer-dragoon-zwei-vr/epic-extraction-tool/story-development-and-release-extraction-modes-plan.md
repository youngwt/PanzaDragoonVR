---
title: 'Development and release extraction modes'
type: 'feature'
ticket: '10'
created: '2026-10-18'
status: done
baseline_revision: 'ac22a3b9d47b637cc4bb47fe3c217b8eccc2be87'
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

**Problem:** The game extracts on every launch and never removes the disc image. The asset contract's "Development and release modes" says otherwise: development mode (an empty `development` file at the top of the asset folder) keeps the image and extracts on every launch; release mode, without that file, extracts when `disc/` holds an image unless the folder's manifest was already written by the extractor from a disc with the same product and version, and deletes the disc image after a successful extraction or such a skip.

**Approach:** Decide the mode and whether to extract in engine-free game logic, from the marker file, the current manifest's `generator` and `source`, and the disc image's Saturn header. Delete the disc image's files in release mode after success or a skip. The PC command has no modes and is unchanged.

## Boundaries & Constraints

**Always:**
- The rules are the contract's: development when `development` is present, whatever the build; release otherwise. The skip test is `generator` starting `pdz-extract ` (with the space) and `source` product and version equal to the disc image's header.
- Deleting the disc image removes the image's own files only: the `.cue` and every `.bin` it names, or the `.iso`. `disc/` itself and anything else in it stay.
- Deletion happens only after a successful extraction or a skip, never after a failure; a failed deletion is logged and the game carries on.
- Each decision is one `PDZ-ASSETS` log line, saying the mode and what was done.
- The decision logic is engine-free and unit-tested; the library reads the header and lists the image's files.

**Never:**
- No change to extraction itself, staging, the progress board or the PC command.
- No change to the asset contract.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Development, disc | `development` present, image in `disc/` | Extracts; image kept | No error expected |
| Development, again | Same, next launch | Extracts again; image kept | No error expected |
| Release, first launch | No marker, image present, no manifest (or one from elsewhere) | Extracts; image deleted after success | No error expected |
| Release, already extracted | No marker, image present, manifest by `pdz-extract ` from the same product and version | No extraction; image deleted | Notice line |
| Release, different disc | Manifest from another product or version | Extracts; image deleted after success | No error expected |
| Release, failure | Extraction fails | Image kept; previous contents kept, as story 9 | As story 9 |
| No disc | `disc/` empty, either mode | Nothing extracted, nothing deleted | Notice line |
| Deletion fails | A file cannot be deleted | Logged; the game carries on | One line |
| Hand-written manifest | `generator` `hand-written` or `pdz-extractor 1.0` | Not a skip: extracts in release mode | — |

</frozen-after-approval>

## Code Map

- `game/DiscExtraction.cs` -- `Start(assetFolder, out problem)` finds the image with `DiscFolder.Find` and runs the library on a background thread; `Finish` reports the outcome. The mode decision goes before the start; the deletion after a successful finish.
- `lib/PdzDecoding/Extraction.cs` -- `DiscFolder.Find`, `DiscSearch(State, DiscFolder, ImagePath, Names)`; `Extraction.Version`; `Manifest` writes `generator` as `pdz-extract <Version>`.
- `lib/PdzDecoding/CueSheet.cs` -- `Parse` reads `FILE` lines but returns only track 1's; add a list of every file a cue names, for deletion.
- `lib/PdzDecoding/DiscImage.cs` -- `OpenImage(path)`, `Header()` gives `SaturnHeader.Product` and `.Version`.
- `game/Logic/AssetManifest.cs` -- parses the manifest; `generator` and `source` are checked for type only and not exposed. The decision needs them; read them from the current `manifest.json` in engine-free code.
- `game/Logic/` -- home for the decision (`ExtractionMode` or similar) and its tests in `tests/GameLogic.Tests/`.
- Device checks: host `adb` (`tools/quest-adb.sh`); `adb shell touch .../files/development` and `rm` for the marker; the author's image is in `files/disc/` and can be pushed again from `rom/JP/` with the `adb push` commands in `tools/README.md`. A Guardian prompt can block a launch.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/` -- the mode and skip decision from the marker, the manifest's `generator` and `source`, and the disc header; tests per matrix row.
- [x] `lib/PdzDecoding/` -- list the files a disc image consists of (a cue and its `.bin` files, or an `.iso`); tests.
- [x] `game/DiscExtraction.cs`, `game/Main.cs` -- apply the decision, delete the image after success or a skip in release mode, log each step.
- [x] `tools/README.md` -- the two modes, how to turn development mode on and off on the headset, and what release mode deletes.

**Acceptance Criteria:**
- Given `development` in the headset's asset folder and the author's image in `disc/`, when the app starts twice, then it extracts both times and the image is still there.
- Given no marker, an emptied folder apart from the image, when the app starts, then it extracts and the image is deleted; when it starts again, then it does not extract.
- Given no marker and a folder already extracted from the same disc with the image still in `disc/`, when the app starts, then it skips extraction and deletes the image.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

- `game/Logic/ExtractionMode.cs`: `ExtractionModes.ModeOf` (a file named `development`; a folder of that name does not count), `LoadOrigin`/`ParseOrigin` (only `generator` and `source`, lenient: anything else is the launch check's job), `ExtractedFrom` and `Decide`, plus the log lines. Tests in `tests/GameLogic.Tests/ExtractionModesTests.cs`.
- Library: `CueSheet.ReadFiles`/`Files` (every `FILE` line, in full, once each), `DiscFolder.ImageFiles` (the cue and the named files that exist inside the cue's folder at any depth, such as `tracks/x.bin`, or the `.iso`; a file named outside `disc/`, such as `../manifest.json`, is never listed, so never deleted), and `DiscImage.ReadHeader`, which reads only sector 0 of the data track and does not walk the file system. Tests in `tests/Decoding.Tests/ExtractionTests.cs`.
- Deletion and when to delete are engine-free, in `ExtractionModes.DeleteImage` and `DeleteAfterExtraction` (tested: release failure keeps the image; a file that cannot be deleted gives one line). Deletion takes the `.cue` or `.iso` first and stops if it cannot go, so the image stays whole; a `.bin` that cannot go is skipped and the rest still go.
- `game/DiscExtraction.cs`: the header is read on the main thread in release mode only (sector 0 alone); an unreadable header is never a skip, and extraction then reports the error as before. It lists the image's files with the library and logs `DeleteImage`'s outcome. `game/Main.cs` needed no change: `Start` and `Finish` carry the decision and the deletion.
- In release mode, any successful extraction deletes the image, including one that found nothing to write, as the intent and the contract say.
- The PC command is unchanged.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-18: high 0, medium 2, low 3, false 0, maybe-false 0. All five patched by the implementer.

- medium, patched: a cue's `.bin` files in a subfolder of `disc/` were not deleted, orphaning them. `ImageFiles` now keeps any named file inside `disc/`, still never outside; test added.
- medium, patched: a successful release-mode extraction that wrote nothing kept the image, against the frozen intent. It is now deleted after any successful extraction, as the intent and the contract say.
- low, patched: the release-mode header read walked the whole file system on the main thread. `ReadHeader` now reads only sector 0 of the data track; tests added.
- low, patched: a README sentence lost a word.
- low, patched: the "Release, failure" and "Deletion fails" rows had no tests. The when-to-delete rule and the deletion itself moved into engine-free `game/Logic/ExtractionMode.cs`, with tests for both rows.

Re-verified after the patches: `tools/test.sh` (97 Python, 245 GameLogic, 118 Decoding) passes. On the headset, 2026-10-18, with the patched build and the marker removed, on a folder already extracted from GS-9049 V1.001: the log read "release mode: ... is already extracted from GS-9049 V1.001: not extracting again; deleting the disc image", then "deleted the disc image ..." listing the `.cue` and four `.bin` files; `disc/` stayed, empty. The author's image was pushed back and the `development` marker set again.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- On the headset, after the checks: the author's image pushed back into `files/disc/` and the marker state as the author wants it.

**Results (2026-10-18):**
- `tools/test.sh`: Python 97 OK, GameLogic.Tests 239 passed (30 new), Decoding.Tests 114 passed (7 new), before the review fixes; after them, 36 ExtractionModes tests and 10 new decoding tests pass (full run on the coordinator's side; the headset checks predate the fixes). `dotnet build game/PanzaDragoonVR.csproj` clean; APK holds `PdzDecoding.dll` and no disc data.
- Headset, Japanese cue/bin image: development marker, two launches: extracted 420 textures both times, image kept. Marker removed with the folder already extracted from GS-9049 V1.001: `already extracted ... not extracting again`, all five image files deleted, `disc/` kept. Folder emptied apart from the re-pushed image: extracted, then image deleted; next launch `release mode: no disc image ... nothing to extract`.
- Left on the headset: the image pushed back into `files/disc/` and the `development` marker on, so the next launch keeps it (remove the marker for release mode).
