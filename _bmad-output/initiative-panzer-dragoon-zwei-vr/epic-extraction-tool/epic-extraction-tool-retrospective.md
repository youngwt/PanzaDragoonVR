---
epic: epic-extraction-tool
date: 2026-10-21
verdict: accepted-with-open-items
criteria: declared
headless: false
---

# Retrospective: In-game extraction

## Epic summary

**Epic:** `epic-extraction-tool` (epic 2), covering CAP-3. The Done when has six criteria, declared in `epic-extraction-tool.md`.

**Tickets:** 13, all `done`; none at `built`, `pending_tickets` empty. Story 13 (board placement) was added on 2026-10-18 and built before the sweep (12). Backlog tickets 1 and 6 were folded in at inception, as entries 1, 7 and 8.

| Ref | Title | Range | Commits | Code |
|-----|-------|-------|---------|------|
| 2.1 | Texture IDs and extraction rules in the contract | `4298659..f70902f` | 2 | docs only |
| 2.2 | One texture file on the headset (tracer) | `f70902f..4fed875` | 3 | +1922 −17 |
| 2.3 | Where the 256-colour palettes are (spike) | `4fed875..3646707` | 4 | docs and diary |
| 2.5 | All Episode 3 textures | `3646707..3910800` | 4 | +381 −63 |
| 2.4 | Every disc image layout | `3910800..6881594` | 2 | +428 −85 |
| 2.6 | Colour-bank textures in colour | `6881594..73adf96` | 3 | +703 −37 |
| 2.7 | The manifest the checker accepts | `73adf96..53e6e5e` | 2 | +400 −30 |
| 2.8 | Android files folder (bug) | `53e6e5e..a9587f3` | 2 | +99 −14 |
| 2.9 | Background extraction, staging, progress board | `a9587f3..ac22a3b` | 5 | +902 −97 |
| 2.10 | Development and release modes | `ac22a3b..433eebc` | 2 | +797 −14 |
| 2.11 | PC matches the headset | `433eebc..47a5641` | 2 | +103 −2 |
| 2.13 | Board in front of the player | `47a5641..8cc9fa1` | 2 | +136 |
| 2.12 | Refactor sweep | `8cc9fa1..403db38` (HEAD, inferred) | 2 | +217 −210 |

Ranges come from each plan's `baseline_revision`, ordered by history. 2.5 was built before 2.4. The whole epic's code is `4298659..HEAD` over `lib/`, `game/`, `tools/` and `tests/`: 55 files, about +5,600 lines. Two merges sit before or at the start of the range (`2f4f7a6`, `4298659`), and none inside it.

**Evidence inventory:**
- Available:
  - the epic file and `tickets.toml`;
  - all 13 plans, each with dated headset checks where relevant and a Review Triage Log (one review per plan, with patches);
  - the spec, the asset contract, `zwei-disc-formats.md` and `stack.md`;
  - `deferred-work.md` and the backlog;
  - dev diary 3 (`devlog/2026-10-17-finding-the-palettes.md`);
  - the previous retrospective (`epic-engine-spike-retrospective.md`).
- Session logs, narrowed: this epic ran in one long session, and its decisions and headset checks are in the plans, so process lessons come from those.
- Not run here: nothing was run on the headset for this retrospective. Behaviour evidence comes from the plans' dated checks.

## Findings

Three passes ran as subagents: the aggregate views; the spec reconciliation; and `bmad-review`'s adversarial, edge-case and verification-gap lenses over the diff, weighted on the seams between stories. Four review claims were re-checked against the code before being written up: the all-skipped colour table, the board heading, the case-sensitive cue lookup and the compare script's emptied folders. All held.

### Seams between stories

| # | Finding | Source | Disposition | Lesson |
|---|---------|--------|-------------|--------|
| R1 | A colour table whose every pair is skipped gives an all-black colour RAM with no notice. Modes 0 and 4 are then written black and listed, and release mode never re-extracts that disc, deleting its image. | `lib/PdzDecoding/ColourBanks.cs` `Build` (returns a `ColourRam` even with every pair in `skipped`); seam 2.6 / 2.10 | **fix now** (A1): no applied block means no table, with the notice | test the "nothing usable" edge of each source |
| R2 | A failure while moving into place is shown under the heading "Nothing was extracted from the disc image", though the detail line says some new files may be in place. The old manifest then disagrees with the overwritten files' sizes until the next run. | `game/Logic/LaunchReport.cs:151` (`NothingExtracted` for every extraction problem), `ExtractionLines.cs:88`; seam 2.9 | **fix now** (A1): a heading true for that case | — |
| R3 | A cue's `FILE` names are looked up case-sensitively. A cue saying `GAME.BIN` for `Game.bin` extracts on Windows but not on the Quest, and its `.bin` would not be deleted. | `lib/PdzDecoding/CueSheet.cs:86-87` (`Path.Combine` then `File.Exists`), `DiscFolder.cs`; seam 2.4 / 2.10 | **fix now** (A1): fall back to a case-insensitive match in the cue's folder | — |
| R4 | `compare-headset.sh` sets aside unlisted files but leaves the folders they emptied, so `diff -rq` reports "Only in headset/images/…" and fails. | `tools/compare-headset.sh` (the Python step removes files only) | **fix now** (A1) | — |
| R5 | A cue sheet that names an `.iso` counts as two images and extracts nothing. | `DiscFolder.cs:46-56`; already in `deferred-work.md` from story 2.4 | **defer** (open question 1) | — |
| R6 | Release mode deletes the disc image after a successful extraction that wrote nothing (a disc with no textures the decoders handle). The old manifest, perhaps from another disc, stays. | `ExtractionMode.cs:137`, `DiscExtraction.cs:137-141`; seam 2.7 / 2.10. As the 2.10 intent and the contract say. | **accept as built**, question raised (open question 2) | — |
| R7 | The release skip matches any `pdz-extract ` version, so an app update that decodes more never re-extracts on a release device: the image is gone. | `ExtractionMode.cs:114-119`, tested on purpose | **accept**, question raised (open question 3) | — |
| R8 | The contract's fixed names (`manifest.json`, `disc/`, `development`) do not include `extracting/`, which extraction empties recursively. | `Extraction.cs:276-288`; `asset-contract.md`, The folder | **fix now** as a spec reconciliation (A3) | — |
| R9 | An exception while showing the reloaded assets after a successful extraction is reported as a failed extraction, and in release mode the image is already deleted by then. | `Main.cs:124-135`; seam 2.9 / 2.10 | **defer** (A5): rare, and wording only | — |
| R10 | A `.bin` left by a partial delete (the cue gone, the app killed between files) is never cleaned up: `Find` ignores loose bins. | `ExtractionMode.cs:147-172` | **accept**: the files are the player's own copy, harmless | — |

### Verification gaps

| # | Finding | Source | Disposition |
|---|---------|--------|-------------|
| V1 | The wiring in `game/DiscExtraction.cs` and `Main._Process` has no automated test: the exception-to-board mapping, `DeleteAfterExtraction(..., succeeded: error == null)`, and the release skip leading to deletion. Changing `succeeded` to `true` would delete images after failures and no test would fail. | no hit for `DiscExtraction` in `tests/`; `tools/test.sh` runs no Godot | **fix now** (A2): move the finish decision into `game/Logic` with tests, as 2.10 did for deletion |
| V2 | `pdz-extract`'s argument handling and exit codes are untested, and it catches only three exception types, so an `ArgumentException` (a bad output path) escapes unhandled, against `AGENTS.md`'s exit-1 rule. | `tools/pdz-extract/Program.cs:72-80` | **fix now** (A2) |
| V3 | Done when 1's ISO and single-`.bin` layouts ran on the PC and the desktop game, not the headset; the ISO only with the European disc. Progress on the board (Done when 5) is known from the log only; nobody captured it, as extraction takes about 0.3 s. | plans 2.4 Implementation Notes, 2.9 Verification | **accept**: the same library code runs on both, and 2.11's byte-for-byte check runs its .NET 9 build on the headset |

### Aggregate views

- **A-arch: architecture delta.**
  - The library uses no Godot types and targets .NET 9 and 10.
  - `game/Logic` stays free of both the engine and the library, and only `game/DiscExtraction.cs` joins them.
  - The background thread carries two strings in, and progress structs and the result out, through a `ConcurrentQueue`. All Godot calls stay on the main thread.
  - `_ExitTree` neither waits for nor cancels the extraction task. **Accept**: staging makes a killed run safe.
- **A-dup: duplication map.**
  - The `pdz-extract ` prefix is defined twice (`ExtractionModes.ExtractorPrefix`, `Manifest.Generator`) with no test tying them; tests hard-code `"pdz-extract 0.1"`.
  - Three one-line helpers differ (`DecodingException.OneLine`, `AssetManifest.OneLine`, inline in `ExtractionMode.cs:161`).
  - The manifest is read twice, by `AssetManifest` and `ExtractionModes.ParseOrigin`.
  - Disc identity is held twice (`SaturnHeader`, `DiscSource`).
  - Most of these follow from `game/Logic` being library-free. **Defer** (A4): one test pinning the prefix against the library's generator, and one shared one-line helper inside `game/Logic`.
- **A-size: size growth.**
  - `Main.cs` is 444 lines, holding the scene, the extraction watch, board placement, the OpenXR session and the refresh rate. `AssetFolder.cs` is 412 lines.
  - The library's files are all under 400 after the sweep.
  - **Defer** (A4): split `Main.cs` when the next epic adds to it.
- **A-pattern: pattern divergence.**
  - New log lines keep the `PDZ-` rule, and the new test names follow `Subject_Behaviour`.
  - The shared-fixture rule was followed for the empty-list rule.
  - The extractor's own manifest is not run through the shared asset-rule checks. **Fix now** (A2): one test running `fixed-disc-manifest.json` through the game's and the PC's rules.

### Spec against the build

- **S1: Done when evidence.** Every criterion is met, with dated checks (Behavior verification below), within the limits in V3.
- **S2: contract and code differ.**
  - No "not a Saturn disc" check exists, though the contract has that row (`DiscImage.Header`: "nothing here is checked").
  - Staging is emptied after decoding rather than first, and removed on failure rather than left for the next run. Both outcomes are safe.
  - The texture numbering rule (the record scan and the fits-the-`.CGB` filter) is recorded only in plan 2.2, not in the contract or the formats notes, though the IDs depend on it.

  **Spec reconciliation** (A3).
- **S3: stale docs.**
  - `stack.md` says the library is "not built yet".
  - `zwei-disc-formats.md` says mode 5 is "Not handled".
  - The spec's open question on the palette is answered.
  - The contract says "Story 1.4 settles" how the folder is reached.
  - The epic's two `Source conflict` lines are settled by later decisions but not marked so.

  **Fix now** (A3).
- **S4: plan bookkeeping.**
  - Plans 2.2 and 2.9 keep "still to do" lines for checks met lower down.
  - Plan 2.10's first headset checks predate its review fixes (only release mode was re-run).
  - Plan 2.5's test counts disagree (53 against 54).

  **Accept**: the record is append-only, and the later lines are there.
- **S5: built with no spec asking.** Skipping a missing disc-file pair, the MODE1/2048 cue, bins in subfolders, black for an unfilled colour code, mode 5, uncompressed PNGs, `compare-headset.sh` and the board placement. **Accept**, recorded so later retros do not re-flag them; R8 covers the one that needs a contract line.

### Process lessons

- **P1: the headset's own prompts cost the most time again.** Guardian or tracking prompts held launches in 2.2, 2.8, 2.9 and 2.10, and the app's log was lost when it closed (2.9's first damaged-disc check). Recording `adb logcat` in the background from before the launch fixed the second problem; captures every few seconds helped too. **Lesson:** start log recording before asking the author to launch, as standard.
- **P2: rechecking on the headset after review patches.** It was done for 2.10's release mode and 2.13. 2.8's recheck was held back, and 2.10's development-mode check predates the patches. **Lesson:** a headset story's last headset check comes after its review patches.
- **P3: the reviews caught real defects every time.** For example, the four-word de-duplication key (2.1), the damaged `EPISODE3.PRG` stopping extraction (2.6), a backwards README cause (2.8), orphaned bins (2.10) and stray files (2.11). Implementers twice went beyond the plan (2.8's untested choice, 2.10's "keep the image when nothing is written"); review sent both back to the intent. **Lesson:** keep one review per story; it pays.
- **P4: decisions stayed with the author.** The spike's palette answer, the ID scheme (sequence numbers, chosen knowing the renumbering cost) and the board's placement were all yours, recorded as dated decisions.

## Behavior verification

From the plans' dated checks (none re-run for this retrospective):
- **Done when 1:** the Japanese per-track bin/cue extracted 420 textures and a manifest on the Quest 3 (2.7 and 2.9, 2026-10-18); `--check` passes on the identical PC output (2.7). Single `.bin` and ISO: on the PC and the desktop game only (2.4), with the European ISO giving output identical to the Japanese disc.
- **Done when 2:** you judged the contact sheets of the mode 1/5 (2.5) and colour-bank (2.6) textures right; mode 0 checked as single-colour shadows (2.6).
- **Done when 3:** development mode re-extracted and kept the image across two launches; release mode extracted then deleted, skipped and deleted on an already-extracted folder (re-run on the patched build), and did not extract again (2.10, 2026-10-18).
- **Done when 4:** `compare-headset.sh` reported "identical: 421 files" (2.11, 2026-10-21), and again after the sweep (2.12).
- **Done when 5:**
  - The failure board was captured on the headset with a cut-short disc image, the scene running and the previous manifest unchanged (2.9, 2026-10-18).
  - Progress was seen in the log only.
  - The board stands in front of you after starting sideways and after recentring (2.13, 2026-10-21).
- **Done when 6:** the decoders have unit tests (Decoding 118, GameLogic 251, Python 97 at the end). No disc data is in git or the APK (2.11).
- Not exercised: a Japanese ISO, any layout but per-track on the headset, and the progress board on screen.

## Previous-retro follow-through

From `epic-engine-spike/epic-engine-spike-retrospective.md`:

| Item (owner: the author) | Landed? | Evidence |
|---|---|---|
| A1 shared fixtures; the PC rules in line with the game | Landed | backlog ticket 2, `313e6ca`, done (`f3022fd`) |
| A2 lone surrogate in `AssetManifest.Parse` | Landed | `a414cc0` |
| A3 try `push-assets.sh` with links and a failing remote `tar` | Not landed | backlog ticket 5 is still `backlog` |
| A4 settle ticket 1.1's second plan | Landed | `f5a1e9d` (`plan-1-1` marked done) |
| A5 reconcile the documents | Partly | `stack.md` and the spec touched by 2.1 (`08bc390`); backlog ticket 7 still open, and S3 lists what remains |
| A6 split `AssetManifest.cs`, write down conventions | Landed | `f399131`, `b73040a` (`AGENTS.md`) |
| A7 move off .NET 9 | Landed in part | `c01fc07`: SDK and tests on .NET 10; the APK stays on .NET 9 until Godot 4.8 (`deferred-work.md`) |
| A8 carry items into later epics | Landed | backlog tickets 8, 9, 10 (`f5a1e9d`) |

## Action items

All are proposed, not applied.

| # | Action | Kind | Owner | From |
|---|--------|------|-------|------|
| A1 | Four small extraction fixes, as one story: a colour table with no applied block counts as none (R1); a heading true for a failure while moving into place (R2); a case-insensitive fallback for cue `FILE` names (R3); remove emptied folders in `compare-headset.sh` (R4). | remediation | you, with `bmad-build` | R1–R4 |
| A2 | Tests for the untested wiring: move the finish decision (report line, delete or keep) from `DiscExtraction` into `game/Logic` with tests; test `pdz-extract`'s arguments and exit codes, and catch every exception there; run the extractor's recorded manifest through the shared asset-rule checks. | remediation, verification | you, with `bmad-build` | V1, V2, A-pattern |
| A3 | Reconcile the docs: add `extracting/` to the contract's fixed names; record the texture numbering rule; decide the "not a Saturn disc" row; fix the stale lines in S3 and mark the epic's two source conflicts settled. | spec reconciliation | you | R8, S2, S3 |
| A4 | When the next epic touches them: one test pinning `ExtractorPrefix` to the library's generator; one shared one-line helper in `game/Logic`; split `Main.cs`. | remediation | the next epic's sweep | A-dup, A-size |
| A5 | When a launch shows a display error after a successful extraction, say so rather than "extraction failed". | remediation | backlog | R9 |
| A6 | Make background log recording, started before the author launches, the standard headset check; rerun a headset story's checks after its review patches. | process | you and agents (`AGENTS.md`) | P1, P2 |

## Acceptance verdict

**Machine verdict: accepted-with-open-items.** The criteria are declared, in the epic's Done when.

- All 13 tickets are done, and `pending_tickets` is empty.
- Done when 1 to 6 are met with dated evidence, within the limits in V3.
- No finding blocks: R1 to R4 affect discs or folders unlike the author's, and V1 and V2 are test gaps on code that behaves correctly today.
- Open items are tracked in A1 to A6.

A human decision overrides this verdict. The epic file's `status` is unchanged; closing it is `bmad-ticket`'s job, after the closure check.

## Open questions

1. Should a cue sheet that names an `.iso` count as one image, so a common dump layout extracts? (R5)
2. In release mode, should an extraction that wrote nothing keep the disc image, rather than delete it as the contract says? (R6)
3. Should the release skip compare the extractor's version too, so an app update can re-extract? It cannot today, because the image is deleted. (R7)

## Decisions (author, 2026-10-21)

- **Verdict: accepted-with-open-items**, the machine verdict, accepted.
- **Yes:** a cue sheet that names an `.iso` counts as one disc image. (R5)
- **Yes:** in release mode, an extraction that wrote nothing keeps the disc image. This changes the asset contract's rule. (R6)
- **Yes:** the release skip also compares the extractor's version, so an app update can re-extract. This changes the asset contract's rule. (R7)

