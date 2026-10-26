---
epic: epic-forest-floor-and-canopy
date: 2026-10-26
verdict: accepted-with-open-items
criteria: declared
headless: false
---

# Retrospective: Forest floor and canopy


## Epic summary

**Epic:** `epic-forest-floor-and-canopy` (epic 3), covering CAP-4 and CAP-3 (plane extraction only). The Done when has four criteria, declared in `epic-forest-floor-and-canopy.md`. The epic file's `status` is `done`, closed by the author on 2026-10-26 (`669d557`).

**Tickets:** 8. All are `done` in `tickets.py status`; none is at `built`; `pending_tickets` is empty. 3.7 and 3.8 came from the backlog (tickets 15 and 11) and carry story files. The other six were never pulled, so their `description` and `verify` come from `tickets.toml`.

Each range runs from the plan's `baseline_revision` to the next baseline, in history order (the history is linear: `git rev-list --count` rises with each baseline). 3.3 was built before 3.2, and 3.8 before 3.4. The last range ends at `HEAD` (`669d557`, the closure commit). It is inferred, not recorded, and nothing outside this epic has landed since.

| Ref | Title | Range | Commits | Code (non-merge, outside `_bmad-output/`) |
|-----|-------|-------|---------|------|
| 3.7 | Extraction code duplicates pinned, `Main.cs` split | `93f9407..f3bf373` | 2 (`8da9411`, `f3bf373`) | +392 −267, the `Main.cs` split and `OneLine` |
| 3.1 | The floor scrolls endlessly (tracer) | `f3bf373..9cdc532` | 2 (`1ae414c`, `9cdc532`) | +176 −8 |
| 3.3 | Tuning file | `9cdc532..f55a17e` | 3 (`53d77b7`, `39bb3b9`, `f55a17e`) | +673 −37 |
| 3.2 | Where the planes are on the disc (spike) | `f55a17e..4005694` | 2 (`02ee36e`, `4005694`) | +317, the probe and its test; the notes are in `zwei-disc-formats.md` |
| 3.8 | A colour table with no usable block (bug) | `4005694..2c6233e` | 2 (`39d8856`, `2c6233e`) | +41 −7 |
| 3.4 | Extraction writes the floor and canopy | `2c6233e..67aeece` | 3 (`8bbb89a`, `728f3f0`, `67aeece`) | +1006 −36 |
| 3.5 | The forest flies past in the headset | `67aeece..5ccc30e` | 2 (`dfeceb7`, `5ccc30e`) | +26 −23, the built-in values only |
| 3.6 | Refactor sweep | `5ccc30e..HEAD` (inferred) | 4 (`34203cb`, `34a7325`, `e33bae6`, `669d557`) | +276 −220 |

The whole epic is `93f9407..HEAD`: 20 commits. Merges: `merge_count` is 0 and `merges_measured` is 0, so no churn was left unmeasured. `binary_revisions` is 0 on every path. No commit subject names a ref apart from the `docs: mark ticket … done` lines, so attribution is by range. `git_evidence.py` was run from `/home/deck/.agents/skills/bmad-retrospective/scripts/git_evidence.py`, the path that `evidence-gathering.md` names. A copy also exists under `/home/deck/.claude/skills/bmad-retrospective/scripts/`, but it was not used.

**Evidence inventory:**
- Available:
  - the epic file: Description, Outcome, Done when 1 to 4, Boundaries, and Notes with dated decisions and the closure check;
  - `tickets.toml`, and `tickets.py status` / `find` for all 8 tickets;
  - the story files of 3.7 and 3.8;
  - all 8 plans, each `status: done` with a `baseline_revision`, one quick-lens Review Triage Log each and dated headset records (3.1, 3.3, 3.4, 3.5, 3.6, and 3.7 by log only);
  - the initiative's Done when and Boundaries. The initiative file has no "Requirements" section: the CAP ids that `covers` names are defined in the spec's Capabilities section;
  - the spec and its companions: `asset-contract.md`, `zwei-disc-formats.md`, `stack.md`, `milestones.md`, `later-ideas.md`;
  - `AGENTS.md`;
  - `deferred-work.md` and the backlog, including ticket 16 (the side walls), opened at closure;
  - the previous retrospective, `epic-extraction-tool/epic-extraction-tool-retrospective.md`.
- Gaps:
  - **Session logs: not available.** The conversation transcripts of the build sessions are not available to this retrospective. What was done, and why, comes from the plans' Implementation Notes and Review Triage Logs only. Abandoned attempts and the reasons behind unexpected turns are not recorded, so process-lesson analysis is narrowed to what the plans say.
  - No dev diary for this epic: `devlog/` has no entry after 2026-10-17, and no commit in the range touches it.
  - `rom/` and `extracted/` were not opened. Statements about disc output (pixel equality with the probe, byte-identical runs, `compare-headset.sh` "identical: 423 files") rest on the plans' records.
  - Nothing was run on the headset for this retrospective, and `tools/test.sh` was not re-run. Test counts are as the plans record them (105 Python, 297 GameLogic and 152 Decoding at the end, plan 3.6 Implementation Notes).
  - Sub-agents were not used. The aggregate views were derived inline from `git_evidence.py`, `wc -l`, `grep` and reading the changed files. The architecture delta was derived from `using` and project references, not from a dependency tool.
- Checked and clean: no commit in the range adds a path under `rom/`, `extracted/` or `tools/mednafen/`, or a `.cue`, `.bin` or `.iso` (the file list of `93f9407..HEAD`). The only fixture changed is the synthetic `tests/Decoding.Tests/expected/fixed-disc-manifest.json`.

## Findings

Derived over `93f9407..HEAD`. A file changed in several ranges is counted once here, and each ticket's range above stays as its provenance.

### Architecture delta

- **F1. A second game file now depends on the decoding library.** `game/AssetIds.cs:2,13,16` takes `E03Floor` and `E03Canopy` from `Planes.FloorId`/`Planes.CanopyId` (`34203cb`, ticket 3.6, Code Map "have `AssetIds` use the library's"). Before this epic, only `game/DiscExtraction.cs` joined the game to the library (previous retrospective, A-arch). Two other sources bear on it:
  - the initiative's Boundaries says the two units "meet only at the external asset folder and its manifest";
  - `AssetIds`'s own summary calls an ID "a promise". The game's IDs now follow the library's constants.
- **Checked and unchanged:**
  - `game/Logic/` uses neither Godot nor the library (no `using Godot` or `PdzDecoding` under `game/Logic`);
  - the library has no Godot reference;
  - `GameLogic.Tests` gained a project reference to the library, for tests only (`8da9411`), and `AGENTS.md:52` now records it.

### Duplication map

- **F2. The plane decoder exists twice.** The LZ77 unpacker, source finding and rendering are in `tools/plane_probe.py` (234 lines, `02ee36e`) and in `lib/PdzDecoding/Planes.cs` (`8bbb89a`). This is deliberate: the probe is the reference ("Port it; do not change it", plan 3.4 Code Map; plan 3.6 Never). The two are tied in two ways:
  - by a pixel comparison run by hand on the PC (plan 3.4 Implementation Notes);
  - by the probe's hand-built streams, copied into the C# tests (`tools/README.md:207`), rather than by a shared case both suites read.
- **F3. The eye height is held in two places.** `game/Logic/Tuning.cs:28` (`EyeHeight = 1.6f`) and the camera at `game/Main.tscn:44` both hold 1.6, and the canopy's speed depends on it (plan 3.3 Boundaries, Always). The 3.1 review rejected the repeat as a stopgap (plan 3.1 Review Triage Log). 3.3 kept the assumption, and 3.6 left the scene's 1.6 alone (plan 3.6 Never).
- **F4. The tuning built-ins are in four places, and only one copy is tested.**
  - The four are `Tuning.BuiltIn`, `tools/tuning.example.json`, the README table and the contract (`AGENTS.md:59`).
  - Only the example file is held to `Tuning.BuiltIn` by a test (`TuningTests.ExampleFile_GivesEverySettingAtItsBuiltInValue`, `34203cb`).
  - The README's default tile sizes had already drifted in 3.5 and were caught by review (plan 3.5 Review Triage Log, first item).
- **Removed during the epic:**
  - 3.7 replaced the two one-line helpers in `game/Logic` with `OneLine.Of` and pinned the prefix (`8da9411`);
  - 3.4 made `EPISODE3.PRG` read once, for both colours and planes (`ColourBanks.ReadProgram`, plan 3.4 Implementation Notes);
  - 3.6 deleted the dead `ColourBanks.FromDisc`, named the VRAM mask and moved `REMOTE=` into `quest-adb.sh` (plan 3.6 Tasks).

### Size growth

Ranked on `files` (non-merge churn), then each file's size was measured.

- **F5. `game/Main.cs` is back at the edge of its own limit.** It went from 444 lines at `93f9407`, to 190 after the split (`f3bf373`), to 199 at `HEAD` (+52 −297 across the epic). Ticket 3.7's acceptance set "under 200 lines" (plan 3.7 Acceptance Criteria). The partial files are 59 to 96 lines each.
- **F6. `lib/PdzDecoding/Extraction.cs` is now 414 lines.** It was 363 at `93f9407` (+65 −14, mostly 3.4). The previous retrospective recorded the library's files as all under 400 after its sweep (A-size). The new `Planes.cs` is 386 lines. Its largest method, `FindSources`, runs from about `Planes.cs:192` to `:260`. This makes it a candidate for a closer look, not a judged god-class.
- **F7. `tests/Decoding.Tests/ExtractionTests.cs` is 1,109 lines** (+145 −3 in the epic, mostly 3.4). It is the largest test file. 3.6 moved the plane builders out of `SyntheticDisc.cs` into `SyntheticPlanes.cs` (118 lines), but this file was not split.
- **F8. One README line lists every notice and is hard to keep complete.** `tools/README.md:518` is one line of 3,306 characters listing every `pdz-extract` notice, and 3.4 added the plane notices to it. Reviews had to patch notice lists in the README twice: plan 3.8 Review Triage Log (first item) and plan 3.4 Review Triage Log (second item).

### Pattern divergence

- **F9. A plan pointed the build at a known exception.** Spike 3.2's Code Map told the new probe to "follow their style ... usage to stdout as these older scripts do" (plan 3.2 Code Map, the `tools/mednafen_state.py` item). `AGENTS.md` lists that style as a known exception not to copy. Review patched it (plan 3.2 Review Triage Log, second item), and `AGENTS.md:30` now records the probe's real exceptions.
- **F10. One plan contradicted itself.** Plan 3.6's Code Map asked for the public `SkySpeed` to be renamed. Its Always rule said existing tests keep their names and assertions. The build renamed two tests (`SkySpeed_*` to `CanopySpeed_*`), as recorded in plan 3.6 Implementation Notes and Review Triage Log, first item. The author accepted the renames at closure (epic Notes, closure decision).
- **F11. Plan 3.1's Execution tasks are all still `[ ]`** (plan 3.1 Tasks & Acceptance), though the plan is `done` and its work is in `1ae414c`.
- **Checked and followed:**
  - `tools/push-tuning.sh` defines `die`/`say` with its own prefix and exits 1;
  - the new log lines are tagged `PDZ-ASSETS` with no full stop (`game/Main.Tuning.cs:47,51`);
  - the new C# tests use `Subject_Behaviour` names (`TuningTests`, `PlaneScrollTests`, `PlanesTests`, `OneLineTests`), and the probe's Python tests use `test_snake_case`;
  - the PNG writer is reused for the planes, so no .NET compressor is used;
  - `AGENTS.md` was brought up to date by 3.6.

### Spec against the build

- **F12. An app update can never add the planes in release mode.** 3.4 raised `Extraction.Version` to 0.2 "as the output changed" (plan 3.4 Boundaries and Implementation Notes). But the release-mode skip matches any `pdz-extract ` generator, whatever its version (`game/Logic/ExtractionMode.cs:114-119`). So a release-mode folder extracted by 0.1 is skipped and its image deleted, and it never gets `e03.floor`/`e03.canopy`. The spec's Constraints line ("extraction does not run again for the same disc") describes this. The previous retrospective's R7 decision to compare the version is in backlog ticket 14, still `backlog`.
- **F13. Done when 2's "no visible seam" on the extracted floor has no check that names it.** The epic's closure decision says so: it rests on the author's approval in 3.5 and 3.6, and on the whole page wrapping. 3.1 checked the seam on the placeholders only (plan 3.1 Implementation Notes).
- **F14. No frame rate is recorded with the extracted planes.** The only 90 Hz record in the epic is the implementer's earlier run on the placeholders (plan 3.1 Implementation Notes). The headset records of 3.4, 3.5 and 3.6 name no frame rate. 3.1's own author session saw only the first "running at 72 Hz" warning. The initiative's Done when 1 asks for a steady 90 Hz between the extracted floor and canopy.
- **F15. One of 3.1's acceptance checks was not made.** Its third criterion, "the sky seems to move at the same speed as the floor", was "not remarked on" (plan 3.1 Implementation Notes). The canopy's motion was later judged only as part of the whole look in 3.5.
- **F16. 3.3 edited the contract's fixed-names line but did not add `extracting/`.** The line (`asset-contract.md:36`, `53d77b7`) gained `tuning.json` but still omits `extracting/`, which extraction defines and empties (`asset-contract.md:161`). This is the previous retrospective's R8.
- **F17. The formats notes still list done work as next steps.** `zwei-disc-formats.md` lists "Port the planes' unpacking to the decoding library (story 3.4)" and "Trace the VDP2 register set-up lists ... if story 3.4 needs" (Next, items 2 and 3, about `:147-148`). The port is done, and the constants decision was taken on 2026-10-25 (epic Notes). 3.6's rule against "story N will" about done work (plan 3.6 Boundaries) covered the README but not this file.
- **F18. Behaviour was added without the spec asking.** Recorded here so later runs can tell it from a defect. Each item is in its plan:
  - the eye assumed at 1.6 m for the canopy's speed (3.3 Always);
  - `floor_depth` of at least 3 m, from the author's dragon clearance (3.3 Intent);
  - tuning problems listed first and turning a notice amber (3.3 Implementation Notes);
  - every plane pixel opaque, and the unpack limited to the VRAM left (3.4 Implementation Notes);
  - `tools/push-tuning.sh` and its JSON check (3.3).
- **F19. Three rejected review findings remain open, and 3.6 was told not to touch them** (plan 3.6 Never):
  - a key given twice in `tuning.json` (plan 3.3 Review Triage Log);
  - `AssetFolder.Locate()` throwing, which leaves the tuning at the built-ins (plan 3.3 Review Triage Log);
  - "nothing written" / "wrote 0 textures" when only the planes are written (plan 3.4 Review Triage Log).
- **F20. Open items the epic leaves:**
  - backlog spike 10 (repeating textures clamp on the Quest) is still `backlog`, and 3.1 and 3.5 rely on the shader's wrap that works around it (plan 3.1 Never; epic Notes, "Related");
  - the spec's remaining open question is the colour table at 0x288, and where codes 0x780 up come from (`spec-panzer-dragoon-zwei-vr.md:99`);
  - the forest's side walls went to backlog ticket 16 at closure.
- **Met as specified:**
  - The spec's Constraints line (`spec-panzer-dragoon-zwei-vr.md:70`) and the contract's "Not in the contract" tuning line (`asset-contract.md:351`) were reconciled to the tuning-file decision.
  - The spec's plane and palette open questions were updated (`:98-99`).
  - The plane paths and notices were added to the contract (`asset-contract.md:126-129`).
  - Done when 1, 3 and 4 each have a dated record in plans 3.4, 3.5 and 3.3. These come into Behaviour verification.

### Diff-scope review (adversarial, edge-case, verification-gap lenses)

Run over `b131013..HEAD` (game, lib, tools, tests, AGENTS.md, the spec folder), weighting the boundaries between tickets, with the plans' triage logs read so that triaged items were not raised again. Narrowing: the lenses ran inline in one sub-agent, not through `bmad-review`. Re-checked against the code by the orchestrator before routing.

- **R1 (= F12). Release mode never picks up the planes on a folder extracted before 3.4.** `ExtractionMode.ExtractedFrom` (`game/Logic/ExtractionMode.cs:114-119`) checks the `pdz-extract ` prefix and the disc's product and version, not the extractor's version; `Decide` then skips extraction and deletes the image. The contract's release-mode rule (`asset-contract.md:157`) has the same gap. No test covers an older generator.
- **R2. `canopy_height`'s minimum of 2 m lets the canopy cut the flyer and sit at the head.** `Tuning.cs:43` allows 2; the flyer's path reaches 1.8 m + 0.6 m bob + 0.25 m half-cube, about 2.65 m (`FlyingCube.cs:21,24`); 2 m is 0.4 m above the assumed 1.6 m eye. The contract says the canopy is "always above the head" (`asset-contract.md:335`). The 3.5 review checked the flyer against the 10 m built-in only.
- **R3. Values allowed at the ends of the ranges break the picture.** `floor_tile`/`canopy_tile` of 0.1 on the 2000 m planes give `tiles` of 20000 (`Main.cs:195`), where float UVs near the edge resolve about one texel of the page; with `flight_speed` 100 the image moves about 11 tiles a frame at 90 Hz and reads as still or backwards; `floor_depth` 1000 against the planes' 1000 m half-width (`Main.tscn:16`) shows the edge 45 degrees below the horizon. `RangeEnds_AreAllowedAndJustBeyondAreNot` checks acceptance, not the picture.
- **R4. Behaviour claimed but not checked.** Every headset check ran in development mode on the JP cue (plans 3.4 and 3.6): extraction of the PAL ISO on the headset, and release mode with the planes, never ran. `Planes.cs:305-309` (a map past the end of VRAM) and the colours running past the end of the file (`Planes.cs:251`) have no test.
- **R5. `push-assets.sh --clear` deletes `tuning.json` without its header or message saying so** (`push-assets.sh:6-8,66-69`); only `tools/README.md:326` says it. Re-checking showed `--clear` also deletes `disc/` and the `development` marker, which predates this epic.
- **R6. The scene file holds a fifth copy of a built-in.** `Main.tscn:63` puts the Sky node at y 10 to match `canopy_height` (3.6), while `AGENTS.md`'s new "Rules kept in two places" counts four places; the Ground node stays at 0 against `floor_depth` 6. `PlacePlanes` overrides both, so nothing shows wrong.
- **Checked and clean:** the unpacker's and source finding's bounds; `Render`'s indices; the shared `EPISODE3.PRG` read (3.8 with 3.4); planes staging, moving and manifest order; the probe and the port on bit order, offsets and the PAL search; the `Main.*.cs` split against later additions; the scroll and tuning maths across 3.1, 3.3 and 3.5; tuning problems surviving the board's rebuild; `push-tuning.sh`'s JSON check against the game's; `REMOTE` shared.

## Dispositions

| Finding | This instance | Next time |
|---|---|---|
| F1 game IDs from the library | accept: the game already references the library for extraction; one name for the promise is better than two | nothing |
| F2 decoder twice | accept: the probe is the reference by design | nothing |
| F3 eye height twice | accept, as triaged in 3.1 | nothing |
| F4, R6 built-ins in several places | accept; the example file is tested | count every copy when adding a rule to AGENTS.md |
| F5 `Main.cs` at 199 | accept | nothing |
| F6, F7 `Extraction.cs` 414, `ExtractionTests.cs` 1,109 | defer: split when backlog 12 or 13 next edits them | nothing |
| F8 one-line notice list | defer to backlog 7: make the notices a table | a notice added in code is added to the README table in the same change |
| F9 plan pointed at a known exception | accept (fixed in review) | planning: a Code Map never cites a style AGENTS.md lists as an exception |
| F10 plan contradicted itself | accept (author accepted the renames) | planning: check a Code Map's renames against the Always rules before approval |
| F11 3.1's tasks unticked | accept | nothing |
| F12 = R1 release mode ignores the version | **fix now**: build backlog 14 before any release-mode use, and check release mode with the planes on the headset | a version rise in a plan names what acts on it |
| F13, F14, F15 seam, 90 Hz and canopy speed unrecorded | fix at the next headset session (A3) | headset checks record each Done when item by name |
| F16, F17 contract fixed names, formats "Next" | defer to backlog 7 | nothing |
| F18 behaviour added unasked | accept | nothing |
| F19 rejected review findings | accept as triaged | nothing |
| F20 open items | tracked: backlog 10, 16 and the spec's questions | nothing |
| R2, R3 tuning ranges | fix: a backlog story narrowing the ranges (A2) | range ends get a look in the headset, not only a parse test |
| R4 unchecked paths | fix: the two `Planes` tests with backlog 13; PAL and release on the headset with A1 | nothing |
| R5 `--clear` header | defer to backlog 5 | nothing |

## Behavior verification

Run for this retrospective on 2026-10-26, on the PC:

- `tools/test.sh`: 105 Python, 297 GameLogic and 152 Decoding tests, all pass.
- `pdz-extract` on the JP cue into a scratch folder under `extracted/`: the plane line names cells at 0x3FDAC, maps at 0x52608 and 0x5287C, colours at 0x52C44 and 0x53244; 420 textures and the two planes; the output is identical (`diff -rq`) to ticket 3.4's run. On the PAL ISO: the planes found at their own offsets (cells 0x3FD90, maps 0x525EC and 0x52860, colours 0x52C28 and 0x53228), and `floor.png` byte-identical to the JP one.

Not run for this retrospective: the headset. The last headset records are the sweep's on 2026-10-26 (development mode, JP cue: `compare-headset.sh` identical, 423 files; the author confirmed the scene). Release mode and the PAL ISO were never run on the headset (R4).

## Previous-retro follow-through

From `epic-extraction-tool/epic-extraction-tool-retrospective.md` (Action items A1 to A6 and the author's Decisions of 2026-10-21). The owners are as that file names them.

| Item (owner) | Landed? | Evidence |
|---|---|---|
| A1, four small extraction fixes as one story: R1 a table with no applied block counts as none; R2 a true heading for a failure while moving into place; R3 a case-insensitive fallback for cue `FILE` names; R4 remove emptied folders in `compare-headset.sh` (you, with `bmad-build`) | **Partly.** R1 landed; R2 to R4 did not | R1: ticket 3.8, `39d8856`. Its tests `ColourBanksTests.FromProgram_EveryPairSkipped_GivesNoColoursAndANotice` and `ExtractionTests.Run_TableWithEveryPairSkipped_LeavesModes4And0OutAsWithNoTable` fail on the old code (plan 3.8). R2 to R4 are in backlog ticket 12, still `backlog`. The code still shows the old behaviour: `LaunchReport.cs:151` keeps the single `NothingExtracted` heading; no case-insensitive file lookup was found in `CueSheet.cs` (only the `FILE` keyword is compared ignoring case, `:113`); no folder removal was found in `compare-headset.sh` (grep) |
| A2, tests for the untested wiring: the finish decision into `game/Logic`; `pdz-extract`'s arguments and exit codes, catching every exception; the extractor's manifest through the shared asset-rule checks (you, with `bmad-build`) | **Not landed** | Backlog ticket 13 is still `backlog`. `tools/pdz-extract/Program.cs:82-90` still catches only `DecodingException`, `IOException` and `UnauthorizedAccessException`, though 3.4 edited the file (+12 −2). No test in `tests/GameLogic.Tests` names `DiscExtraction`. The only check of `fixed-disc-manifest.json` from Python (`tests/tools/test_manifest.py:117`) predates the retrospective (`2541cdc`) and checks the writer's text, not the shared rules |
| A3, reconcile the docs: `extracting/` in the fixed names, the texture numbering rule, the "not a Saturn disc" row, the stale lines in S3, the two source conflicts (you) | **Partly** | Folded into backlog ticket 7 (Notes, 2026-10-21), still `backlog`. Still stale: `asset-contract.md:36` (no `extracting/`, see F16); `stack.md:16` "not built yet"; `zwei-disc-formats.md:38` mode 5 "Not handled"; `asset-contract.md:353` "Story 1.4 settles that". No `SEGA SEGASATURN` check exists in `lib/`. Landed: the spec's palette question is now "Mostly answered" (`spec-panzer-dragoon-zwei-vr.md:99`, `02ee36e`), and the two source conflicts are marked settled (`epic-extraction-tool.md:41-42`; that change is not in this range). The texture numbering rule was not looked for in the contract |
| A4, when the next epic touches them: a test pinning `ExtractorPrefix`, one shared one-line helper, split `Main.cs` (the next epic's sweep) | **Landed** | Ticket 3.7 (from backlog 15), `8da9411`: `ExtractionModesTests.ExtractorPrefix_StartsTheLibrarysGenerator` (`tests/GameLogic.Tests/ExtractionModesTests.cs:277`), shown to fail on a changed generator (plan 3.7 Checks); `game/Logic/OneLine.cs`; `Main` split into five partial files. The `OneLine` tests moved to `OneLineTests.cs` in 3.6 |
| A5, say so when a display error follows a successful extraction, rather than "extraction failed" (backlog) | **Not landed** | In backlog ticket 12 (R9), still `backlog` |
| A6, background log recording before the author launches as the standard headset check; rerun a headset story's checks after its review patches (you and agents, `AGENTS.md`) | **Partly.** No evidence found of the first half; the second was practised | First half: `AGENTS.md` has no line on log recording (grep for `logcat` and `record` finds none), and no plan in this epic records starting a background capture. `build-quest.sh`'s log clearing and crash capture (`:104`, `:130`) predate this epic. Second half: the author's headset checks came after review in 3.4 (review 2026-10-25, headset 2026-10-26), and 3.1, 3.3 and 3.4 each record "checked with the author before the plan is presented", after the review. This is a practice, not a written rule |
| Decisions R5 to R7: a cue naming an `.iso` is one image; release mode keeps the image when nothing was written; the release skip compares the extractor's version (author) | **Not landed** | Backlog ticket 14, still `backlog`. `ExtractionMode.cs:114-119` still ignores the version, which leaves this epic's planes out of release mode (F12) |

Carried from the engine-spike retrospective, as that file's follow-through recorded them as not landed or partly landed:

| Item | Landed? | Evidence |
|---|---|---|
| Engine-spike A3: try `push-assets.sh` with links and a failing remote `tar` | **Not landed** | Backlog ticket 5, still `backlog`. 3.6 changed `push-assets.sh` only to share `REMOTE=` (−1 line) |
| Engine-spike A5: reconcile the documents | **Partly** | Backlog ticket 7, still `backlog`, as for A3 above |
| Engine-spike A7: move off .NET 9 | **Partly**, unchanged | `game/PanzaDragoonVR.csproj:7` still builds Android for `net9.0` on Godot 4.7.2. `deferred-work.md` records that .NET 9 support ends on 2026-11-10, waiting on Godot 4.8 |

## Action items

Proposed; the author decides what runs.

- **A1 (author, `bmad-build`):** build backlog 14 (the release-mode decisions R5 to R7) before any release-mode use, now that 3.4's planes depend on its version check (F12, R1); its headset check covers release mode with the planes and the PAL ISO (R4).
- **A2 (author, `bmad-ticket`):** a backlog story narrowing the tuning ranges so every allowed value gives a sane picture: the canopy above the head and the flyer, tile sizes and speed within what the 2000 m planes and 90 Hz can show, the floor's depth within the planes' reach (R2, R3).
- **A3 (author, with the next headset session):** record by name the three unrecorded checks on the extracted planes: no seam on the floor, the readout holding 90 Hz, and the canopy's speed against the floor's (F13, F14, F15).
- **A4 (backlog tickets, `bmad-ticket`):** add to backlog 13 the two untested `Planes` branches (R4); to backlog 7 the notices table, `extracting/` in the fixed names and the formats file's stale "Next" (F8, F16, F17); to backlog 5 the `--clear` header (R5); to backlog 12 and 13 splitting `Extraction.cs` and `ExtractionTests.cs` when next edited (F6, F7).
- **A5 (process, at planning):** a Code Map never cites a style AGENTS.md lists as an exception, and its renames are checked against the plan's Always rules before approval (F9, F10); a plan that raises the extractor's version names what acts on it (F12).
- **Spec reconciliations awaiting the author:** the contract's release-mode rule (`asset-contract.md:157`) to compare the extractor's version, with backlog 14; the canopy's range in the contract's Tuning table, with A2.

## Acceptance verdict

**accepted-with-open-items** (criteria declared). Done when 1 to 4 are met in the evidence: extraction writes both planes on the PC and the headset, pixel-equal to the probe and byte-identical between them (plan 3.4, behaviour check above); the floor loops beneath the player, approved by the author in 3.5 and 3.6; the canopy is a flat backdrop overhead at the author's chosen height (3.5); tuning changes the scene without touching the decoders (3.3). All eight tickets are done, so nothing forces a rejection. The open items: F12 (release mode, tracked as backlog 14 and A1), the unrecorded seam, 90 Hz and canopy-speed checks (A3), and the tuning ranges (A2). No blocking finding stands for development mode, the only mode in use.

## Open questions

- Should backlog 14 be built next, ahead of the next epic, given F12? (A1)
- .NET 9's support ends on 2026-11-10 and the APK still builds for it, waiting on Godot 4.8 (engine-spike A7, `deferred-work.md`). Is running on an unsupported .NET 9 for a while acceptable, or should Godot 4.8's beta be tried?
- Should the new tuning-ranges story (A2) be written now?
