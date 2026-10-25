---
title: 'Where the floor and canopy planes are on the disc'
type: 'chore'
ticket: '2'
created: '2026-10-25'
status: 'built'
baseline_revision: 'f55a17eb9efda2b656e59a22bc6de3b528bea158'
route: 'full'
route_source: 'auto'
risk: 'high'
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

**Problem:** Story 3.4 must extract the floor and canopy from the player's disc, but nobody knows where on the disc their cells, maps and colours are: today they are only in one Episode 3 save state's VDP2 memory. CAP-3 and CAP-4 cannot be met until this is known.

**Approach:** A spike. Trace the save state's plane data back to the disc: first by static means (byte searches, the code that writes VDP2 VRAM, trying likely decompressors on `EPISODE3.PRG`'s high-entropy region), then with Ghidra headless, and only if those fail with Mednafen's debugger run by the author. Prove each finding by a byte comparison that could have failed, write it into `zwei-disc-formats.md`, and bring the author the decisions story 3.4 needs.

## Boundaries & Constraints

**Always:**
- A finding is "confirmed" only when bytes produced from the disc equal the save state's bytes (cells, map, or colours); otherwise it is a "lead" with what it rests on, as the notes already mark things.
- Everything read from the disc or the state, and anything decoded, stays under `extracted/` (git-ignored). Only the author's own scripts and notes are committed.
- Work on GS-9049 V1.001 (`rom/JP/`); say where an offset would differ on the PAL disc, and check the PAL ISO too when a finding is confirmed.
- Time box: stop after three working sessions' worth of effort without a confirmed source for the cells, write up what was ruled out, and bring it to the author (the epic's Decision: the author decides then).

**Never:**
- No change to `lib/PdzDecoding`, the game, the asset contract or the manifest: decoding for real is story 3.4.
- No guessing presented as fact; no downloading other people's extracted assets.

</frozen-after-approval>

## Code Map

- `extracted/state_e03/VDP2_VRAM.bin`, `VDP2_CRAM.bin`, `WorkRAMH.bin`, `WorkRAML.bin` -- the save state unpacked by `tools/mednafen_state.py`, already in Saturn byte order. `rbg0_plane_A.png` / `rbg0_plane_B.png` are its renders of the two planes.
- `extracted/disc/` -- every file of the JP disc, unpacked by `tools/saturn_disc.py`; `EPISODE3.PRG` loads at 0x06080000.
- Found while planning (2026-10-25), to start from:
  - Plane A's map (VRAM 0x60000) is 1024 one-word pattern names 0x6002, 0x6004 ... 0x6200: 256 cells in order, a 16x16 grid repeated 2x2, so likely written by code, not stored. Plane B's map (0x60800) runs 0x6202 to 0x6378 with 188 distinct names and does not repeat 2x2.
  - The cells (VRAM about 0x41000 to 0x5BFFF) are found nowhere in the disc's files nor in work RAM as plain bytes, in 64-byte probes: they are stored packed. VRAM 0x20000 to 0x28FFF does match work RAM H at 0x0607A6EC to 0x0607B8AC (likely sprite data, not the planes).
  - `EPISODE3.PRG` holds the literal 0x25E40000 (VDP2 VRAM bank B0, where the cells are) at file offset 0x15E68 and 0x25E60000 (the maps) at 0x15E74: the code beside that literal pool is the likely upload routine. 0x25E60000 also appears at 0x52AD8 onward, in a table.
  - The file's entropy is about 7.7 bits a byte from 0x40000 to 0x53FFF (about 80 KB): the likely packed data.
- `tools/mednafen_state.py`, `tools/saturn_disc.py`, `tools/cgb_textures.py` -- existing Python helpers; follow their style for any new probe script (f-strings, usage to stdout as these older scripts do).
- Ghidra 12.1.4 headless: `~/.local/bin/ghidra-headless`, with the Saturn loader installed; `extracted/ghidra/` holds earlier projects. Mednafen: `tools/mednafen/run.sh`, GUI on the host via `flatpak-spawn --host`; BIOS in `~/Emulation/bios/Sega Saturn Bios (All Regions)/`.
- `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr.md` -- Open questions (~line 98) on where the floor and canopy data and palette are.

## Tasks & Acceptance

**Execution:**
- [x] Disassemble around `EPISODE3.PRG` 0x15E00 (load address 0x06095E00) with Ghidra headless; find the routine that writes 0x25E40000 / 0x25E60000, its source pointer and any decompress call, and the table at 0x52AD8.
- [x] Identify the packing (the routine's loop, or by trying LZSS/LZ77 variants and Sega's common Saturn formats on offsets in 0x40000 to 0x53FFF); write `tools/plane_probe.py` to unpack a stream and compare it with the state's VRAM.
- [x] Find the colours for codes 0x500 up (CRAM bytes 0xA00 up): the block at `EPISODE3.PRG` 0x52C44 and whatever loads it; compare with `VDP2_CRAM.bin`.
- [x] Settle how the maps are made (stored, or generated as the sequential names suggest) and plane B's layout.
- [x] (Not needed: static work found everything.) If static work stalls, write the author a step-by-step Mednafen debugger session (a write watch on VRAM 0x25E41000), run it with them, and record what it shows.
- [x] `zwei-disc-formats.md` -- the Floor and canopy section rewritten with each finding marked confirmed or lead with its evidence, what was ruled out, PAL differences, and the Next list updated; the spec's two Open questions updated.

**Acceptance Criteria:**
- Given the notes, when read, then the cells', maps' and colours' sources and layout are each named and marked confirmed (with the byte comparison) or lead, or the ruled-out paths are listed.
- Given `tools/plane_probe.py` on the JP disc (if the cells are found), when it runs, then its output equals the state's VRAM cell bytes and it writes images under `extracted/` only.
- Given the findings, when they are presented, then the author decides what story 3.4 extracts, including whether each plane image is the whole 512x512 page or its repeating pattern, and the decisions are recorded in the epic's Notes.

## Implementation Notes

- 2026-10-25, first session: all three sources confirmed by static work, so Mednafen's debugger was not needed. The set-up function at 0x06095CF0 copies the colours from 0x52C44 (0x060D2C44), calls 1ST_READ's LZ77 unpacker 0x0600FD1C on 0x3FDAC for the cells, and on the two maps (0x52608, 0x5287C) through the list at 0x52ACC. The table at 0x52AD8 is rotation parameter A's 16-plane map table (B's is at 0x52B18).
- `tools/plane_probe.py` on the JP track and on the PAL ISO: cells, both maps and both colour blocks (CRAM 0xC00 to 0xEFF) "identical" to the save state; its PNGs are byte-identical to `mednafen_state.py`'s renders. On PAL the data and tables are 0x1C lower but the code is 4 bytes later (set-up function 0x15CF4, pointer table 0x53328). Output went to `extracted/plane-probe-jp/` and `extracted/plane-probe-pal/`.
- `tests/tools/test_plane_probe.py` tests the unpacker on hand-built streams (no disc data).
- The maps are stored, not generated. Plane B's repeating unit is 256x512 (the whole page's height), not 256x256.
- Not done: the author's decisions for story 3.4 (third acceptance criterion) are still to be taken and recorded in the epic's Notes. Questions for the author: (1) each plane image as the whole 512x512 page, or its repeating unit (256x256 for the floor, 256x512 for the canopy)? (2) the VDP2 plane settings (cell size, colour count, pattern-name supplement) as constants in the library, or traced from the register lists at 0x52B58 onward first? Neither plane uses colour index 0, so transparency is not a question.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-25: high 0, medium 1, low 6, false 0, maybe-false 0. The reviewer re-ran the probe on both discs (all identical, PNGs byte-identical to the state's renders) and checked the colour, index and map claims against the bytes.

- medium, patch: the notes said PAL offsets are all 0x1C lower; the code is 4 bytes later and only the data and tables are 0x1C earlier. Both rules now stated, with the PAL addresses found by search.
- low, patch: `plane_probe.py` copied the old scripts' usage exception that AGENTS.md says not to copy (the plan's Code Map pointed at those scripts). `-h` now exits 0; usage errors are a `plane_probe: error:` line, exit 1.
- low, patch: an out-of-range map pointer or a short read gave a traceback; now one error line.
- low, patch: the colour source was the first 0x25F00C00 anywhere; now read from the same literal pool as the cells and maps.
- low, patch: codes 0x700-0x77F from 0x53244 were stated as confirmed without a comparison; the probe now compares CRAM 0xE00-0xEFF (identical on both discs).
- low, patch: the `EPISODE3.PRG` row called the packed span the whole high-entropy region; reworded.
- low, patch: the README example's output folder now matches the `-jp`/`-pal` folders.
- Acceptance criterion 3: the author decided on 2026-10-25 (whole 512x512 page; VDP2 settings as constants), recorded in the epic's Notes; story 3.4's `unknown` is removed.

## Verification

**Commands:**
- `python3 tools/plane_probe.py ...` -- expected: "identical" against `extracted/state_e03/VDP2_VRAM.bin` for each found region.
- `git status --short` -- expected: nothing under `extracted/` or `rom/` staged; only scripts and notes.

**Manual checks (if no CLI):**
- The author compares any rendered plane under `extracted/` with the game's look in the save state.
