---
title: 'How .MDB geometry is laid out'
type: 'feature'
ticket: '2'
created: '2026-10-30'
status: 'built'
baseline_revision: '941672178efa01070dd89a05d47da1e134e55931'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `.MDB` geometry is undecoded, which blocks stories 4.5, 4.6 and 4.7 and later CAP-9. Only the texture records in these files are understood.

**Approach:** Work out the layout from the files and, if needed, the game's code in Mednafen and Ghidra. Draw one dragon form on the PC with a throwaway script. Record the answer in `zwei-disc-formats.md`, each point marked confirmed or lead with its evidence.

## Boundaries & Constraints

**Always:**
- Answer: vertices, polygons, each polygon's link to the decoded texture records, how `DRA01A`..`DRA10J` make up the ten forms, whether a form has separately moving parts, and the Saturn-to-metres scale.
- Decoded output and drawings go only under `extracted/`. Nothing from the disc is committed, and that includes the throwaway script's output.
- The author decides each form's asset ID under the `dragon.` prefix.
- Decision (author, 2026-10-30): also answer flip bits, drawing mode to material, flat colour, three-cornered polygons and per-vertex shading; the Saturn's axes and the dragon's front; the `DRA*` files compared on both discs; and whether one Episode 3 `.MDB` has the same layout.
- Decision (author, 2026-10-30): the seat comes from the rider. The game draws the rider as a model, so find that model and where it is placed on the dragon, and record that position as the lead for the seat.
- Decision (author, 2026-10-30): the time box is two sessions. At the limit, the best answer so far is recorded and the author decides whether to go on.

**Never:**
- No decoding in `lib/PdzDecoding/` (story 4.5), no change to the writer or to texture numbering, no confirming of provisional model rules (story 4.6), no wing animation from `.MTB` (backlog 18).

</frozen-after-approval>

## Code Map

- `extracted/disc/` -- the author's disc files, already extracted, including `DRA*` and `E03*` `.MDB` files.
- `lib/PdzDecoding/Textures.cs`, `FindRecords` -- scans for texture records at even offsets; do not change, as it fixes asset IDs. A record's offset is the way to link a polygon to its texture.
- `lib/PdzDecoding/Model.cs`, `ModelQuad` -- what story 4.5 must supply: four corners in the Saturn's order, texture or colour, drawing mode, flips.
- `tools/cgb_textures.py`, `tools/saturn_disc.py`, `tools/mednafen_state.py` -- reuse for textures, disc files and save states.
- `extracted/ghidra/zwei.gpr`, `tools/mednafen/` -- the existing Ghidra project and the emulator.
- Lead: `DRA01A.MDB` holds 16 big-endian longs in 0x0603xxxx, which look like pointers into work RAM, so a file may link its parts by load address. `E03B0N0.MDB` is 1,716 bytes against `DRA01A.MDB`'s 44,572, and holds none. Seen by a quick count only.

## Tasks & Acceptance

**Execution:**
- [x] `extracted/` -- find the layout with a throwaway script kept out of git -- the spike's question.
- [x] `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md` -- record the layout, each point confirmed or lead with evidence -- where the waiting stories read it.
- [x] `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md` -- update the open question on `.MDB` geometry -- it is answered.
- [x] `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-riding-the-dragon/epic-riding-the-dragon.md` -- record the author's asset IDs as a decision -- story 4.5 needs them.

**Acceptance Criteria:**
- Given the notes, when a reader follows them, then one dragon form's polygons are drawn on the PC under `extracted/` and the author recognises the dragon by eye.
- Given `zwei-disc-formats.md`, when read, then each point of the Always list is marked confirmed or lead with evidence.
- Given the finished spike, when the author is asked, then each form's asset ID under `dragon.` is recorded as a dated decision.
- Given the notes, when read, then they name the rider's model and its position on the dragon, or say what was tried and that it was not found, with the seat left for the author to decide.
- Given the staged files, when listed before the commit, then none holds disc data.

## Implementation Notes

- 2026-10-30, within the first session of the time box. The layout is in `zwei-disc-formats.md`, Models. Scripts and drawings are in `extracted/spike-mdb/` (git-ignored).
- Still needs the author: looking at `extracted/spike-mdb/out/forms_quarter_sheet.png` and `DRA01A_side.png`, each form's asset ID, and the seat. The epic's task stays open until then: the proposal there is only a proposal.
- The Code Map's lead on 0x0603xxxx pointers was wrong; ruled out in the notes.

Author, 2026-10-30: recognised the drawn forms by eye; chose `dragon.form01` to `dragon.form10`; chose the rider's position as the seat, read from the disc. Recorded in the epic's Notes.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 1 high, 5 medium, 2 low, 2 rejected; all kept findings patched, none deferred.

- high, patch: the rider's ten-row offset table was copied from `1ST_READ`, against AGENTS.md. Values removed, with other verbatim values found in the new section; offsets and shape kept.
- medium, patch: "wings spread flat" was false for `DRA06F` and `DRA07G`. Now says the pose differs by form.
- medium, patch: The rider still called the seat undecided, and no behaviour was stated for another disc. Decision recorded; the seat point is the rider's origin; another disc is story 5's to define.
- medium, patch: the contract still put `dragon.ride`'s origin on top of the back. Changed to where the game places its rider. Ticket 4's "place along the back" stays true, so it was left.
- medium, patch: layout statements had no confirmed or lead mark. Marked, from checks actually run.
- medium, patch: the epic's formats reference named only Textures. Models and The rider added.
- low, patch: `DRA02E06.GRB` is 160 bytes over twenty times its `E01`. Noted and listed under Not known.
- low, patch: spec and epic said the form choice waited on geometry or viewing. Now waits on the author, in story 7.
- rejected: the plan's Implementation Notes line on what the author still owes is out of date; its fix edits this plan, and the Author line below it records the outcome.
- false: criterion 5 could not be met while nothing was staged; the staged list was checked at this commit and holds notes and plan files only.

## Verification

**Commands:**
- `git diff --cached --name-status` -- expected: only notes and plan files.

**Manual checks (if no CLI):**
- The author looks at the drawing of the chosen form and recognises the dragon.
