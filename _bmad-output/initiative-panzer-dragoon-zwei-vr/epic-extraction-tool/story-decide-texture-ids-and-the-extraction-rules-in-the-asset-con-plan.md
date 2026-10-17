---
title: 'Decide texture IDs and the extraction rules in the asset contract'
type: 'chore'
ticket: '1'
created: '2026-10-17'
status: 'built'
baseline_revision: '42986593ae581b0cba109779229bdee4f145e758'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extraction epic cannot start its tracer bullet. The asset contract gives extracted textures no IDs, and says nothing about where the disc image goes, what development and release modes do, what stops a second extraction, or how extraction progress and failure are reported. It also still allows an empty asset list, which the author has made an error (epic Notes, 2026-10-16).

**Approach:** Add these rules to `asset-contract.md`, with a worked example manifest listing extracted textures, so the PC command and the headset write identical files. The author decides the open points and agrees the document. Spec and stack notes are touched only where they contradict the new rules.

**Decisions (author, 2026-10-17):**
- An extracted texture's ID is a sequence number within its file, `e03.tex.<file>.<n>`, chosen over IDs built from the disc record, knowing that a change to how records are found renumbers them.
- Development mode is set by a marker file named `development` in the asset folder; without it the game is in release mode, whatever the build.
- In release mode, extraction is skipped when `manifest.json` was written by the extractor from a disc with the same product and version, and a leftover disc image is then deleted. No separate marker.
- Extraction writes to a staging folder and replaces the previous extraction only on success; on failure the folder keeps what it held.

</frozen-after-approval>

## Implementation Notes

Oneshot: one document and small spec edits, no code.

Facts the proposals rest on (`tools/cgb_textures.py`, `zwei-disc-formats.md`):
- A texture is drawn by a `.MDB` record: CMDPMOD (colour mode), CMDCOLR (lookup table or colour bank), CMDSRCA (pixel address / 8), CMDSIZE (width, height). The same pixels can be drawn with a different table, mode or size, and come out as a different image. The prototype de-duplicates on (SRCA, width, height, mode, COLR for mode 1) but names files by pixel offset, width, height and mode only, so two tables over the same pixels would collide.
- The `.CGB` load address is inferred, not stored; an offset into the `.CGB` depends on that inference, while SRCA and COLR are read straight from the disc.
- Episode 3 has 420 textures by the prototype's log, in nine `.MDB`/`.CGB` pairs (`E03CMN`, `E03B0N0`–`N2`, `E03B1N0`–`N2`, `E03BOSS`, `E03END`).

Proposals the author is asked to agree as drafted (beyond the open questions):
- Paths: `images/e03/<file>/<id's last level>.png`, lowercase.
- `source`: product and version from the disc's Saturn header, whatever the disc; another disc is attempted, not refused.
- `generator`: `pdz-extract <library version>`, the same string on the PC and the headset.
- Identical output: entries sorted by ID (ordinal); `sha256` on every entry; fixed JSON layout (two-space indent, `\n` line ends, final newline, keys in the order of the example); PNGs written by one deterministic encoder.
- The disc image lives in `disc/` inside the asset folder: one `.cue` with its `.bin` files, or one `.iso`. More than one image is reported and nothing is extracted.
- Progress: a board in the world shows extraction progress by file; the scene runs meanwhile on what the folder already holds.
- An empty `assets` list is a problem: the manifest cannot be read, placeholders for everything.

Done:
- `asset-contract.md`: the folder tree gains `disc/` and the `development` marker; `assets` must list at least one entry; a new "Extracted textures" subsection under IDs; a new Extraction section (disc image, modes, what extraction writes, a second worked example, progress and failure); a launch row for an empty list and a table of extraction rows.
- Spec: CAP-3 says release mode rather than release builds, the development-mode constraint names the marker, and the `disc/` assumption is now fixed.
- Both example manifests parse as JSON, and their IDs and paths pass `tools/manifest.py`'s ID and path rules.

## Review Triage Log

Quick lens, 2026-10-17: high 0, medium 1, low 7, false 0, maybe-false 0. Eight patched, two carried.

- medium, patched: the de-duplication key left out the CMDPMOD transparency bit and the mode 0/4 colour bank, so two records drawing different images could share a number. It is now all four record words, so the numbering is fixed exactly before anything relies on it.
- low, patched: the `generator` and `source` rows said the game does not act on them, against the release-mode rule. Both rows now name that one use.
- low, patched: the folder rules said the game assumes no file name or subfolder, and "on first launch". They now name `disc/` and `development` as the only fixed names, and point to Extraction.
- low, patched: the example manifest broke its own layout rule with a one-line `source`. The rule now says one member per line, and the example matches `json.dumps(indent=2)` exactly (checked).
- low, patched: the staging folder had no name and no swap order. It is now `extracting/`, emptied first, with files moved into place before the manifest, and a leftover folder emptied by the next run.
- low, patched: the generator test `starts pdz-extract` matched other names. It is now `pdz-extract ` with the space.
- low, patched: the spec still said release mode never extracts again. It now says not again for the same disc.
- low, patched: `stack.md` said "at first launch". It now says at launch, pointing to the contract.
- carried: no code enforces an empty asset list as a problem yet, and some C# tests use `"assets": []` as readable. That is entry 7's work (game check and shared case), as planned.
- carried: the author's agreement is pending; it is asked for at presentation.

## Verification

**Manual checks:**
- The contract's new sections exist, its example manifest parses as JSON and passes the asset-rule checks, and the author has agreed them.
