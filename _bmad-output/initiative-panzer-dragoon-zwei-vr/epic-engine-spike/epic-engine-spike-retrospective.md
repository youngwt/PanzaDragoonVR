---
epic: epic-engine-spike
date: 2026-10-16
verdict: accepted-with-open-items
criteria: declared
headless: false
---

# Retrospective: Engine spike on Quest 3

## Epic summary

**Epic:** `epic-engine-spike` (epic 1 of the Panzer Dragoon Zwei VR initiative), covering CAP-1 and CAP-2. The Done when has five criteria, declared in `epic-engine-spike.md`.

**Tickets, in build order:** all 10 are `done`, and none is still at `built`. `pending_tickets` is empty.

| Ref | Title | Plan baseline | Range (baseline to next baseline) | Commits |
|-----|-------|---------------|-----------------------------------|---------|
| 1.1 | Toolchain and a C# VR scene on the headset | `b4c6709` | `b4c6709..63fae81` | 4 |
| 1.2 | Decide the manifest format and folder layout | `63fae81` | `63fae81..3d75c71` | 1 |
| 1.3 | Test scene at 90 Hz | `3d75c71` | `3d75c71..dc00a18` | 6 |
| 1.6 | Decide the standard model format | `dc00a18` | `dc00a18..e0c8fba` | 2 |
| 1.9 | Manifest writer for your own files | `e0c8fba` | `e0c8fba..d5b3627` | 2 |
| 1.4 | Textures from the app's files folder | `d5b3627` | `d5b3627..07fd277` | 2 |
| 1.5 | Launch report and fallback | `07fd277` | `07fd277..cb1b739` | 2 |
| 1.7 | Grey cube loaded from a model file | `cb1b739` | `cb1b739..f152b6a` | 3 |
| 1.10 | Find why the display stays at 72 Hz | `f152b6a` | `f152b6a..3707b89` | 2 |
| 1.8 | Refactor sweep | `3707b89` | `3707b89..4bb3960` (HEAD, inferred) | 3 |

The plans' baselines put the builds in the order above, which is not the order of the ids. Each range comes from `git_evidence.py`; the outputs were kept in the session scratch folder and not committed. No range holds a merge. The whole epic's code is `b4c6709..4bb3960` over `game/`, `tools/` and `tests/`: 49 files and about 7,200 lines added.

**Evidence inventory:**
- Available:
  - the epic file, `tickets.toml` and `tickets.py find` for each entry
  - all ten plan files, each with a `baseline_revision`, a Review Triage Log and Verification
  - `deferred-work.md`
  - the spec, the asset contract and `stack.md`
  - the dev diaries `devlog/2026-10-08-milestone-0-engine-spike.md` and `devlog/2026-10-15-loading-from-the-headset.md`
  - three Claude Code session transcripts, under `~/.claude/projects/-home-deck-projects-PanzaDragoonVR/` (`11f0d555…`, `d59e37cd…` and this session `01895c08…`)
- No story files: no entry was pulled, so each entry's `description` and `verify` are the intent.
- A second plan for ticket 1.1 sits outside the epic folder: `plan-1-1-csharp-vr-scene-on-headset.md`, with no `ticket` field and status `built`. See finding S1.
- No previous retrospective: this is the initiative's first epic.
- Session logs, narrowed: the transcripts were scanned for the author's own typed messages only. Most of the author's turns are headset screenshots and subagent hand-backs, so process lessons come mainly from the plans' Implementation Notes, Review Triage Logs and the dev diaries.


## Findings

All three Phase 2 passes ran as subagents, then the strongest claims were re-checked against the code. The three passes were: the aggregate views, the spec reconciliation, and `bmad-review` with the adversarial, edge-case and verification-gap lenses over `b4c6709..HEAD`, weighted on the seams between tickets. The reviewer probed the C# rules with a small console program compiled against `AssetManifest.cs` and `GlbCheck.cs`, and ran `write_manifest.py --check` on hand-made folders. Each finding has a disposition (**fix now**, **defer** or **accept**) and the lesson that would prevent the next one.

### Seams between tickets (review lenses)

| # | Finding | Source | Disposition | Lesson |
|---|---------|--------|-------------|--------|
| R1 | `write_manifest.py --check` checks a model's header only. It passes a `.glb` the game rejects: external or `data:` URI, `extensionsRequired`, wrong length, no mesh. The reviewer built one with `extensionsRequired` and an image URI of `../../etc/passwd`; `--check` exited 0. | `tools/manifest.py:91-97` (1.9) against `game/Logic/GlbCheck.cs:146-229` (1.7) | **defer** to the extraction tool epic, which writes models through `manifest.py` (A1) | V1 |
| R2 | Python's `json.loads` accepts `NaN`, `Infinity` and nesting deeper than 64; the game's `JsonDocument` rejects them. The whole manifest is then unreadable, so every asset falls back. A Python extractor writing `meta` with `json.dumps` could produce a `NaN`. | `tools/manifest.py:255`, `game/Logic/AssetManifest.cs:140` | **defer** to the extraction tool epic (A1) | V1 |
| R3 | A lone surrogate escape in an entry's `id`, `kind` or `path` makes `GetString()` throw `InvalidOperationException`. `Parse` catches only `JsonException`, so the exception leaves `AssetFolder.Open()` and the board says "Asset folder: (not found)" for a folder that was found. Reproduced by the reviewer. `GlbCheck` already handles this case (`GlbCheck.cs:138`, tests `LoneSurrogate*`); the pattern was not carried back to the manifest reader. `--check` on the PC rejects such an ID. | `game/Logic/AssetManifest.cs:137-145, 348-352` (1.4) | **fix now** (A2) | V1 |
| R4 | `push-assets.sh` sends symbolic links as links (`tar` without `-h`), though `write_manifest.py` accepts linked files on purpose. `adb exec-in` probably does not return the remote `tar` status, and the only check afterwards (`test -f manifest.json`) passes on a manifest left from an earlier push. Plausible, not run. | `tools/push-assets.sh` copy step | **defer**, unverified; settle by pushing a folder holding a link, and one where `tar` fails on the headset (A3) | none |
| R5 | A refresh-rate change to a new target starts a new `FrameStats`, so a Guardian prompt (90 to 72 to 90) wipes the readout's totals. The match on `double` is exact. | `game/FrameRateReadout.cs:63`; `tools/README.md:147` on Guardian | **accept**: a debug aid; totals restart with a `status=` log line | none |
| R6 | If the session is already focused in `_Ready`, the second rate request never happens. `OnRefreshRateChanged` ignores changes when 90 was not offered. | `game/Main.cs` `RequestRefreshRateAgain`, `OnRefreshRateChanged` | **accept**: the headset path is proven at 90 Hz (1.10, 1.8) | none |
| R7 | The board lists invalid or duplicate entries for IDs the game never asks for, but not a wrong kind on an unused ID. | `game/Logic/LaunchReport.cs:123` | **accept**, recorded so later retros do not re-flag it; it matches `write_manifest.py --check` | none |
| R8 | Small seams, all **accepted**: <br>• "no node uses any of its meshes" counts any node with a mesh (`GlbCheck.cs:222`); `FindMeshes` after the build catches the case anyway. <br>• Images are not re-checked on the bytes loaded, unlike models (`AssetFolder.cs:150-164`); the window is narrow. <br>• `manifest.py` lists loose atlas PNGs named like IDs, and one badly named folder fails the whole write (`tools/manifest.py:177-184`). | as listed | **accept**; the atlas case comes back with the extraction tool epic | none |

### Verification gaps

| # | Finding | Source | Disposition |
|---|---------|--------|-------------|
| V1 | The two rule sets have no shared fixtures. `AssetManifest.cs:51` says the Python and C# rules "must agree", but each suite uses its own hand-written cases, which is how R1 to R3 got through. The output of `make_test_model.py` is never run through the C# `GlbCheck` (it passes today; drift would go unseen). | `tests/tools/test_make_test_model.py:33`; no reference to `make_test_model` in `tests/GameLogic.Tests` | **fix now** as a process change (A1) |
| V2 | The OpenXR session handling in `Main.cs`, and the readout's pause and resume, are checked only on the headset; `RefreshRateTests` covers `Choose` and `Confirm` and nothing else. | `tests/GameLogic.Tests/RefreshRateTests.cs` | **accept**: these need engine signals; the headset checks of 1.3, 1.10 and 1.8 cover them |

Checked and clean by the reviewer: Python and C# agree on the ID and path rules, the PNG header rule, BOM and strict UTF-8, integer-only `format` and `bytes`, and containment through symbolic links. The 1.8 refactor keeps the supplied and placeholder bookkeeping and `Model()`'s exception path.

### Aggregate views

- **A-arch: architecture delta.** `game/Logic/` holds no Godot reference (`AssetManifest.cs:2-8`, `GlbCheck.cs:2-5`), and the test project compiles it directly (`GameLogic.Tests.csproj:13`). The graph has no cycles: `LaunchReport` → `AssetManifest` → `GlbCheck`, while `FrameStats` and `RefreshRate` stand alone. One implicit contract: `tools/quest-adb.sh` needs the sourcing script's `die`. `setup-toolchain.sh:321` calls `adb` directly. **Accept.**
- **A-dup: duplication map.**
  - The rules mirrored between `tools/manifest.py` and `AssetManifest.cs` are a recorded design choice (1.4 Design Notes). But the entry-level wording differs in about six messages, although the 1.4 notes say "the same ID and path rules and wording". For example, Python says "wrong size" where C# says "incomplete or changed", and "wrong contents for kind 'image'" where C# says "unreadable image". **Defer**, folded into A1.
  - Five PNG writers (`tools/png.py`, `make_test_model.atlas_png`, and three test helpers) and the glTF constants in four places. **Accept** for test code.
  - The package name appears in `quest-adb.sh:5`, `AssetFolder.cs:26` and `export_presets.cfg:31`. **Accept.**
- **A-size: size growth.**
  - `game/Logic/AssetManifest.cs` is 548 lines doing six jobs: load and parse, `Find` with its file checks, the ID, path and PNG rules, entry parsing and its own `RealPath`. It is the file the extraction tool epic will grow. **Defer**: split it when it next grows (A6).
  - `AssetFolder.cs` (393) and `Main.cs` (302, with an inline shader) are **accepted**.
- **A-pattern: pattern divergence.** **Accept**; noted as conventions to set in A6.
  - The epic's Python uses `%` formatting and the `tool: error:` prefix on stderr; the older tools use f-strings and stdout.
  - `make_test_model.py` alone uses argparse, so a usage error exits 2 rather than 1.
  - Two warnings lack the `PDZ-` tag and end in a full stop (`Main.cs:72`, `Main.cs:262`).

### Spec against the build

- **S1: plan bookkeeping.** Ticket 1.1 has two plans:
  - the toolchain plan in the epic folder, titled differently from the ticket;
  - `plan-1-1-csharp-vr-scene-on-headset.md` at the initiative's top level, with no `ticket` field and `status: 'built'`, never moved to done. `tickets.py` cannot see it.

  **Fix now** (A4).
- **S2: stale statements.** **Fix now** (A5).
  - `stack.md:8` names `Image.load_from_file`; the code uses `LoadPngFromBuffer` (`AssetFolder.cs:164`).
  - `stack.md:9` says model loading is not yet run on the headset; 1.7 ran it on 2026-10-15.
  - `stack.md`'s toolchain section omits the vendor plugin and Gradle, and its extraction table omits `manifest.py`, `write_manifest.py` and `make_test_model.py`.
  - The asset contract says no part of the Models section has run in Godot (`asset-contract.md:121`); 1.7 ran it. It also says "Story 1.4 settles that" (`:224`), which 1.4 did by settling on the fixed path.
  - The epic file says "eight stories" (`epic-engine-spike.md:42`), and lists the C# question as unknown (`:41`).
- **S3: deferred-work entries resolved but never closed.** **Fix now** (A5).
  - The rest of ticket 1.1 (`deferred-work.md:177`).
  - The epic's CAP-2 wording (`:181`): already reworded at `epic-engine-spike.md:25`.
  - No log output reaching logcat (`:186`): plan 1.3 of 2026-10-09 says "The `PDZ-FPS` tag works".
  - The readout covering the board (`:201`): fixed in `0146440`.
- **S4: places where the asset contract and the build disagree.**
  - The contract says the report "lists every problem" (`asset-contract.md:197`). The board shows 6 and a count, as ticket 1.5's frozen intent asked; the full list goes to the log.
  - The contract has no "unreadable image" row, and the game reports one.
  - "Empty model" is wider in the code: no node uses a mesh, or a mesh has no surfaces.
  - The code rejects empty path parts such as `a//b`; the contract does not mention them.
  - The contract allows an empty `assets` list (`:76`), which `manifest.py:220` treats as an error.
  - The contract says an empty folder "says so" (`:11`), but `assets: []` shows no board.

  Each is **proposed** as a reconciliation of the contract (A5); the code is the better account in every case except the last two, which are for you to decide.
- **S5: accepted deviations, recorded so later retros stop raising them.**
  - Done when 5's agreement is recorded in plans 1.2 and 1.6, not in the contract itself.
  - Ticket 1.9 says "IDs given on the command line"; as built, the ID is the file name, with `ID=path` to override (1.9's frozen intent).
  - Plan 1.3 is done with its 90 Hz criterion renegotiated twice; ticket 1.10 met it.
- **S6: inaccurate record.**
  - Plan 1.8's headset note says 1.10 "had already shown a minute at 90". Plan 1.10's figures cover about 2,600 frames, about 29 seconds.
  - Plan 1.10's dated entries say 2026-10-17, but its commits are dated 2026-10-16.

  **Accept**: Done when 1 has no duration, and both readings are well above 89 Hz. If ticket 1.3's "one minute" matters to you, it needs one more run.

### Process lessons (plans, dev diaries; session logs scanned narrowly)

- **P1:** a system prompt on the headset stopped the headset checks again and again: Guardian or "Finding position in room" in 1.3 (2026-10-09), 1.4, 1.5, 1.7 (no headset) and 1.8. The `adb` self-service route (proximity sensor held, `screencap`) works only once you have cleared the prompt. Marking every headset ticket `hitl` was right. **Lesson:** plan a headset check as a short session with you present, and batch the checks.
- **P2:** the 72 Hz cause took three stories' worth of evidence (1.3 renegotiated twice, then 1.10), and it was a headset setting that needed a restart. 1.10's ordered approach (read the system, then other apps, then settings) found it. **Lesson:** when the platform misbehaves, check device state and other apps first.
- **P3:** each ticket ran one quick review lens, which could not see rules split across two languages and two tickets; R1 to R3 come from that. **Lesson:** see V1 and A1.

## Behavior verification

Run end to end on the Quest 3 on 2026-10-16, in ticket 1.8 after the refactor (plan 1.8, "Headset checks, 2026-10-16"), with you wearing the headset:

- **Done when 1:** `PDZ-FPS` read `target=90` and averages of 90.0 after the first second, with slow frames under 1% within about ten seconds. Each run was read for about 15 seconds.
- **Done when 2:** two generated images and the test model, pushed with `push-assets.sh`, all loaded; the striped floor was seen in a capture.
- **Done when 3:** with the sky image deleted on the headset, the board read `e03.canopy: images/e03.canopy.png: missing`, the sky showed its placeholder and the floor its image.
- **Done when 4:** after `push-assets.sh --clear`, the board read "Running on placeholders" over the green ground, the blue sky and the grey cube.
- **Done when 5:** on paper. The contract exists, and the agreement is recorded in plans 1.2 and 1.6.

Earlier headset evidence: 1.4 (own images, 2026-10-14), 1.5 (missing file and truncated file, 2026-10-14), 1.7 (model in, model removed, 2026-10-15) and 1.10 (90 Hz, about 29 seconds).

Not exercised: the "loading throws" path ran only on the desktop, with an injected exception (plan 1.8). Neither the truncated-file board nor R3's surrogate case was seen on the headset.

## Previous-retro follow-through

There was nothing to follow through on: there is no previous retrospective file, since this is the initiative's first epic.

## Action items

All are proposed, not applied. You decide what runs.

| # | Action | Kind | Owner | From |
|---|--------|------|-------|------|
| A1 | Give the PC tools and the game one set of shared fixtures (manifests, PNGs, `.glb` files, with the expected result for each) that both test suites run. Make `manifest.py` apply the contract's invalid-model table and reject `NaN`, `Infinity` and deep nesting. Run the `make_test_model.py` output through the C# `GlbCheck`, and line up the entry-level wording. Best placed as the extraction tool epic's first story, before it writes real manifests. | remediation, process | you, with `bmad-ticket` for the extraction tool epic | R1, R2, V1, A-dup, P3 |
| A2 | Catch `InvalidOperationException` from `GetString()` in `AssetManifest.Parse`, as `GlbCheck` does, so a lone surrogate gives an invalid entry rather than "folder not found". Add a test. A one-ticket bug. | remediation | you, with `bmad-build` | R3 |
| A3 | Try `push-assets.sh` with a symbolic link and with a failing remote `tar`. Then follow links (`tar -h`) and check the remote exit status if either fails. | verification | you, at the next headset session | R4 |
| A4 | Settle ticket 1.1's second plan: mark `plan-1-1-csharp-vr-scene-on-headset.md` done and give it `ticket: '1'`, or fold it into the epic folder's 1.1 plan. | bookkeeping | you | S1 |
| A5 | Reconcile the documents:<br>• update `stack.md` and the epic file's Notes;<br>• update the asset contract's lines 121 and 224 and the S4 rows (decide the `assets: []` cases);<br>• append closing entries to `deferred-work.md` for the four resolved items in S3. | spec reconciliation | you | S2, S3, S4 |
| A6 | Before the extraction tool epic grows `AssetManifest.cs`, split it: parse and read, the rules, the file checks. Write down the epic's Python and logging conventions: the error prefix, exit codes, the `PDZ-` tag, no closing full stop. | remediation, convention | you, as part of the extraction tool epic | A-size, A-pattern |
| A7 | Plan the move off .NET 9 before its support ends on 2026-11-10 (`tools/README.md:89`). Godot plans .NET 10 templates for 4.8. | risk | you | carried in `tools/README.md` |
| A8 | Carry these into later epics:<br>• the asset contract's 13 provisional Models rules → the dragon epic;<br>• a battery-saving line in the player instructions (`deferred-work.md:212`);<br>• the `AndroidRuntime` lookup that returns nothing on the Quest → the extraction tool epic;<br>• the unexplained repeat-clamping on the Quest (`tools/README.md:119`). | carry-forward | you, with `bmad-ticket` | inherited |

## Acceptance verdict

**Machine verdict: accepted-with-open-items.** The criteria were declared, in `epic-engine-spike.md`'s Done when.

- All ten tickets are done, and `pending_tickets` is empty.
- Done when 1 to 4 were seen on the headset on 2026-10-16 (Behavior verification), and Done when 5 is met on paper.
- No open finding blocks: R3 is a crash-free mis-report on a malformed manifest, and R1 and R2 matter once a tool writes real manifests, which is the next epic.
- Open items stay tracked in A1 to A8.

Your decision overrides this verdict. The epic file's `status` is still `in-progress`; closing it is `bmad-ticket`'s job.

## Open questions

1. Should an empty `assets` list be an error on the PC (as `manifest.py` has it) or allowed (as the contract has it)? And should the board show a notice for one, as it does for a missing manifest? (S4)
2. Is the about-29-second run at 90 Hz enough for ticket 1.3's "one minute", or do you want a full minute recorded? (S6)
3. Should A2 be fixed now, before the extraction tool epic starts, or wait for A1?
