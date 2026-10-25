# Zwei disc formats

What is known about the disc as of 2026-10-25 (first written 2026-10-06), worked out from the image and one Episode 3 save state. "By eye" means confirmed only by looking at the output.

## Disc

- `PANZER DRAGOON ZWEI`, GS-9049, V1.001, 1996-02-27.
- Track 1 is MODE1/2352 with an ordinary ISO9660 file system. Tracks 2 to 4 are short CD audio.
- 308 files, named by episode.

## Files

| Files | Holds | State |
|-------|-------|-------|
| `EPISODE3.PRG` | Episode 3 code; loads the `E03*` and `COMMON` pairs by name. Also holds the floor and canopy planes, packed (0x3FDAC to 0x52ACB), which make up most of the high-entropy region (about 0x40000 to 0x53FFF) | Loads at 0x06080000; mostly undisassembled |
| `EPISODE3.DAT` | Repeating 3x4 matrices in 4.12 fixed point (rotation plus position) | Looks like recorded motion; what it drives is unknown |
| `EPISODE3.SND`, `E3BOSS.SND` | Sound | Unexamined |
| `E03CMN`, `E03B0N0`..`E03B1N2`, `E03BOSS`, `E03END` (`.MDB` + `.CGB`) | Episode 3 models and textures | Textures decoded; geometry not |
| `DRA01A`..`DRA10J` (`.MDB`, `.CGB`, `.MTB`) | The ten dragon forms | Textures decoded; `.MTB` unexamined |
| `DRAnnE03.GRB` | 16-bit words with the top bit set: RGB555 colours | Guess: Gouraud shading tables per episode |
| `EPISODE2.SCB` (131,072 bytes) | The only `.SCB` on the disc | Guess: an Episode 2 scroll plane |
| `OPENINGA/B/C.cpk` | Intro videos | Not needed |
| `1ST_READ.PRG` | Core engine, including the LZ77 unpacker at 0x0600FD1C (see Floor and canopy) | Loads at 0x06008000 |

## Textures: `.CGB` + `.MDB`

- `.CGB` is raw VDP1 pixel data with no header.
- The paired `.MDB` holds, per polygon, four big-endian words in VDP1 command order: CMDPMOD, CMDCOLR, CMDSRCA, CMDSIZE.
- Width is `(CMDSIZE >> 8) * 8`. Height is `CMDSIZE & 0xFF`.
- CMDSRCA and CMDCOLR are VRAM addresses divided by 8. The address each `.CGB` loads at is not in the file; `tools/cgb_textures.py` infers it from the records (0x49000 for `E03B0N0`, 0x20000 for `E03CMN`).
- 9,020 textures across all 77 pairs:

| Colour mode | Count | State |
|-------------|-------|-------|
| 1: 16 colours through a lookup table | 4,873 | Decoded in colour. The 32-byte RGB555 table is inside the `.CGB`. By eye on `E03B0N0`, `E03BOSS`, `DRA01A` |
| 4: 256 colours | 4,033 | Palette is in VDP2 colour RAM, filled from `EPISODE3.PRG` for Episode 3 (see Palettes). Coloured by eye on `E03CMN` and `E03END`; the extractor colours them from the 0xE23C table (ticket 2.6) |
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
- Colour RAM above 0xA00 bytes (codes 0x500 up) is not filled from these tables. Codes 0x500 to 0x5FF are all zero in the save state. Codes 0x600 to 0x6FF are the floor and canopy colours (see Floor and canopy), and codes 0x700 to 0x77F are 0x53244's 128 colours. The planes' set-up code copies both, not a table. Confirmed: `plane_probe.py` compares both blocks with the state's colour RAM (bytes 0xC00 to 0xDFF and 0xE00 to 0xEFF). Codes 0x780 up match nothing on the disc found so far.
- The offsets above are for GS-9049 V1.001. The tables' file offsets, and the load address 0x06080000, would differ on another version.

## Floor and canopy

Found 2026-10-25 (spike, ticket 2). Every "confirmed" below is a byte comparison against the Episode 3 save state that could have failed; `tools/plane_probe.py` repeats them.

- They are the two rotation-parameter planes of VDP2's RBG0 layer: plane A is the floor, plane B the canopy. By eye against the save state's screenshot.
- Each is one 512x512 page of 16x16-pixel cells in 256 colours, with one-word pattern names.
- VDP2 registers in the state: CHCTLB=0x1100, PNCR=0x8008, PLSZ=0, MPOFR=0x33, RPMD=3 (A and B switched by window), CRAOFB=0 (no colour offset for RBG0). Plane A map 0xC0 (VRAM 0x60000), plane B map 0xC1 (VRAM 0x60800). CRAM mode 1 (2048 colours, RGB555). NBG0 and NBG3 are also on. The code that sets these registers is not traced; lead (by inference): the lists of number pairs at 0x52B58, 0x52BB4, 0x52C10, 0x52C24 and 0x52C30, which the set-up code passes to 1ST_READ routines at 0x060444F0, 0x06045DC4, 0x06046400, 0x060465C4 and 0x06046A74.

### Where they are

All in `EPISODE3.PRG`. The packed streams are back to back and fill most of the region whose entropy is about 7.7 bits a byte (about 0x40000 to 0x53FFF):

| What | File offset (JP) | Packed | Unpacked | Goes to | State |
|------|------------------|--------|----------|---------|-------|
| Cells | 0x3FDAC | 0x1285C | 0x1BD00 | VRAM 0x40000 to 0x5BCFF | Confirmed |
| Plane A map | 0x52608 | 0x274 | 0x800 | VRAM 0x60000 | Confirmed |
| Plane B map | 0x5287C | 0x250 | 0x800 | VRAM 0x60800 | Confirmed |
| Map list | 0x52ACC | | | Two pointers (0x060D2608, 0x060D287C) and a zero word | Traced |
| Colours | 0x52C44 | stored plain | 0x200 (256 colours) | Colour RAM 0xC00 (codes 0x600 to 0x6FF) | Confirmed |
| Other colours | 0x53244 | stored plain | 0x100 (128 colours) | Colour RAM 0xE00 (codes 0x700 to 0x77F) | Confirmed; what uses them is not known (lead: NBG0 or NBG3) |

- Each packed stream ends where the next begins, and the map list follows the last.
- The code (traced in Ghidra): the function at 0x06095CF0 (file 0x15CF0), reached through a table of function pointers at file 0x53344, does this in order:
  1. calls 0x0600DA80 to copy 0x100 colours from 0x060D2C44 to colour RAM 0x25F00C00, and 0x80 from 0x060D3244 to 0x25F00E00;
  2. calls 1ST_READ's unpacker 0x0600FD1C with source 0x060BFDAC and destination 0x25E40000: the cells;
  3. calls 0x06039218 with the map list 0x060D2ACC and destination 0x25E60000, which calls 0x0600FD1C for each pointer in the zero-ended list, adding 0x800 to the destination after each.
- Then it passes the tables at 0x52AD8 (16 words of 0x25E60000) and 0x52B18 (16 of 0x25E60800) to 0x060399F0 with 0 and 1: the 16-plane map tables of rotation parameters A and B, every plane pointing at the one page. Lead (by inference from the values; 0x060399F0 not read).
- Which part of Episode 3 the function at 0x06095CF0 serves is a lead: it is the only reference to these sources found, so the floor and canopy look the same through the episode, but the state is from one point only.

### Packing

LZ77, read from the unpacker's code and confirmed by the comparisons above:

- Flag bits are read least significant first, eight to a byte; the stream starts with a flag byte, and a new flag byte is read from the stream when the eight are used.
- Flag 1: copy one literal byte.
- Flags 0, 0: a short copy. Two more flag bits (first one high) give the length minus 2 (2 to 5); the next byte is the offset minus 256 (back 1 to 256 bytes).
- Flags 0, 1: a long copy. The next two bytes are a little-endian word. Its top 13 bits are the offset minus 8192 (back 1 to 8192 bytes). Its low 3 bits are the length minus 2 (3 to 9), or when they are 0, the next byte plus 1 is the length (1 to 256).
- A long-copy word of 0 ends the stream. The routine returns the number of bytes written.
- Copies go byte by byte, so a copy may overlap what it writes.

### Layout

- Cells: 16x16 pixels, 256 colours, 256 bytes each, in the four-8x8 order VDP2 uses (top left, top right, bottom left, bottom right). Cell 0 (VRAM 0x40000) is blank. A pattern name `w` points at VRAM `((w & 0x3FF) << 2 | 0x2000) * 0x20`, with the supplement bits from PNCR=0x8008.
- Plane A uses cells 1 to 256 (VRAM 0x40100 to 0x500FF); plane B uses all 188 cells 257 to 444 (0x50100 to 0x5BCFF). Every name is palette 6 (codes 0x600 up) with no flips.
- Plane A's pixels use only colour indexes 1 to 127, and plane B's only 128 to 255: the two share colour block 0x600 to 0x6FF, half each.
- The maps are stored, not generated (confirmed: they unpack to the state's maps). Plane A's map is cells 1 to 256 in order as a 16x16 grid, repeated 2x2: the floor's repeating unit is 256x256 pixels.
- Plane B's map is not a 2x2 repeat. Its left and right halves are the same, but top and bottom differ: the canopy's repeating unit is 256 pixels wide and 512 tall (the whole page's height). Within it, in rows of 16 cells: rows 0 to 7 hold cells 257 to 383 in order (one used twice); rows 8 to 15 keep rows 0 to 7's outer four columns each side and put cells 384 to 444 in most of the middle eight; rows 16 to 23 are those outer columns side by side, twice; rows 24 to 31 repeat rows 0 to 7.

### On the PAL disc

- MK-81022 V1.000 moves things by two different amounts, all found by searching the PAL file:
  - The code moves 4 bytes later: the set-up function is at file offset 0x15CF4 (0x06095CF4, its first 0x118 bytes the same as JP's), its literal pool's 0x25E40000 at 0x15E6C (JP 0x15E68), and the colour RAM literals 0x25F00C00 at 0x15E30 (JP 0x15E2C) and 0x25F00E00 at 0x15E3C (JP 0x15E38).
  - The data and tables move 0x1C bytes earlier: cells at 0x3FD90, maps at 0x525EC and 0x52860, map list at 0x52AB0, colours at 0x52C28 and 0x53228, and the function pointer table at 0x53328 (JP 0x53344).
  - 1ST_READ's unpacker is at 0x0600FC84 (JP 0x0600FD1C).
- Confirmed: `plane_probe.py` on the PAL ISO gives bytes identical to the JP save state for the cells, both maps and both colour blocks, so the planes are the same on both discs.
- `plane_probe.py` finds the sources by the literal pool (0x25E40000 then a source pointer, and 0x25E60000 then the map list) and the colour RAM literals 0x3C and 0x30 bytes before it, not by fixed offsets.

### Ruled out

- The cells as plain bytes in any disc file or in work RAM (64-byte probes, before the trace).
- Code that writes the maps: they are packed data.
- `EPISODE3.PRG` changing once loaded: the save state's work RAM H from 0x06080000 equals the file byte for byte, packed planes included, so the game unpacks straight into VRAM and keeps no unpacked copy.

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
2. Port the planes' unpacking to the decoding library (story 3.4), using `tools/plane_probe.py` as the reference.
3. Trace the VDP2 register set-up lists (0x52B58 onward) if story 3.4 needs the plane settings from the disc rather than as constants.
4. Disassemble the Episode 3 region, then trace the code that reads `EPISODE3.DAT`.
5. Decode `.MDB` geometry.
