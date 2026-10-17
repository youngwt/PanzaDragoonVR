# Zwei disc formats

What is known about the disc as of 2026-10-17 (first written 2026-10-06), worked out from the image and one Episode 3 save state. "By eye" means confirmed only by looking at the output.

## Disc

- `PANZER DRAGOON ZWEI`, GS-9049, V1.001, 1996-02-27.
- Track 1 is MODE1/2352 with an ordinary ISO9660 file system. Tracks 2 to 4 are short CD audio.
- 308 files, named by episode.

## Files

| Files | Holds | State |
|-------|-------|-------|
| `EPISODE3.PRG` | Episode 3 code; loads the `E03*` and `COMMON` pairs by name. Last quarter has very high entropy | Loads at 0x06080000; mostly undisassembled |
| `EPISODE3.DAT` | Repeating 3x4 matrices in 4.12 fixed point (rotation plus position) | Looks like recorded motion; what it drives is unknown |
| `EPISODE3.SND`, `E3BOSS.SND` | Sound | Unexamined |
| `E03CMN`, `E03B0N0`..`E03B1N2`, `E03BOSS`, `E03END` (`.MDB` + `.CGB`) | Episode 3 models and textures | Textures decoded; geometry not |
| `DRA01A`..`DRA10J` (`.MDB`, `.CGB`, `.MTB`) | The ten dragon forms | Textures decoded; `.MTB` unexamined |
| `DRAnnE03.GRB` | 16-bit words with the top bit set: RGB555 colours | Guess: Gouraud shading tables per episode |
| `EPISODE2.SCB` (131,072 bytes) | The only `.SCB` on the disc | Guess: an Episode 2 scroll plane |
| `OPENINGA/B/C.cpk` | Intro videos | Not needed |
| `1ST_READ.PRG` | Core engine | Loads at 0x06008000 |

## Textures: `.CGB` + `.MDB`

- `.CGB` is raw VDP1 pixel data with no header.
- The paired `.MDB` holds, per polygon, four big-endian words in VDP1 command order: CMDPMOD, CMDCOLR, CMDSRCA, CMDSIZE.
- Width is `(CMDSIZE >> 8) * 8`. Height is `CMDSIZE & 0xFF`.
- CMDSRCA and CMDCOLR are VRAM addresses divided by 8. The address each `.CGB` loads at is not in the file; `tools/cgb_textures.py` infers it from the records (0x49000 for `E03B0N0`, 0x20000 for `E03CMN`).
- 9,020 textures across all 77 pairs:

| Colour mode | Count | State |
|-------------|-------|-------|
| 1: 16 colours through a lookup table | 4,873 | Decoded in colour. The 32-byte RGB555 table is inside the `.CGB`. By eye on `E03B0N0`, `E03BOSS`, `DRA01A` |
| 4: 256 colours | 4,033 | Palette is in VDP2 colour RAM, filled from `EPISODE3.PRG` for Episode 3 (see Palettes). Coloured by eye on `E03CMN` and `E03END`; not yet in the extractor |
| 0: 16 colours from a colour bank | 100 records found by the scan, not all real | Lead: same colour RAM as mode 4. In Episode 3 the clear case is `E03BOSS`'s shadows (colour code 0x400, colour RAM bank 4: one dark colour); most other mode 0 records do not fit their `.CGB` and are likely false |
| 5 | 13 | Not handled |

- `.MDB` geometry is undecoded beyond the texture records. Vertices appear to be big-endian signed 16-bit. The Saturn draws quads, not triangles, so expect four-cornered faces.

## Palettes for colour banks (modes 0 and 4)

Found 2026-10-17, ticket 2.3.

- Banks below are 256-colour blocks of colour RAM numbered by byte offset / 0x200: bank 2 starts at byte 0x400 and holds colour codes 0x200 to 0x2FF.
- Lead (by eye): a mode 4 pixel is the colour code `((CMDCOLR & 0xFF00) | pixel) & 0x7FF` into VDP2 colour RAM, with no sprite colour offset. `E03CMN`'s 116 mode 4 textures coloured from the Episode 3 save state's colour RAM this way come out as forest foliage, vines and trunks, and `E03END`'s 45 likewise.
- Lead (by inference): a mode 0 pixel is `((CMDCOLR & 0xFFF0) | pixel) & 0x7FF`. The only clear case is `E03BOSS`'s shadows at code 0x400, where bank 4 is one transparent entry and fifteen of colour 0x0022.
- Confirmed (a byte comparison that could have failed): the save state's colour RAM from byte 0x000 to 0x9FF equals `EPISODE3.PRG`'s blocks at 0x31E30 (banks 0 and 1), 0x32230 (banks 2 and 3) and 0x34030 (bank 4), byte for byte. Recolouring `E03CMN` from the disc's block instead of the save state gives an identical image.
- `EPISODE3.PRG` holds tables of pairs of big-endian words: destination (`0x25F00000` plus a colour RAM byte offset) then source (a load address, `0x06080000` plus a file offset). Lead (by inference, no code traced): each pair copies 0x200 bytes (256 colours), as the 0x200 stride between destinations and the matching blocks suggest; a table ends at the first pair whose destination is not `0x25Fxxxxx`, as the word after each table (0x06022E58, 0x060A5B7C) suggests. The two known tables were found by their references to 0x060B1E30; a rule for where a table starts is not known.
- Two tables, at file offsets 0x288 and 0xE23C:

| Colour RAM | Table at 0x288 | Table at 0xE23C |
|------------|----------------|-----------------|
| 0x000 (bank 0) | 0x31E30 | 0x31E30 |
| 0x200 (bank 1) | 0x32030 | 0x32030 |
| 0x400 (bank 2) | 0x33230 | 0x32230 |
| 0x600 (bank 3) | 0x33430 | 0x32430 |
| 0x800 (bank 4) | 0x34030 | 0x34030 |

- Offsets in the table are bytes of colour RAM, so "bank 2" at 0x400 holds colour codes 0x200 to 0x2FF.
- The save state matches the table at 0xE23C. `E03END` (banks 0 to 3) looks right with it by eye; with the 0x288 table its stone textures turn greener with red specks. Which part of Episode 3 uses which table is a lead, not known: the code that picks one is not traced.
- Colour RAM above 0xA00 bytes (codes 0x500 up) differs from these tables in the save state; the block at codes 0x600 to 0x6FF appears in `EPISODE3.PRG` at 0x52C44 with no table found. Lead (by inference): those colours are for the VDP2 planes, since the Episode 3 texture records found use codes up to 0x4FF, apart from one likely false mode 0 record.
- The offsets above are for GS-9049 V1.001. The tables' file offsets, and the load address 0x06080000, would differ on another version.

## Floor and canopy

- They are the two rotation-parameter planes of VDP2's RBG0 layer: plane A is the floor, plane B the canopy. By eye against the save state's screenshot.
- Each is one 512x512 page of 16x16-pixel cells in 256 colours, with one-word pattern names. Each looks like a 256x256 pattern repeated 2x2, so it tiles.
- VDP2 registers in the state: CHCTLB=0x1100, PNCR=0x8008, PLSZ=0, MPOFR=0x33, RPMD=3 (A and B switched by window). Plane A map 0xC0 (VRAM 0x60000), plane B map 0xC1 (VRAM 0x60800). CRAM mode 1 (2048 colours, RGB555). NBG0 and NBG3 are also on.
- They are not in the `.CGB` files. Which disc file holds the tiles, maps and palette is unknown; the high-entropy tail of `EPISODE3.PRG` is the lead.

## Save states

- Mednafen stores RAM, VRAM and CRAM as host-endian 16-bit words. Swap bytes in pairs to get Saturn order.
- `tools/mednafen_state.py` unpacks a state into `extracted/state_e03/`.

## Code

- Ghidra's auto-analysis found 938 functions: 849 in the core engine and 38 in the Episode 3 range. Level code is reached through address tables that auto-analysis does not follow, so the Episode 3 region (0x06080000 onward, about 348 KB) needs disassembling directly.

## Leads not yet followed

- yaz0r's open-source Azel (a reverse-engineered PC port of Panzer Dragoon Saga): same studio, later engine. Shared formats are unconfirmed.
- The SegaXtreme forums.
- [Panzer Dragoon Legacy: MIDI files extracted from Zwei and Saga](https://discuss.panzerdragoonlegacy.com/t/i-have-extracted-the-midi-files-from-panzer-dragoon-zwei-and-saga/9013), for the audio side.

## Next

1. Trace which code picks the colour upload table at 0x288 or 0xE23C, and for which part of Episode 3.
2. Find where on the disc the floor and canopy data is stored.
3. Disassemble the Episode 3 region, then trace the code that reads `EPISODE3.DAT`.
4. Decode `.MDB` geometry.
