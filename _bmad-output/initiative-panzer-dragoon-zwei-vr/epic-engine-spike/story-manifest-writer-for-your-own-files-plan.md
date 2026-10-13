---
title: 'Manifest writer for your own files'
type: 'feature'
ticket: '9'
created: '2026-10-13'
status: 'built'
baseline_revision: 'e0c8fba9cb2b5e9b164967b3fa09ef4d49c7811e'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The asset contract requires a `manifest.json` with an exact byte size for every file. Story 1.4 needs one for the author's own two images, nobody will type sizes by hand, and nothing yet writes or checks a manifest.

**Approach:** Add a small Python module that builds and validates manifests to the asset contract, and a command that writes `manifest.json` automatically from the files already in a folder: each file's name without its extension is its asset ID, so `images/e03.floor.png` becomes `e03.floor`. `ID=path` pairs on the command line remain for a file that is not named after its ID. A check mode validates an existing folder against its manifest, which is how "passes every rule in the contract" is shown. The extraction tool reuses the module later.

## Boundaries & Constraints

**Always:**
- Python standard library only, like the other scripts in `tools/`.
- The asset contract is the authority. Every rule the code enforces is one the contract states.
- On any error nothing is written: an existing `manifest.json` is left as it was.
- Each failure prints one line naming the ID or path and the rule broken, and exits non-zero.
- The manifest is written as UTF-8 without a byte-order mark, with `format` 1, `source` null and a `sha256` for every file.

**Never:**
- No copying to the headset and no `adb` (story 1.4).
- No game code and no change to the asset contract.
- No reading of the disc or of Saturn formats.
- No deep validation of file contents beyond what identifies the kind: a PNG's header, a `.glb`'s header.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Automatic | Folder holding `images/e03.floor.png` and `images/e03.canopy.png`; no pairs given | `manifest.json` at the folder's top level listing both, IDs taken from the file names, with exact `bytes` and `sha256` | No error expected |
| Explicit pair | `e03.floor=images/a.png` given as well | That file is listed under the given ID; the rest of the folder is still scanned | No error expected |
| Not an asset | A file whose name is not a valid ID, or whose extension is not a known kind, with no pair naming it | Left out of the manifest and named in a one-line notice; the run still succeeds | Not an error |
| Nothing found | Folder with no file that qualifies | Nothing written | One line saying no assets were found |
| Same ID twice | Two files whose names give the same ID, or a pair repeating a scanned ID | Nothing written | One line naming the ID and both paths |
| Run again | Same command, a manifest already there | Manifest replaced with the same content | No error expected |
| Bad ID in a pair | `E03.Floor=...` or `e03..floor=...` | Nothing written | One line naming the ID and the rule |
| Bad path | A pair's path that is absolute, contains `..` or `\`, or is outside the folder; or a scanned asset whose path breaks the contract's path rules, such as upper case in a folder name | Nothing written | One line naming the path and the rule |
| Missing or not a file | Path does not exist or is a folder | Nothing written | One line naming the path |
| Unknown kind in a pair | A pair naming a file that is neither `.png` nor `.glb` | Nothing written | One line naming the path and the kinds supported |
| Wrong contents | A `.png` that is not a PNG or is not 8-bit RGB or RGBA; a `.glb` without the glTF header | Nothing written | One line naming the path and what was found |
| Check, all good | `--check` on a folder whose files match its manifest | Reports the number of assets checked; exit 0 | No error expected |
| Check, problems | `--check` with a missing file, a wrong size, a wrong checksum, a duplicate ID, an unreadable manifest or an unsupported `format` | Every problem listed, not only the first; exit non-zero | Manifest and files untouched |

</frozen-after-approval>

## Code Map

- `tools/` -- Python scripts with a module docstring giving usage, `main(argv)` returning an exit status, and `sys.exit(main(sys.argv))`. Follow `tools/saturn_disc.py`. No packaging; scripts import siblings by adding their own folder to `sys.path` (see `tools/cgb_textures.py` importing `png`).
- `tools/png.py` -- a PNG writer only. Reading a PNG header (signature, then the `IHDR` chunk's bit depth and colour type) is new code.
- `tools/test.sh` -- runs `dotnet test` on `tests/GameLogic.Tests` and passes extra arguments through. There are no Python tests yet and `pytest` is not installed; use `unittest`.
- `tests/GameLogic.Tests/` -- the C# tests; leave alone.
- `tools/README.md` -- has a short "Extraction scripts" section at the end.
- Asset contract sections that bind this story: The folder, The manifest (top-level fields, asset entries, IDs, paths, kinds) and the first table under "What the game does at launch", whose problems the check mode mirrors. The Models section's content rules are out of scope here.
- `.gitignore` already ignores `__pycache__/`.

## Tasks & Acceptance

**Execution:**
- [x] `tools/manifest.py` -- the rules as code: ID and path validation, kind from extension, the header checks, `scan(folder)` finding assets by file name, `build(folder, pairs)` returning the manifest, and `check(folder)` returning a list of problems -- one owner of the contract's manifest rules on the PC.
- [x] `tools/write_manifest.py` -- the command: `write_manifest.py <folder> [ID=path ...]` scans the folder and writes the manifest through a temporary file so a failure cannot leave a partial one; `write_manifest.py --check <folder>` validates -- what the author runs.
- [x] `tests/tools/test_manifest.py` -- `unittest` cases, one or more per matrix row, working in temporary folders with small files generated in the test.
- [x] `tools/test.sh` -- run the Python tests as well as the C# ones, failing if either fails; with extra arguments, behave as now -- one command for all tests.
- [x] `tools/README.md` -- how to name files, write and check a manifest, with the two-image example.

**Acceptance Criteria:**
- Given a folder holding two PNG images named after their IDs, when the command is run with only the folder, then `--check` on that folder exits 0 and the manifest parses as JSON with the fields the contract requires.
- Given that manifest, when one image is then replaced by a file of a different size, then `--check` names that asset's ID and path and exits non-zero.
- Given `tools/test.sh`, when it runs with no arguments, then the Python and the C# tests all pass.
- Given the work is done, when `git status` is run, then no generated manifest, cache or temporary file appears.

## Implementation Notes

- `tools/manifest.py` holds the rules: `scan`, `build`, `check` and `check_report` (which also returns the count and takes an optional notices list). `tools/write_manifest.py` is the command. `tests/tools/test_manifest.py` has 65 `unittest` cases; `tools/test.sh` runs them before the C# tests when given no arguments.
- The file name without its extension is the ID. `manifest.json` is skipped silently in the scan; other non-assets get a one-line notice. A folder with no assets is an error.
- Write mode reports every problem, not only the first. Extensions are case-sensitive. A `.glb` must have the `glTF` magic and version 2.
- Check mode treats a missing `manifest.json` as a problem (the game treats it as a notice) and an unknown kind as a notice, as the contract does.
- The manifest is written through a temporary file in the folder and given an ordinary file's mode. A killed run can leave a `.manifest-*.tmp` file, which the next scan skips with a notice.
- Generator string: `write_manifest.py 0.1`.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens), 2026-10-13: high 0, medium 2, low 8, false 0, maybe-false 0. All ten patched, each with a test; none rejected.

- medium, patched: check mode failed on an entry of an unknown kind, which the contract does not treat as a problem. Now a notice; the file is still checked.
- medium, patched: `manifest.json` was written mode 0600 whatever the umask. Now an ordinary file's mode.
- low, patched: a subfolder that could not be listed was dropped silently. Now an error naming it.
- low, patched: a duplicate ID went unreported when one entry lacked a required field.
- low, patched: a failed replace named the temporary file. Now names the manifest.
- low, patched: a pair through a symlinked folder listed one file twice. Now keyed on the real path.
- low, patched: a deeply nested manifest raised `RecursionError`. Now reported as unreadable JSON.
- low, patched: a pair with an empty side gave a line naming nothing. Now echoes the argument.
- low, patched: `-h` and `--help` exited 1 on standard error. Now print usage and exit 0.
- low, patched: one test created a file outside its temporary folder. Now nested inside it.

Matrix rows are each covered by one or more passing tests; the two-image run, the changed-file check and the bad-ID and bad-path refusals were also run by hand.

## Design Notes

- **A check mode as well as a writer.** The ticket asks that the manifest "passes every rule in the asset contract". A validator is the only way to show that, and the extraction tool and story 1.4 can both use it before anything is copied to the headset.
- **A module and a command.** The extraction tool epic must write the same manifest; keeping the rules in `manifest.py` means it imports them instead of repeating them.
- **Checksums always written.** The contract makes `sha256` optional so hand-written manifests are allowed; a tool has no reason to leave it out, and the check mode uses it.
- **The file name is the ID.** The contract leaves file names to the tool and lets a path contain dots, so `e03.floor.png` is a legal path and needs no separate naming table. `manifest.json` itself and anything else that is not an asset is skipped with a notice, so stray files do not break a run.
- **IDs are not restricted to the prefix table.** The contract says the table lists prefixes in use and grows; the tool enforces the ID pattern only.

## Verification

**Commands:**
- `tools/test.sh` -- expected: Python and C# tests pass.
- `python3 tools/write_manifest.py <tmp folder>` on a folder holding `images/e03.floor.png` and `images/e03.canopy.png`, then `python3 tools/write_manifest.py --check <tmp folder>` -- expected: exit 0, two assets checked.
- `git status --short` -- expected: only the files listed in Tasks.
