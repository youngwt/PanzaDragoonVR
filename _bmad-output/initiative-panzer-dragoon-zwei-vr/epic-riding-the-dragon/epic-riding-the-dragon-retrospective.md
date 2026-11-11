---
epic: epic-riding-the-dragon
date: 2026-11-04
verdict: accepted-with-open-items
criteria: declared
headless: false
---

# Retrospective: Riding the dragon

## Epic summary

**Epic:** `epic-riding-the-dragon` (epic 4), covering CAP-5 and, for model decoding, CAP-3. Its Done when has four criteria, declared in `epic-riding-the-dragon.md`. The epic file's `status` is still empty: closing the epic is the author's, with `bmad-ticket`.

**Tickets:** 8. All are `done` in `tickets.py status`; none is at `built`; `pending_tickets` is empty. Only 4.6 was pulled (it was backlog 8) and carries a story file. The other seven were never pulled, so their `description` and `verify` come from `tickets.toml`.

**The author's going-in concerns** (2026-11-04): the headset checks left out when 4.8 was marked done; backlog 21 changing the tuning built-ins against its frozen plan; values from the disc still in tracked text; and the friction of adb and the Flatpak sandbox.

**Ranges.** Each plan's range runs from its `baseline_revision` to the next baseline in history order. Three things bend the rule, and each is recorded:

- 4.1 and 4.3 were built at the same time, 4.3 in a worktree merged at `301e5f9`. 4.3's baseline `b1f1fe3` is older than 4.1's `c5f4a25`, but its range `b1f1fe3..c5f4a25` holds only the work before the epic (the tuning-ranges merge and the inception). Both stories' commits are in `c5f4a25..9416721`, which is used for both.
- The seat fix, `plan-dragon-under-the-head.md` (a oneshot in the initiative folder, not a ticket of this epic), has its commits in the same range: `cbf5024`, `14f7232`, `3ed88ff`.
- 4.7's range is cut at `a5470f7`, when 4.7 was marked done. `a5470f7..f7a2f45` is backlog spike 20 and story 21, outside this epic. It is read here as context, because 4.8 swept its code and it changed the seat's built-ins.

| Ref | Title | Range | Commits | Code |
|-----|-------|-------|---------|------|
| 4.1 | The player sits on a placeholder dragon (tracer) | `c5f4a25..9416721` | `89eec7a`, merge `301e5f9`, `cbf5024`, `14f7232`, `3ed88ff`, `9416721` | `RiddenDragon.cs`, `Main.cs`, the seat fix, the Local Floor reference space |
| 4.3 | The library writes a model as a `.glb` | the same | `9a6b480`, `d3d309b` | `Model.cs`, `Atlas.cs`, `GlbWriter.cs`, `validate-gltf.sh`, recorded samples |
| 4.2 | How `.MDB` geometry is laid out (spike) | `9416721..41ff70f` | `408ea92`, `41ff70f` | notes only |
| 4.5 | Extraction writes the dragon forms | `41ff70f..67d0077` | `b40d313`, `67d0077` | `Dragons.cs`, `Extraction.cs`, `Extraction.Version` 0.3 |
| 4.6 | The provisional model rules are confirmed | `67d0077..020dfa5` | `e30b24c`, `020dfa5` | contract wording only |
| 4.4 | The seat comes from the tuning file | `020dfa5..41699e3` | `80cbef5`, `1f417c9`, `41699e3` | three settings in `Tuning`, `RiddenDragon` |
| 4.7 | The player rides the chosen dragon | `41699e3..a5470f7` (cut) | `925adc6`, `a5470f7` | `dragon.ride`, `Extraction.Version` 0.4 |
| (backlog 20, 21) | Debug menu spike and tuning list | `a5470f7..f7a2f45` | `7408d72`, `7e49a19`, `608f54f`, `f7a2f45` | `TuningMenu`, `pull-tuning.sh`, new built-ins |
| 4.8 | Refactor sweep | `f7a2f45..a0362e1` (inferred, to `HEAD`) | `0b504ca`, `a0362e1` | the sweep |

The whole epic is `c5f4a25..a0362e1`: 25 commits, with one merge (`301e5f9`), measured (`merge_count` 1, `merges_measured` 1). `binary_revisions` is 0 on every path. No commit subject names a ref apart from the `docs: mark ...` lines, so attribution is by range. `git_evidence.py` was run from `/home/deck/.claude/skills/bmad-retrospective/scripts/`.

**Evidence inventory:**

- Available:
  - the epic file, with Description, Outcome, Done when 1 to 4, Boundaries and dated Notes;
  - `tickets.toml`, and `tickets.py status` for all eight;
  - the story file of 4.6;
  - all eight plans, plus the seat fix's plan. Each is `done` with a baseline. Each has one quick-lens Review Triage Log except 4.6, whose log is in its notes. Dated author lines record the headset checks;
  - the spec's Capabilities (CAP-3, CAP-5) and the initiative's Done when. The initiative file has no Requirements section, as the previous retrospective found;
  - `asset-contract.md`, `zwei-disc-formats.md`, `AGENTS.md`, `deferred-work.md`;
  - the backlog plans of spike 20 and story 21;
  - the previous retrospective, `epic-forest-floor-and-canopy/epic-forest-floor-and-canopy-retrospective.md`.
- Session logs: **one available**, the conversation behind backlog 20, backlog 21 and 4.8 (`~/.claude/projects/-home-deck-projects-PanzaDragoonVR/15cd57c4-ba9b-4c37-8fab-8e06d673aa0f.jsonl`). It is known here only through its summary. No session log was read for 4.1 to 4.7, so what was tried and dropped in those builds comes from the plans alone.
- Not opened: `rom/`, `extracted/` and the headset. Statements about disc output rest on the plans' records, apart from the behaviour check below.
- Checked and clean: no path in `c5f4a25..a0362e1` is under `rom/` or `extracted/`, and none is a `.cue`, `.bin`, `.iso` or `.png`.

## Findings

Derived over `c5f4a25..a0362e1`. A file changed in several ranges is counted once, and each ticket's range above stays as its provenance. The views were derived inline from `git_evidence.py`, `wc -l`, `grep` and reading the plans and files; no dependency tool was run.

### Architecture delta

- **Checked and unchanged.** The game still reaches the library from two files only, `game/AssetIds.cs` and `game/DiscExtraction.cs`, as at `c5f4a25` (`git grep -l PdzDecoding` at both ends). `game/Logic/` uses neither Godot nor the library, and the library has no Godot reference. The new model path follows the initiative's Boundaries: the library writes `.glb` files and manifest entries (`Dragons.cs`, `Extraction.cs`), and the game reads them only through the asset folder (`AssetFolder.Model`, plan 4.1 Always).
- **F1. The seat now lives inside each model.** 4.5 put the rider's offset into each form's origin (plan 4.5 Always, third item), so the game needs no constant from the disc at launch, and the config file the epic decided on was never needed (epic Notes, line 39). This was a design choice made inside one ticket, and the epic file records it. Accepted.

### Duplication map

- **F2. Disc values reached tracked text again and again, and each time a review or a sweep caught them after they were written:**
  - 4.2's review found the rider's offset table copied from `1ST_READ` (plan 4.2 Review Triage Log, the high item);
  - 4.7's review found a count from the disc re-recorded in the README (plan 4.7 Review Triage Log, the medium item), and deferred the rest of the README's sample output;
  - 4.8's survey found more in `tools/README.md`, `Dragons.cs`, `Extraction.cs` and `zwei-disc-formats.md` (plan 4.8 Code Map), and its review found two more values in the colour-mode table (plan 4.8 Review Triage Log);
  - some are still there: comments in `ColourBanks.cs` and `Planes.cs`, test literals in `DragonsTests.cs` and `PlanesTests.cs`, and tables in `zwei-disc-formats.md` (`deferred-work.md`, last entry);
  - the previous retrospective itself put the plane offsets measured on both discs into git, in its Behavior verification (`epic-forest-floor-and-canopy-retrospective.md:171`).

  No check runs before a commit: `AGENTS.md`'s rule is read by people and agents only.
- **F3. The built-ins are still in four places.** 4.4 added three settings to all four (plan 4.4 Intent), and backlog 21 changed all nine in all four (`608f54f`, seven files). Only the example file is held to `Tuning.BuiltIn` by a test, as the previous retrospective found (its F4).
- **Removed during the epic:** 4.8 brought the repeated paths and IDs under one constant each, merged the two carry blocks into `CarryOrPlaceholder`, built the facing once, and gave each test project one repo walker (plan 4.8 Tasks).

### Size growth

Ranked on `files` (non-merge churn), then each file's size was measured at `HEAD`.

- **F4. `game/Main.cs` is 230 lines.** It was 199 at `c5f4a25`. Ticket 3.7 set "under 200 lines" (the previous retrospective, F5), and this epic passed that limit with the dragon's carry and the tuning list's start and drive. 4.8 merged the carry blocks but did not split the file.
- **F5. `lib/PdzDecoding/Dragons.cs` is new at 523 lines**, the library's largest file. It holds the form list, the search for the seat table, the tree walk, the matrices and the polygons, in about 20 methods, the largest being `Part` (`:397` onwards). It is one format, so it is not judged a god-class. `GlbWriter.cs` is 440 lines.
- **F6. `lib/PdzDecoding/Extraction.cs` grew from 414 to 458 lines, and `tests/Decoding.Tests/ExtractionTests.cs` from 1,109 to 1,220.** The previous retrospective deferred splitting both "when backlog 12 or 13 next edits them" (its F6 and F7). Neither ticket ran. This epic edited both files instead, and nothing named the split.
- **F7. The test files grew more than the code.** `GlbWriterTests.cs` is 638 lines and `DragonsTests.cs` 504, both new. These are in proportion to the format knowledge they hold, so this is recorded and not a defect.

### Pattern divergence

- **Checked and followed:**
  - the two new scripts, `tools/validate-gltf.sh` and `tools/pull-tuning.sh`, define `die` and `say` with their own prefix and exit 1;
  - the new game log lines are tagged `PDZ-ASSETS` and have no full stop (`game/Main.cs:113`, `game/Main.TuningMenu.cs:49,93,97`);
  - the new tests use `Subject_Behaviour` names;
  - the library's errors in caller-supplied models are one-line `ArgumentException`s, like `DecodingException`'s (plan 4.3 Implementation Notes);
  - no .NET compressor is used: atlases go through `PngWriter`, and the recorded samples are byte-compared.
- **F8. Two version rises named what acts on them.** 4.5 raised `Extraction.Version` to 0.3 and 4.7 to 0.4, each saying a folder from the previous version must gain the new files in release mode (plan 4.5 and plan 4.7 Implementation Notes). 4.7 added a test for a 0.3 folder (plan 4.7 Review Triage Log). This follows the previous retrospective's A5 ("a plan that raises the extractor's version names what acts on it").
- **F9. A recorded test file is too large to commit.** `two_atlases.glb` is about 16 MB, because any second atlas is a full one and the PNGs are stored uncompressed. Only its SHA-256 is recorded (plan 4.3 Implementation Notes). The test still checks the bytes, by hash. Accepted.

### Spec against the build

- **F10. Done when 3 and 4 on the real dragon rest on the author marking 4.7 done, with no word on each item.** On 2026-10-30 the author said only "happy for now" about form 9. Asked to confirm the wings, head, tail and steady flight, the author "replied by marking the story done" (plan 4.7 Implementation Notes). The two placeholder checks are stronger:
  - in 4.1 the author saw the grey dragon and found it off centre, which led to the seat fix (`plan-dragon-under-the-head.md`, Intent);
  - in 4.6 the author looked at form 1 in the headset and judged its size, wing edges and filtering (plan 4.6 Implementation Notes).

  4.8's plan then asked for the same items again after the sweep (plan 4.8 Acceptance, third item). It was marked done on 2026-11-04 with no install, no `compare-headset.sh` and no headset look (plan 4.8, Verification, the author's line).
- **F11. The swept code has not run on the headset.** 4.8 changed `game/Main.cs`, `Main.Board.cs`, `Main.Tuning.cs`, `RiddenDragon.cs` and the library (`0b504ca`). PC extraction is byte-identical before and after the sweep on both discs (plan 4.8, and the behaviour check below). The game code is compiled by no test project, and the last headset run was story 21's build, before the sweep.
- **F12. The seat's built-ins changed outside the epic, under a plan that said they would not.** Story 21's frozen Never says "no change ... to any range or built-in value". On the author's word, `608f54f` changed all nine, including the three seat settings that 4.4 added, and the plan records this (backlog 21 plan, the author's line of 2026-11-01). Measured against this epic, it fulfils the epic's decision that "the author's chosen values become the built-ins" (epic Notes, line 47), which 4.7 had left at 0, 0 and 1 (plan 4.7 Implementation Notes). The values are the author's choices, offsets from the seat, not values from the disc.
- **F13. The initiative's 90 Hz is still not recorded with extracted assets.** The initiative's Done when 1 is "rides the extracted dragon between the extracted forest floor and canopy at a steady 90 Hz". 4.1's acceptance asked for 90 Hz on the placeholder, and the implementer saw it (plan 4.1 Implementation Notes). No plan in this epic records the frame rate with the extracted dragon or planes. The last named 90 Hz record is the .NET spike's, on 2026-10-16, before any extracted asset.
- **F14. Behaviour that was added without a ticket asking for it.** Recorded so that later runs can tell it from a defect:
  - the seat placed under the head at launch and after a recentre, and the switch to the Local Floor reference space (`plan-dragon-under-the-head.md`, and its follow-up);
  - colour mode 5 decoded as well as mode 1 (plan 4.5 Implementation Notes);
  - `compare-headset.sh` pulling `models/` and removing emptied folders (plan 4.5 Implementation Notes and Review Triage Log). This also lands half of the previous retrospective's A1 R4.
- **F15. The open questions the epic leaves:**
  - where the wing animation comes from (epic Notes, line 40), which is backlog 18;
  - per-corner shading, backlog 19;
  - the second atlas, `half` and `mesh`, which no dragon uses (plan 4.6 Review Triage Log);
  - `cutout` pixels drawn black in F3D, which only the game was checked against (plan 4.5, the author's line; plan 4.6).
- **Met as specified:**
  - Done when 1: ten forms and `dragon.ride` written on both discs, with 0 validator errors, opened in F3D by the author (plan 4.5, the author's line), and `compare-headset.sh` identical in 4.7 (plan 4.7 Implementation Notes);
  - Done when 2: form 9 chosen (epic Notes, line 35);
  - no Provisional mark left in the Models section (plan 4.6 Verification).

### Diff-scope review (adversarial, edge-case, verification-gap lenses)

The review ran through `bmad-review` over `c5f4a25..0b504ca`, limited to `game/`, `lib/`, `tools/`, `tests/` and `AGENTS.md` (5,253 diff lines, including backlog 20 and 21). It weighted four boundaries between tickets: the writer against the decoder (4.3 and 4.5), the placeholder against the seat settings and `dragon.ride` (4.1, 4.4 and 4.7), the version rises against the release-mode rule (4.5, 4.7 and backlog 14), and the tuning list against the sweep's renames (backlog 21 and 4.8). Items already in the plans' triage logs were not raised again. Each lens ran in its own sub-agent. The orchestrator re-checked each finding below against the code before routing it; findings that did not hold are listed as rejected.

- **R1. In release mode, a run that writes everything but the dragons deletes the disc image** (adversarial and edge-case). `DeleteAfterExtraction` deletes it after any successful run that wrote something (`game/Logic/ExtractionMode.cs:136-143`), and a run where `Dragons.FromDisc` gave a notice still writes the textures and planes (`lib/PdzDecoding/Extraction.cs:254-257`). The player then rides the placeholder, and getting the dragons means copying the image back. The notice is in the log and the result's line, but it is not a problem on the board. This is the same shape as backlog 14's decision on a run that wrote nothing, which keeps the image, but taken one step further.
- **R2. The writer's errors escape `FromDisc`** (all three lenses). `FromDisc` catches only `DecodingException` (`lib/PdzDecoding/Dragons.cs:156`). `GlbWriter.Write` throws `ArgumentException` for a model it rejects, and `Atlas` throws `InvalidOperationException` (`GlbWriter.cs:39,439`, `Atlas.cs:76,120`). `FromDisc`'s summary says "nothing here stops the rest of extraction". The verification-gap lens found no disc input that reaches a writer rejection today, so this is a mismatch between the contract and the code, not a failure that has been seen. It sits on the 4.3/4.5 boundary.
- **R3. Nothing ties a change in output to a rise in `Extraction.Version`** (adversarial and verification-gap). The recorded manifest has no model entries (`tests/Decoding.Tests/expected/fixed-disc-manifest.json`). The dragon tests compare against `GlbWriter.Write(Dragons.Decode(...))` worked out in the test itself (`DragonsTests.cs:74-94`), so changed output still passes them. Release mode skips extraction for the current generator. 4.5 and 4.7 remembered to raise the version (F8); nothing makes the next change remember.
- **R4. The seat's arithmetic has no test** (verification-gap). The meaning of `seat_forward`'s and `seat_height`'s signs, the scaling about the seat, the dragon's half-turn and the order of `SitUnder` and `Adjust` are in `game/RiddenDragon.cs:25,42-57`. No test project compiles that file. The library's half-turn is pinned (`DragonsTests.cs:364`); the game's is not. `BoardPlacement` shows the pattern of engine-free placement maths in `game/Logic/`.
- **R5. The seat waits on the board** (adversarial and edge-case). `PlaceBoardAndSeat` returns before seating the dragon when `BoardPlacement.InFrontOf` gives no pose (`game/Main.Board.cs:52-61`), so a recentre while looking steeply up or down leaves the dragon where it was until the board can be placed. The seat fix put the two together (`plan-dragon-under-the-head.md`), and 4.8's rename made the coupling visible but kept it.
- **R6. `SitUnder` writes a global position and `Adjust` a local one** (adversarial). This holds only while the `Dragon` node's parent is at the origin (`game/RiddenDragon.cs:41-57`). It is right today and would break if the dragon is moved under a moving node later.
- **R7. Two smaller decoding gaps** (adversarial). Neither is known to occur on the dragons:
  - an untextured polygon's colour is always read as RGB555, without checking the bit that marks a colour-bank code (`Dragons.cs:485-490`);
  - colour-calculation modes other than the two mapped to `Half` are drawn at full strength, with no count (`Dragons.cs:452-465`).
- **R8. The ridden form is decoded twice** (adversarial). It is decoded once as form 9 and again only to change its root name (`Dragons.cs:149-153`), while `Model` is a record that `with` could rename.
- **R9. The tuning list shows two decimals** (adversarial). The log, the built-ins and the documents use three (`game/Logic/TuningMenu.cs:90`), so the author cannot tell on the list whether a value is at its built-in.
- **R10. Pressing A on the same frame as the B that shows the list saves at once** (edge-case). `Step` toggles `Shown` and then acts on A (`TuningMenu.cs:56-72`). The plan's latch covers a stick held while the list was hidden, not a press on the showing frame.
- **R11. Two lines of text are out of date:**
  - the library's assembly `<Version>` is 0.3.0 while `Extraction.Version` is 0.4 (`lib/PdzDecoding/PdzDecoding.csproj:11`), from the edge-case lens;
  - `DeleteAfterExtraction`'s summary says "a texture or a plane" (`ExtractionMode.cs:138`), while the caller now passes `WroteAnything`, which counts models too.
- **R12. Older plans name the renamed methods** (adversarial). `plan-dragon-under-the-head.md` (Review Triage Log) says `PlaceBoard` kept its name, and the backlog 20 and 21 plans name `PlacePlanes`. 4.8 renamed them.
- **Rejected after re-checking:**
  - "a 0.3 folder in release mode never gains `dragon.ride`, because the image is gone". This is the author's decision of 2026-10-21: the player copies the image back and the version check lets it extract (backlog 14, Notes). Plan 4.7's line "so a folder from 0.3 gains `dragon.ride` in release mode" leaves out that condition, but the rule is as decided.
  - "`compare-headset.sh` dies when the headset has no `images/`". `images/` is always written when anything is: extraction throws when no texture file is decoded (`Extraction.cs:259-262`).
  - "the built-ins depend on `RideForm` and `UnitsPerMetre` with no tie". This is true but by design: they are the author's offsets for the chosen form. Recorded in R4's remedy, not as its own item.
  - "MASK on flat colours costs fill rate", and "the ten forms are written though nothing reads them". Both follow the epic's decisions (the contract's materials, and "the extraction always writes all ten dragon forms", epic Notes, line 51), with no measured cost.
  - "a long unfocused gap arrives as one large step", and "B held as focus returns toggles the list". These were not reproduced. The plan drives the list only while the session is focused (backlog 21 plan, Implementation Notes). Low, and not routed.
- **Checked and clean, by the verification-gap lens:** the writer's checks reach the decoder's output (`DragonsTests.Write_DecodedModel_PassesThroughTheWriterWithPartsAndName`, `GlbCheckTests.WrittenModel_IsUsable`), and the flips, modes, axes and seat subtraction are pinned on both sides of the `ModelQuad`/`ModelPart` boundary (`DragonsTests.cs:339-397`, `GlbWriterTests.cs:318-400`). The sweep's renames are referenced only from compiled code. The one test that reads game source does not touch them.

## Dispositions

| Finding | This instance | Next time |
|---|---|---|
| F1 seat in the model's origin | accept | nothing |
| F2 disc values in tracked text | **fix**: a story to clear the remaining ones, including the previous retrospective's line 171, with the author's reading of "where to look" (A1) | a check before each commit that fails on numbers from a list kept in `extracted/` (A1) |
| F3 built-ins in four places | accept, as last time | nothing |
| F4 `Main.cs` at 230 | defer: split when the next epic edits it | a size limit a ticket set is checked by the next sweep |
| F5, F7 large new files | accept | nothing |
| F6 `Extraction.cs`, `ExtractionTests.cs` | defer again, to backlog 12 or 13 | nothing |
| F8 version rises named | accept: last retro's A5 practised | nothing |
| F9 hashed sample | accept | nothing |
| F10, F11 headset checks on form 9 and after the sweep | **fix at the next headset session** (A2) | a story whose criterion is a look in the headset names each item, and is marked done only with the author's word on each, or with the gap written into the plan, as 4.8 now is |
| F12 built-ins changed by backlog 21 | accept: fulfils the epic's decision | a change to a frozen plan's Never is its own commit naming the decision, as `608f54f` was |
| F13 90 Hz with extracted assets | fix at the next headset session (A2) | as F10 |
| F14 behaviour added unasked | accept | nothing |
| F15 open questions | tracked: backlog 18, 19 | nothing |
| R1 release mode deletes the image after a run with no dragons | **fix**: a story (A3) | nothing |
| R2 writer errors escape | fix with A3 | nothing |
| R3 version rise not enforced | fix with A3 | nothing |
| R4 seat maths untested | fix: move it into `game/Logic/` with tests (A4) | placement maths goes into `game/Logic/` from the start, as `BoardPlacement` does |
| R5, R6 seat waits on the board; mixed spaces | fix with A4 | nothing |
| R7 colour bank bit, unmapped modes | defer to backlog 19 (shading), which reads the same words | nothing |
| R8 decoded twice | fix with A3 | nothing |
| R9, R10 the list's decimals and same-frame A | fix: small, with A4 | nothing |
| R11 stale version and summary | fix with A3 | a version rise changes the `.csproj` too |
| R12 old names in plans | accept: plans record their own time, and 4.8's Code Map names the renames | nothing |

## Behavior verification

Run for this retrospective on 2026-11-04, on the PC at `a0362e1`:

- `tools/test.sh`: 105 Python, 334 GameLogic and 250 Decoding tests, all pass.
- `pdz-extract` on the JP cue and the PAL ISO, into `extracted/retro4-JP` and `extracted/retro4-PAL`. Both exit 0, and each output is identical (`diff -r`) to 4.8's after-sweep run. Each writes the ten forms and `dragon.ride`, and `dragon_ride.glb` is byte-identical between the two discs.
- `tools/validate-gltf.sh` on all 22 `.glb` files from both discs: 0 errors, 0 warnings.

Not run when this retrospective was written: the headset, which was not connected (`adb devices` listed none). So the swept game code, the seat, the facing, the tuning list after the sweep and the frame rate with extracted assets were not exercised then (F10, F11, F13).

Run later the same day, as A2, on the headset with 4.8's build (`a0362e1`):

- `tools/build-quest.sh`: built, installed and launched. The APK holds `PdzDecoding.dll` and no disc data.
- Development mode extracted the JP cue on launch. `tools/compare-headset.sh` reported identical, the manifest included. This is also the first check since the sweep that the library's .NET 9 build on the headset matches the PC's.
- The headset's `tuning.json` held the built-ins exactly, so the look was at the built-ins.
- The author, after a recentre, said "all good" to each item asked: the wings either side, the head in front, the tail behind, the dragon steady over the scrolling forest, the readout holding 90 Hz, no seam on the floor, the canopy's speed against the floor, and the tuning list working.
- The log's frame-rate line read target 90, an average of 90, and 0.33% of frames slow over the session. The log shows the seat placed under the head after the recentre.

## Previous-retro follow-through

From `epic-forest-floor-and-canopy/epic-forest-floor-and-canopy-retrospective.md`, Action items A1 to A5 and the author's decisions of 2026-10-26. The owner of each is the author, as that file names it.

| Item | Landed? | Evidence |
|---|---|---|
| A1, build backlog 14 before any release-mode use; check release mode with the planes on the headset | **Landed** | `cdbd9f0`, headset check in release mode `cc0c0af`, marked done `6644053`. 4.5 and 4.7 then relied on its version check (F8) |
| A2, a backlog story narrowing the tuning ranges | **Landed** | Backlog 17, `a347a2a`, done `fdfc1d3`, merged at `b1f1fe3`, the base of this epic |
| A3, record by name the seam, 90 Hz and the canopy's speed against the floor at the next headset session | **No evidence found** | No plan in this epic or the backlog since 2026-10-26 names the seam, the canopy's speed or a measured frame rate (grep of the plans for `90 Hz`, `seam`, `same speed`). There were several headset sessions (4.1, 4.4, 4.6, 4.7, backlog 21). See F13 |
| A4, add notes to backlog 13 (two `Planes` tests), 7 (notices table, `extracting/`, the formats file's "Next"), 5 (`--clear` header), 12 and 13 (split `Extraction.cs` and `ExtractionTests.cs`) | **Not landed** | No note dated after 2026-10-21 in those ticket files (grep). Both files to split grew in this epic (F6) |
| A5, at planning: a Code Map never cites a style listed as an exception; renames are checked against the Always rules; a version rise names what acts on it | **Partly.** The third half landed; no evidence found for the first two | Plans 4.5 and 4.7 name what acts on their rises (F8). No Code Map in this epic cites an exception style (read of the eight plans). The rename check was not needed: 4.8's renames were of private names, which its Always rule allowed |
| Decisions: stay on .NET 9 until Godot 4.8 stable; backlog 14 next; tuning-ranges story now | **Landed** | Backlog 14 and 17 as above. `game/PanzaDragoonVR.csproj` still builds Android for `net9.0`, as decided. .NET 9's support ends on 2026-11-10, six days after this retrospective |

Carried from earlier retrospectives as not landed: backlog 5, 7, 12 and 13 are all still `backlog` (`tickets.py next`), so the extraction-tool retrospective's A1 R2 and R3, its A2, A3 and A5, and the engine-spike retrospective's A3 and A5 stay open. Its A1 R4 (remove emptied folders in `compare-headset.sh`) landed in 4.5 (plan 4.5 Review Triage Log).

## Action items

Proposed; the author decides what runs.

- **A1 (author, `bmad-ticket` then `bmad-build`):** a story to clear the values from the disc that remain in tracked text, from `deferred-work.md`'s last entry and the previous retrospective's line 171, once the author has said which offsets count as "where to look". Test literals change with their tests' assertions, on the author's word. Add a check that `tools/test.sh` or a pre-commit hook runs: it reads a list of the author's measured numbers from a git-ignored file under `extracted/`, and fails when a staged file holds one (F2).
- **A2 (author, at the next headset session):** with 4.8's build installed, run `tools/compare-headset.sh`. Then look, and record by name: the wings either side, the head in front, the tail behind, the dragon steady over the scrolling forest, the readout holding 90 Hz with the extracted dragon and planes, the floor's seam, and the canopy's speed against the floor's. Check that the tuning list still works (F10, F11, F13, previous A3). Recording these closes Done when 3 and 4 on evidence, not on a mark.
- **A3 (author, `bmad-ticket`, a backlog story on extraction's dragons):** keep the disc image in release mode when the dragons gave a notice, or put the notice on the board as a problem (R1); turn the writer's exceptions in `FromDisc` into the one-line notice, with a test (R2); a test that fails when extraction's recorded output changes and `Extraction.Version` does not, with model entries in the recorded manifest (R3); rename the ridden form with `with`, not a second decode (R8); bring the `.csproj` version and `DeleteAfterExtraction`'s summary up to date (R11).
- **A4 (author, `bmad-ticket`, a backlog story on the seat):** move the seat's position, facing and scale into an engine-free helper in `game/Logic/` with tests of each sign, the half-turn and either order of recentre and tuning change (R4); seat the dragon whether or not the board can be placed, and use one space for its position (R5, R6); show three decimals in the tuning list, and ignore A on the frame that shows it (R9, R10).
- **A5 (process, `AGENTS.md` or the build's checklist):** a ticket whose criterion is a look in the headset is marked done only with the author's word on each item, or with the gap written into its plan; a change to a frozen plan's Never is a separate commit that names the decision; a version rise changes the `.csproj` too (F10, F12, R11).
- **A6 (to backlog notes, `bmad-ticket`):** carry the previous retrospective's A4 notes into backlog 5, 7, 12 and 13, which did not happen last time. Add R7 (the colour-bank bit and the unmapped colour modes) to backlog 19.

## Acceptance verdict

**accepted-with-open-items** (criteria declared). The machine verdict, confirmed by the author on 2026-11-04 (see Author's decisions).

- **Done when 1:** met in the evidence. Extraction writes the ten forms and `dragon.ride` on both discs, with 0 validator errors, and identical on the PC and the headset in 4.7. The author opened the forms in F3D (plan 4.5, the author's line), and this retrospective's run confirms it on the PC.
- **Done when 2:** met. Form 9 was chosen on 2026-10-30 (epic Notes).
- **Done when 3 and 4:** met on the placeholder in 4.1, and on form 1 in 4.6. For form 9 they rest on the author marking 4.7 done when asked for the four items (F10). After the sweep they were not checked again (F11). The author's marks are a human decision and count. A2, on 2026-11-04 after the sweep, then recorded the look item by item on form 9 (see Behavior verification), so both now rest on a named record.
- All eight tickets are done, so nothing forces a rejection. No finding blocks development mode, the only mode in use.
- The open items are A3 (R1 to R3, release mode and the version), A4 (the seat's maths) and A1 (disc values in git). A2 is closed.

## Open questions

- Which offsets in `zwei-disc-formats.md` and the code comments count as "where to look", and so may stay (A1)?
- .NET 9's support ends on 2026-11-10. The decision of 2026-10-26 stands until Godot 4.8 is stable. Is there news of 4.8 that changes it?
- Should the next work be the wing spike (backlog 18), or A3 and A4 first?

## Author's decisions

- 2026-11-04: the epic is accepted with open items and closed now; the headset look (A2) follows at the next headset session rather than holding the epic open.
- 2026-11-04: A6 done: the previous retrospective's A4 notes are added to backlog 5, 7, 12 and 13, and R7 to backlog 19.
- 2026-11-04: the order after this retrospective is A2 at the next headset session, then stories for A3 and A4, then backlog 18.
- 2026-11-04: A2 done. The author looked in the headset with the swept build and said "all good" to each item; recorded in Behavior verification and in the sweep's plan.
- 2026-11-09: A1 dropped. Anything that is not an image, mesh or sound may be in git, so simple values measured from the disc, such as offsets, may stay; `AGENTS.md`'s rule now says so. F2 is accepted on this.
- 2026-11-11: A3, A4 and A5 done, each by a plan in the initiative folder that the author marked done. Every action item of this retrospective is now closed.
