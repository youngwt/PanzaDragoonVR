---
title: 'Development and release extraction modes'
type: 'feature'
ticket: '10'
created: '2026-10-18'
status: 'ready-for-dev'
baseline_revision: ''
route: 'full'
route_source: 'auto'
risk: 'medium'
review: ''
review_source: ''
lenses_ran: []
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
- [ ] `game/Logic/` -- the mode and skip decision from the marker, the manifest's `generator` and `source`, and the disc header; tests per matrix row.
- [ ] `lib/PdzDecoding/` -- list the files a disc image consists of (a cue and its `.bin` files, or an `.iso`); tests.
- [ ] `game/DiscExtraction.cs`, `game/Main.cs` -- apply the decision, delete the image after success or a skip in release mode, log each step.
- [ ] `tools/README.md` -- the two modes, how to turn development mode on and off on the headset, and what release mode deletes.

**Acceptance Criteria:**
- Given `development` in the headset's asset folder and the author's image in `disc/`, when the app starts twice, then it extracts both times and the image is still there.
- Given no marker, an emptied folder apart from the image, when the app starts, then it extracts and the image is deleted; when it starts again, then it does not extract.
- Given no marker and a folder already extracted from the same disc with the image still in `disc/`, when the app starts, then it skips extraction and deletes the image.
- Given `tools/test.sh`, when it runs, then every test passes.

## Implementation Notes

## Plan Change Log

## Review Triage Log

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- On the headset, after the checks: the author's image pushed back into `files/disc/` and the marker state as the author wants it.
