---
title: 'Split the manifest reader and write down the code conventions'
type: 'refactor'
ticket: '3'
created: '2026-10-16'
status: 'built'
baseline_revision: 'f3022fd1b82e767b83304aeba2649e8ee5cfee70'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `game/Logic/AssetManifest.cs` is about 580 lines doing six jobs (loading and parsing, entry reading, the ID, path and PNG rules, the per-ID file checks, folder containment through links, JSON helpers), and the extraction tool epic will grow it. The conventions the engine spike settled (error lines and exit codes of the PC tools, game log tags) exist only in the code and the retrospective, where the next agent will not look (retrospective A-size, A-pattern, A6).

**Approach:** Split the class by job with no change in behaviour, guarded by the shared asset-rule cases from ticket 2, and write the conventions down where agents read them.

## Boundaries & Constraints

**Always:**
- No change in behaviour: every test passes without edits to its assertions, and the game's log and launch report lines are unchanged.
- `game/Logic/` stays engine-free and compiled straight into the C# tests.
- The conventions describe what the code does today. Code that breaks one is listed as a known exception, not changed here.

**Never:**
- No change to public names other code or tests use.
- No change to `tools/manifest.py`, `GlbCheck`, `LaunchReport` or the game layer.
- No new rules.

**Decision (author, 2026-10-16):** the conventions go in a new `AGENTS.md` at the repository root, as a short hand-written Conventions section; `bmad-project-context` can adopt it later.

</frozen-after-approval>

## Code Map

- `game/Logic/AssetManifest.cs` (578 lines) -- the jobs, by line:
  - types `ManifestStatus`, `AssetLookupStatus`, `AssetLookup` (13-46), `ManifestRead` (573-578);
  - load and parse (`Load` 97, `Parse` 119, `CheckTopField` 455, `OneLine` 480, `IsInteger` 487);
  - entry reading (`ReadEntries` 329, `EntryProblem` 390, `RequireField` 467, `Text` 435, `KindText` 453);
  - the rules (`IdProblem` 262, `PathProblem` 266, `PngProblem` 303, with `IdRule`, `PathChars`, `PngSignature`);
  - the file checks (`Find` 185, `LeadsInside` 495, `RealPath` 514).
- Only `game/Logic/` and the tests use these names; `IdProblem`, `PathProblem` and `PngProblem` have no caller outside the file. `partial class AssetManifest` across files keeps every name as it is.
- Guards: `tests/GameLogic.Tests/AssetManifestTests.cs`, `AssetRulesTests.cs` (the 42 shared cases), `LaunchReportTests.cs`, `GlbCheckTests.cs`; `tools/test.sh` runs them.
- Conventions as the code has them:
  - PC tools print `<tool>: error: <line>` to stderr, one problem per line, and exit 0 or 1; `-h` prints usage and exits 0. Exceptions: `make_test_model.py` uses argparse, so a usage error exits 2; `build-quest.sh` exits 2 when installed but not launched; `setup-toolchain.sh` exits 130 or 143 when interrupted.
  - Shell scripts define `die` and `say` with the script's name as prefix; `quest-adb.sh` needs the sourcing script's `die`.
  - Game log lines start `PDZ-ASSETS` or `PDZ-FPS`, are one line, and end without a full stop. Exceptions: `Main.cs` warnings at lines 72 and 262 have no tag and end in a full stop.
  - Epic-era Python uses `%` formatting; the older extraction scripts use f-strings.
  - Tests: Python `unittest` with `test_snake_case` names; C# xUnit with `Subject_Behaviour` names.
  - Rules mirrored in Python and C# must agree; a new rule gets a shared case in `tests/fixtures/asset-rules/` (ticket 2).
- There is no `AGENTS.md` or `CLAUDE.md` in the repository. The `bmad-project-context` skill manages an `AGENTS.md` block.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/AssetManifest.cs`, `game/Logic/AssetManifest.Rules.cs`, `game/Logic/AssetManifest.Files.cs` -- split `AssetManifest` by job as a partial class: reading and parsing stay in `AssetManifest.cs`, the ID, path and PNG rules move to `.Rules.cs`, and `Find` with the containment checks moves to `.Files.cs`. Each file's summary says what it holds.
- [x] `AGENTS.md` (new) -- the conventions above, the known exceptions, and the rule that mirrored rules get a shared case.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every test passes and no test file changed.
- Given a desktop run with `PDZ_ASSETS` at a folder with a missing file and another with own files, when the `PDZ-ASSETS` lines are compared with the same runs at the baseline, then they are identical.
- Given the split, when the three files are read, then no file holds more than one of the jobs listed in the Code Map, and none is over 300 lines.

## Implementation Notes

- A fourth file, `game/Logic/AssetManifest.Entries.cs`, holds entry reading (`Entry`, `ReadEntries`, `EntryProblem`, `RequireField`, `Text`, `KindText`). With entry reading left in `AssetManifest.cs` that file came to about 345 lines and held two Code Map jobs, failing the 300-line and one-job criteria; the task's three-file list could not meet both. See the Plan Change Log.
- `AssetLookupStatus` and `AssetLookup` moved with `Find` to `.Files.cs`; `ManifestStatus` and `ManifestRead` stay with `Load` and `Parse`. `IsInteger` and `OneLine` stay in `AssetManifest.cs` as the shared JSON helpers (`IsInteger` is used by both the top-level and the entry checks).
- The split was made by copying line ranges with a script; the sorted non-blank lines of the old file and the four new files differ only by the added `partial`, usings, namespace lines, braces and file comments.
- Godot's editor (`--headless --editor --quit`) generated `.cs.uid` files for the three new files, as the other `game/Logic` files have.
- `AGENTS.md` also lists, as a known exception, that the older extraction scripts print usage and exit 1 with no `error:` line, and notes the shell scripts take no `-h`; both are what the code does today.

## Plan Change Log

- 2026-10-16: added `game/Logic/AssetManifest.Entries.cs` as a fourth file for entry reading, so that every file holds one job and stays under 300 lines (acceptance criteria 3). Intent and boundaries unchanged.

## Review Triage Log

Quick lens, 2026-10-16: high 0, medium 0, low 6, false 0, maybe-false 0. Six patched. Every finding was in `AGENTS.md`; the split itself checked clean (a faithful move, one job per file, at most 194 lines, no test or public-name change).

- low, patched: the warning in `FrameRateReadout.cs` when Android's log library cannot be called breaks the log-line rule (tag mid-line, closing full stop). It is now listed as an exception.
- low, patched: `test.sh` has no `die`/`say`, passes extra arguments to `dotnet test` and exits with its status. Now listed, and `toolchain-env.sh` is named as a sourced file.
- low, patched: `write_manifest.py` prints only the usage, with no `error:` line, on no arguments or an unknown option. Now listed.
- low, patched: the older extraction scripts print the usage to stdout and exit 1 on `-h` too. The `-h` rule is now limited to epic-era Python, and the exception says stdout.
- low, patched: two C# test names are a single phrase rather than `Subject_Behaviour`. Now noted.
- low, patched: `make_test_model.py` is now named among the epic-era Python files that use `%`.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all tests pass.
- `git diff --stat <baseline> -- tests` -- expected: empty.

**Results (2026-10-16):**
- `tools/test.sh`: Python 94 tests OK; C# 192 passed, 0 failed.
- `git diff --stat f3022fd -- tests`: empty.
- `dotnet build` of `game/`: 0 warnings, 0 errors.
- Desktop, `godot --headless --xr-mode off --quit-after 120` with `PDZ_ASSETS` at three folders (own files: two PNGs and the test model; one file missing; a non-PNG image plus a symbolic link leaving the folder): the `PDZ-ASSETS` lines (5, 9 and 10) are identical to the same runs at the baseline.
- File sizes: `AssetManifest.cs` 189, `.Entries.cs` 152, `.Files.cs` 194, `.Rules.cs` 83 lines.
