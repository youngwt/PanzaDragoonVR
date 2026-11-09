---
title: "Where the dragon's wing animation comes from"
type: 'chore'
ticket: '18'
created: '2026-11-09'
status: 'built'
baseline_revision: 'c9265a947ed592e6673699b2302fdbffc4b83964'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The ridden dragon holds a still pose. Nothing yet says where its wing motion is held on the disc: in the unexamined `.MTB` beside each form's `.MDB` and `.CGB`, or somewhere else. So nobody can say whether the dragon can flap its wings, or at what cost.

**Approach:** Work it out from the files and, where needed, from the game's code in Ghidra and the Episode 3 save state in Mednafen, as spike 4.2 did for the geometry. Record the answer in `zwei-disc-formats.md`, with each point marked confirmed or lead and its evidence, or with what was ruled out. Then the author decides whether to animate the wings.

## Boundaries & Constraints

**Always:**
- Answer:
  - what an `.MTB` holds and its layout;
  - how it drives the 27 body nodes (`zwei-disc-formats.md`, Models: legs, wings and tail turn on their own in the save state);
  - which motions a form has (wing beats, other poses) and how many frames each has, at what rate;
  - whether form 9, `DRA09I`, the demo's form, flaps with what its own files hold.
- Throwaway scripts, their output and any drawings go only under `extracted/` and are never committed. Simple values such as offsets and sizes may go in the notes (`AGENTS.md`, author 2026-11-09). The disc's motion data itself is not copied into git as tables.
- At the end, give the author the shape of a follow-up story, if the answer allows one, and the author decides whether to animate.
- Decision (author, 2026-11-09): the time box is two sessions. At the limit, the best answer so far is recorded and the author decides whether to go on.
- Decision (author, 2026-11-09): find and prove: find the layout and where the motion comes from, and draw form 9 on the PC in a raised-wing and a lowered-wing pose; no whole animated sequence.

**Never:** No change to `lib/PdzDecoding/`, to the game or to any asset ID; no change to the `.glb` writer or the contract's "no animations" rule (story work, if any); no confirming beyond what a check could have disproved.

</frozen-after-approval>

## Code Map

- `extracted/disc/` -- the author's disc files, already extracted: `DRA01A`..`DRA10J` `.MDB`, `.CGB`, `.MTB`, and the `DRA*E0n.GRB` shading tables.
- `zwei-disc-formats.md`, Models -- the `.MDB` tree (27 body nodes, depth-first order and body parts by eye), matrices (3x3 16.16 plus whole-unit translation), the live matrices in the save state's work RAM, and the one line known about `.MTB` (its size is not a whole number of 27 x 12 bytes). Not known: lists `.MTB` as backlog 18.
- `extracted/spike-mdb/` -- spike 4.2's scripts: `draw2.py` (draws a form from its tree, the working reference), `check.py`, `hd.py`, and the Ghidra logs. Reuse; do not commit.
- `lib/PdzDecoding/Dragons.cs` -- `Walk`, `ReadMatrix`, `Multiply`: the decoder's own tree walk and matrix product, read only, to name nodes the same way.
- `tools/mednafen_state.py`, `tools/saturn_disc.py` -- save-state and disc readers. `extracted/ghidra/zwei.gpr` and `tools/mednafen/` -- the Ghidra project and the emulator.

## Tasks & Acceptance

**Execution:**
- [x] `extracted/` -- find the `.MTB` layout and how it drives the nodes, with throwaway scripts; if it allows, draw form 9 in two or more poses of a wing beat.
- [x] `zwei-disc-formats.md` -- record the answers in Models (or a new Motion section), each point confirmed or lead with evidence; update the file table's `.MTB` line and Not known.
- [x] `spec-panzer-dragoon-zwei-vr.md` -- answer the open question "Where does the dragon's wing animation come from?" (about `:102`); `epic-riding-the-dragon.md`'s open question is left as history.

**Acceptance Criteria:**
- Given `zwei-disc-formats.md`, when read, then it says what the `.MTB` files hold and where the wing motion comes from, each point marked confirmed or lead, or lists what was ruled out.
- Given the notes, when a reader follows them, then form 9 can be drawn in a raised-wing and a lowered-wing pose, or the notes say why not.
- Given the finished spike, when the author is asked, then whether to animate the wings is recorded as a dated decision in this plan.
- Given the staged files, when listed before the commit, then they are notes and plan files only.

## Implementation Notes

- Done in one session. Scripts and drawings are in `extracted/spike-mtb/` (git-ignored): `mtb.py` (file layout), `slots.py` (1ST_READ's slot table), `match.py` (fit against the state's live matrices), `drawpose.py` (drawings), and the Ghidra decompiles `d1.txt` to `d5.txt`.
- The answer: each form's `.MTB` holds one pose per frame for 24 of the 27 body nodes (6 words a node: translation / 16, then Euler angles at 4,096 a turn, matrix `Ry * Rx * Rz`). The motions are listed in 1ST_READ (12 slots a form). Slot 0 is the wing beat in all ten forms; `DRA09I`'s has 54 frames, and the save state's live matrices are its frame 25 to within 4e-5 (confirmed by fit and by code). Frames are shown whole, with no blending. Forms A, B, C, D, F and I also swap in 15 wing shapes while raising or lowering their wings (the 30 extra roots of Models). See `zwei-disc-formats.md`, Motion.
- Drawn: `extracted/spike-mtb/out/DRA09I_beat_behind_sheet.png` and `DRA09I_beat_side_sheet.png`, the rest pose, then frames 9 (raised), 17 and 26 (lowered) of slot 0.
- Not found: how many updates a second the motion plays at (one frame an update at speed 1.0; the update rate is not traced). At 30 a second the beat takes 1.8 s, at 60 0.9 s.
- Follow-up story, if the author wants it ("The dragon beats its wings"): the extractor reads the form's slot 0 from 1ST_READ's slot table (found by the form-pointer table, as the seat table is) and the frames from its `.MTB`, and writes them into the form's `.glb` as a glTF animation of node rotations and translations (one key a frame, step interpolation), which changes the `.glb` writer and the asset contract's "no animations" rule. The game plays it in a loop at a tuning-set rate, until the rate is traced. Risks: the decoder's output must stay byte-for-byte the same on PC and headset (Euler to quaternion in fixed arithmetic or with care), and the rate is a guess. The wing shapes, the other slots and any blending are out of it.
- Decision (author, 2026-11-09): animate the wings, after looking at the drawings; the follow-up story is backlog 22, "The dragon beats its wings". The playback rate is left for now: the story uses a tuning setting, and the spike's second session is not spent.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 2 low patched; 1 medium settled by the author.

- medium, settled: the third criterion, the author's dated decision on animating the wings, was not met. The author was asked and decided on 2026-11-09, recorded in Implementation Notes.
- low, patch: the notes said the time box ended before the update rate was traced, though one of the two sessions remains. Reworded to "not traced in the spike's first session"; whether to spend the second session on it is the author's call.
- low, patch: "the others run from 19 to 41 frames" was wrong for `DRA07G`, whose slot 2 shares slot 0's run. Corrected.

## Verification

**Commands:**
- `git diff --cached --name-status` -- expected: notes and plan files only

**Manual checks (if no CLI):**
- The author looks at the drawn poses under `extracted/` and recognises a wing beat.

