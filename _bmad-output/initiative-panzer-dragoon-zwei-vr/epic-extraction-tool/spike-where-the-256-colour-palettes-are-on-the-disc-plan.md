---
title: 'Where the 256-colour palettes are on the disc'
type: 'chore'
ticket: '3'
created: '2026-10-17'
status: 'built'
baseline_revision: '4fed8751fefbb6a5d926ba5140b887a9a842f0d1'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** 161 of Episode 3's textures (mode 4, 256 colours) and 9 (mode 0, colour bank) take their colours from VDP2 colour RAM, not from their own files, so the extractor writes them grey. CAP-3 forbids taking colours from a save state: the extractor must find them on the disc. Story 6 cannot be planned until we know where they are and how a texture finds its palette.

**Approach:** Investigate in order, cheapest first, and stop at the first answer that holds: confirm with the existing save state that colour RAM is the source and which part a texture uses; search the disc's files for those colours; only then trace the game's colour RAM writes in Mednafen and Ghidra. Write the answer, or what was ruled out, into `zwei-disc-formats.md`, and the author decides what story 6 does.

## Boundaries & Constraints

**Always:**
- Each step's evidence (command or tool, what it shows) goes into Implementation Notes as it is found.
- Scripts made for the investigation live in the session scratch folder or under the git-ignored `extracted/`; nothing from the disc, the save state or memory dumps is committed.
- Findings are stated as confirmed (by a check that could have failed) or as a lead (by eye or by inference).

**Never:**
- No change to the decoding library, the game or the contract; story 6 does the decoding.
- No palette taken from a save state into anything the extractor ships.
- No guessing a format into the notes: an unconfirmed reading is marked as a lead.

</frozen-after-approval>

## Code Map

- `extracted/state_e03/` (git-ignored) -- an Episode 3 Mednafen state already unpacked by `tools/mednafen_state.py`: `VDP2_CRAM.bin` (byte-swapped to Saturn order), `VDP1_VRAM.bin`, `VDP2_VRAM.bin`, `WorkRAMH.bin`, `WorkRAML.bin`, `state.mc`. CRAM was in mode 1 (2048 RGB555 colours).
- `extracted/disc/` (git-ignored) -- every file from the disc, extracted by `tools/saturn_disc.py`.
- `tools/cgb_textures.py` -- `find_records`, `infer_base`, `decode`; mode 4 is grey by index today. For mode 4 and mode 0, CMDCOLR is a colour bank (the palette's offset in CRAM), not a lookup-table address.
- `lib/PdzDecoding/Textures.cs` -- the C# port; the place story 6 will change.
- `tools/mednafen/run.sh` (git-ignored) -- Mednafen; save states in `tools/mednafen/home/mcs/`. Ghidra 12.1.4 with the Saturn loader: `ghidra`, `ghidra-headless` in `~/.local/bin`; a project under `extracted/ghidra/`.
- `EPISODE3.PRG` loads at 0x06080000; its last quarter has very high entropy (`zwei-disc-formats.md`). `DRAnnE03.GRB` files hold RGB555 words with the top bit set.
- Episode 3 files with mode 4 textures: `E03CMN` (116) and `E03END` (45), by the prototype's log; mode 0: 9.

## Tasks & Acceptance

**Execution:**
- [x] Step 1 -- with the save state: colour each mode 4 texture of `E03CMN` from `VDP2_CRAM.bin` at the bank its CMDCOLR names (and mode 0 likewise), in a scratch script; the author judges by eye whether they come out right. Confirms or rules out CRAM, and the bank rule.
- [x] Step 2 -- search every file in `extracted/disc/` for the CRAM blocks those textures use, as raw RGB555 words (both byte orders, with and without the top bit). A hit gives the file and offset; check it against a second block and a second state if one is needed.
- [x] Step 3 (not needed: step 2 found the source) -- only if step 2 finds nothing: look for a compressed or generated source. Check whether the high-entropy tail of `EPISODE3.PRG` decompresses with a known Saturn scheme, then trace writes to colour RAM (0x05F00000) during Episode 3's load in Mednafen's debugger and Ghidra, with the author running the game.
- [x] `zwei-disc-formats.md` -- the palette source and how a texture finds its palette, with the evidence; or what was ruled out and the next lead. Update the colour modes table and "Next".
- [x] Stop and ask the author what story 6 does, and record the decision.

**Acceptance Criteria:**
- Given the findings, when `zwei-disc-formats.md` is read, then it names where the palettes come from and how a mode 4 and a mode 0 texture find theirs, each marked confirmed or lead, with evidence; or it lists what was ruled out.
- Given step 1's images, when the author looks at them, then they say whether colour RAM gives the right colours.
- Given `git status`, when read, then no disc data, save state, dump or scratch output is tracked.

## Implementation Notes

Done in the main conversation (the author judges images mid-way). Scripts in the session scratch folder (`spike/`); images under the git-ignored `extracted/`.

- **Step 1, confirmed.** `E03CMN`'s mode 4 records use banks 0x000 and 0x100 (473 and 2,197 records). Colouring its 116 mode 4 textures with code `((CMDCOLR & 0xFF00) | pixel) & 0x7FF` into `extracted/state_e03/VDP2_CRAM.bin`, no sprite colour offset, gives forest foliage, vines and trunks (`extracted/spike-e03cmn-mode4-cram.png`).
- **Step 2, found.** Searching every disc file for the save state's colour blocks: the first 1,024 colours equal `EPISODE3.PRG` from 0x31E30 byte for byte (big-endian, top bit as stored); codes 0x400-0x4FF equal 0x34030; codes 0x600-0x6FF equal 0x52C44. Codes 0x500 up otherwise differ (VDP2 planes).
- **The upload tables.** The two references to 0x060B1E30 in `EPISODE3.PRG` sit in tables of (destination `0x25F00000 + offset`, source address) pairs at file offsets 0x288 and 0xE23C, 0x200 bytes per copy. They agree on colour RAM 0x000, 0x200, 0x800 and differ on 0x400 and 0x600 (0x33230/0x33430 against 0x32230/0x32430; 482 of their 512 colours differ). The save state matches 0xE23C.
- **From the disc alone.** Recolouring `E03CMN` from `EPISODE3.PRG`'s block instead of the save state gives a byte-identical image. `E03END`'s 45 mode 4 textures (banks 0x000-0x300) look right with the 0xE23C table (`extracted/spike-e03end-mode4-disc.png`); with the 0x288 table its stone textures turn greener with red specks (`extracted/spike-e03end-mode4-table1.png`).
- **Mode 0.** `E03BOSS`'s 205 mode 0 records use code 0x400 onward: one transparent entry and fifteen of colour 0x22, so they are shadows. The other mode 0 records the scan finds in `E03CMN` and `E03END` mostly do not fit the `.CGB` and are likely false records.
- **Not found:** which code picks a table, and for which part of Episode 3. Step 3 was not needed for the palette source.
- **Author, 2026-10-17:** the coloured sheets look right. Decision for story 6: read the colour upload table at 0xE23C from `EPISODE3.PRG`, found by its pattern rather than a fixed offset, and colour every mode 4 and mode 0 texture from it; the 0x288 table stays a recorded lead.
- `zwei-disc-formats.md`: new section "Palettes for colour banks", updated colour modes table and Next.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-17: high 0, medium 1, low 9, false 0, maybe-false 0. Nine patched, one carried. Every disc claim was re-checked against the files and held.

- low, patched: the mode 4 rule was labelled "Confirmed" on a by-eye result. Now a lead (by eye); the byte comparisons are the confirmed part.
- low, patched: the mode 0 rule was unmarked. Now a lead (by inference), with the `E03BOSS` shadow evidence.
- low, patched: the 0x200 copy size and the table end rule were stated as fact without traced code. Both now leads, with what suggests them, and the doc says no start rule is known.
- low, patched: "those colours belong to the VDP2 planes" was unmarked. Now a lead, tied to the codes the records use.
- low, patched: "bank" meant two things. The section now defines banks by colour RAM byte offset, and the mode 0 row says "colour code 0x400, colour RAM bank 4".
- low, patched: "482 colours apart" now reads "482 of their 512 colours differ".
- low, patched: the Execution boxes are ticked.
- low, patched: the doc's "as of" date is updated.
- low, patched: the mode 0 count now says it is scan records, not all real.
- medium, carried to `bmad-ticket`: the story 6 decision is in this plan only. `tickets.toml` still has story 6's `unknown`, story 6 covers only the 256-colour textures, story 5 still writes mode 0 in greyscale, and the epic's Notes still call the palette location unknown. The build may not edit ticket files; updating entries 5 and 6 and the epic's Notes is for `bmad-ticket`, with the author.

## Verification

**Manual checks (if no CLI):**
- The author judges step 1's coloured textures by eye, and agrees the written answer and story 6's decision.
