# Zwei disc formats

What is known about the disc as of 2026-10-30 (first written 2026-10-06), worked out from the image and one Episode 3 save state. "By eye" means confirmed only by looking at the output.

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
| `E03CMN`, `E03B0N0`..`E03B1N2`, `E03BOSS`, `E03END` (`.MDB` + `.CGB`) | Episode 3 models and textures | Textures decoded; geometry in the second layout (see Models) |
| `DRA01A`..`DRA10J` (`.MDB`, `.CGB`, `.MTB`) | The ten dragon forms | Textures and geometry worked out (see Models); `.MTB` unexamined |
| `DRAnnE01`..`E07.GRB` | Per-corner shading words, 8 bytes a polygon | Confirmed for Episode 3 and `DRA09I`: the game points the models at `E03` (see Shading) |
| `COMMON.MDB`, `.CGB` | The rider | Geometry worked out (see The rider) |
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

- `.MDB` geometry: see Models.

## Models: `.MDB` geometry

Found 2026-10-30 (spike, ticket 2), from the files, the Episode 3 save state and the code in Ghidra. **Confirmed** below means a check that could have failed; **lead** means it fits but nothing tested it. All addresses are the JP disc's. Anyone can repeat the drawing from this section alone; the throwaway scripts are in `extracted/spike-mdb/`, which is git-ignored, and the drawings in `extracted/spike-mdb/out/`.

Everything is big-endian. A file holds absolute pointers, so an offset is `pointer - load address`.

### Load addresses

| File | Load address | Evidence |
|------|--------------|----------|
| `DRA*.MDB` (all ten) | 0x20288000 | Confirmed: the literal at 0x0601ED08 is passed to the file loader with the name table, and `DRA09I` is at that address in the state |
| `COMMON.MDB` | 0x20280000 | Confirmed: in the state at that address |
| `E03B0N*`, `E03BOSS` | 0x202DD000 | `E03B0N0` confirmed in the state; others lead, from their pointers |
| `E03B1N*` | 0x202EF000 | `E03B1N0` confirmed in the state; others lead |
| `E03CMN` | 0x202A0000 | Confirmed in the state |
| `E03END` | 0x202E3000 | Lead, from its pointers |

Ruled out: the lead that `DRA01A`'s longs in 0x0603xxxx are pointers. They are polygons' four one-byte corner indexes that happen to read that way (confirmed: they lie inside the polygon tables the headers point at). The real pointers are 0x2028xxxx, and `E03B0N0`'s are 0x202DDxxx.

### Node tree

- A node is three longs: mesh pointer, first child, next sibling. Any may be 0. A node with no mesh still takes a matrix, so it is a pure joint. Nodes are written children first, so they fill the end of the file. Confirmed: walking from the roots reaches a mesh that parses at every non-zero pointer, in all ten forms, `COMMON` and the nine `E03` files, with no corner index out of range.
- A root is a node that no other node points at. The last 12 bytes of a dragon file are its shadow root (below).
- Each root's tree has one 48-byte matrix per node, in depth-first order (node, children, then siblings), straight after that tree's last mesh. Confirmed: the table's pointers land exactly there, and the rebuilt model holds together.
- A matrix is 12 longs, 16.16 fixed point, row by row: `[r r r tx] [r r r ty] [r r r tz]`. A vertex's position is the product of the matrices from the root down, applied to the vertex.

### First layout: 32-bit vertices (dragons, rider)

Mesh header, 32 bytes:

| Offset | Long |
|--------|------|
| 0 | Radius: the furthest vertex from the node's origin. Confirmed: radius / furthest vertex is 0.9997 to 1.0003 for all of `DRA01A` |
| 4 | Vertex count minus 1 |
| 8 | Pointer to the vertices: 3 signed longs each (x, y, z), whole units, not 16.16 |
| 12 | Polygon count minus 1 |
| 16 | Pointer to the polygons |
| 20 | Pointer to the shading table |
| 24 | Pointer to the normals: 3 longs each, 16.16, one per polygon. Confirmed: length 1 on all but one polygon across the forms |
| 28 | 0 |

Header, confirmed: every pointer and count parses on all ten forms and `COMMON`, and on `DRA01A` and `DRA05E`, where each mesh's tables were listed, the normals end exactly at its header. Vertices in whole units, confirmed by the radius check above and by the projection fit (see Corners).

A polygon is 16 bytes: 4 bytes of corner indexes (A, B, C, D), then CMDPMOD, CMDCOLR, CMDSRCA, CMDSIZE, then a flag word twice. Confirmed: no index is out of range on any form, the four words match the state's VDP1 commands, and the corner order passes the fit (see Corners). The two flag words were equal on every polygon of all ten forms (confirmed by count); why it is held twice is not known.

The file order is the vertices, polygons, shading, normals and header of each mesh in turn. Confirmed on `DRA01A` and `DRA05E` from the same listing; lead on the others.

### Second layout: 16-bit vertices (Episode 3, dragon shadows)

- Mesh header, 12 bytes: radius (long), vertex count (word), 0 (word), pointer to the vertices, then the polygons follow at once. The vertices are 3 signed words each, stored before the header, padded to a long. Confirmed: all such meshes in the nine `E03` files and the 14 on each dragon parse, with the vertices fitting before the header and no index out of range. That they sit directly before it and the padding: lead, seen in the gaps listed, not checked on every mesh.
- A polygon: 4 words of corner indexes, a word whose meaning is not known (0x2E00, 0x2C00, 0x2A00, 0x0C00 and 0x0800 are seen), the flag word, the four texture words, and, when CMDPMOD's low three bits are 4, 6 or 7, 4 shading words in place. Eight zero bytes end the list. Confirmed for low bits 4 and 0: walking the lists this way never ran off a file, and the flag and texture words land as records (nearly every scan record in the nine `E03` files is one of them). Lead for 6 and 7: no polygon with them was seen, and the rule comes from the VDP1 manual's Gouraud modes. Whether the flag word's meaning matches the first layout's: lead, by the same values and `E03B0N1`'s drawing.
- Vertices are scaled: multiply by 16, or by 64 in `E03END`. Confirmed: radius / furthest stored vertex is 16.00 to 16.13 across `E03B0N0`, `E03B0N1`, `E03B0N2`, `E03BOSS`, `E03CMN` and `DRA01A`'s shadow, and 64.00 for all of `E03END`. Where the game keeps the 64 is not traced. With the 16, `E03B0N1` draws as a flyer's body and two wings, by eye.
- `E03CMN`'s tail has many roots of one node each, so it is a set of separate pieces; how they are placed is not worked out.

So one Episode 3 `.MDB` does not have the dragon's layout: it has the same tree and matrices, and different meshes. Story 4.5 needs the first layout only; CAP-9 needs the second. `COMMON` and all dragon bodies are the first.

### The ten forms

- Each form is one file set, with no sharing between forms. Confirmed: no two of the `DRA01`..`DRA10` files are byte-identical, the load tables name each form's own `.MDB`, `.CGB` and `.MTB`, and every pointer walked stays inside its own file. Number and letter go together: `DRA01A`, `02B`, `03C`, `04D`, `05E`, `06F`, `07G`, `08H`, `09I`, `10J`.
- 1ST_READ at 0x0601ECAC takes a form index and loads that form. Its tables are indexed in this order, not the files' order: **0 A, 1 I, 2 B, 3 F, 4 C, 5 D, 6 H, 7 E, 8 G, 9 J**. Confirmed: the name tables in the state read so.
- 1ST_READ file offsets of the tables (add 0x06008000 for the address); PAL offsets are 0x9C more, same contents checked for the seat table:

| JP file offset | PAL | What |
|----------------|-----|------|
| 0x547C8 | 0x54864 | Body matrices, 10 pointers |
| 0x547F0 | 0x5488C | Body roots, 10 pointers |
| 0x54818 | | Shadow roots, 10 pointers |
| 0x54840 | 0x548DC | Rider offsets, 10 of 3 longs |

- Confirmed: for all ten, the body root found as the first unreferenced node equals the table's, the matrices follow its last mesh, and the shadow root is the last 12 bytes.
- Each file also holds a body tree of 27 nodes; a shadow tree of 27 nodes, in the second layout, of 14 flat cards (lead that they are a shadow: flat, one colour table, drawn through the body's matrices, 0x06021722); and, in forms A, B, C, D, F and I only, 30 more roots of 3 nodes each, each with its own 3 matrices. Those are wing meshes in steps whose tips move: lead that they are 15 flapping frames a side, for backlog 18. The still pose does not need them.
- Body nodes, depth first, the same in all ten (body parts by eye on `DRA01A`): 0 root; 1 to 5 hind leg at -X; 6 to 10 hind leg at +X; 11 trunk; 12 neck; 13 head; 14 empty; 15 to 20 tail; 21 to 23 wing at -X; 24 to 26 wing at +X. Mesh-bearing nodes vary between 17 and 24; the others are joints.
- Each form's measured size is left out of these notes: the spike's scripts in `extracted/spike-mdb/` print it. Forms differ a lot in size, `DRA05E` the smallest and `DRA10J` the largest (confirmed by measuring the rest pose).

- Separately moving parts: yes, the nodes. Confirmed: in the state the 27 live matrices at 0x0607CD88 are not the file's, with legs, wings and tail each turned their own way. Each mesh-bearing node is one `ModelPart`, its origin the translation of its product of matrices. The game copies 27 x 12 bytes for the tree (0x144) and animates from `.MTB`, whose format this spike did not look into.

### Link to the texture records

- A polygon's CMDPMOD, CMDCOLR, CMDSRCA and CMDSIZE **are** a texture record: at polygon offset + 4 in the first layout, + 12 in the second. A polygon's texture is the numbered texture with the same four words, as `Textures.Number` dedupes by them.
- Confirmed: on `DRA01A`, `DRA07G` the numbered textures and the polygons' distinct records are the same set; on `DRA09I` the one difference is 1 false numbered record and the flat polygon below. `FindRecords`' scan finds many false records, at any even offset, but few fit the `.CGB` and survive numbering.
- Polygons not found by the scan, and so not numbered: flat colours (CMDSIZE 0), in all forms; and mesh polygons, whose CMDPMOD high byte is 0x05, in `E03CMN` and none on a dragon. This does not affect story 4.5. It will need the author's call before CAP-9, because `FindRecords` fixes the numbering.

### Corners, flips, drawing mode, flat colour, three corners

- Corners: A, B, C, D go to VDP1's XA/YA to XD/YD in order. Confirmed: fitting one projection to 53 of `DRA09I`'s polygons against the state's VDP1 commands leaves a mean error of 0.37 pixels. On the 4 polygons whose words are unique, 0.27 pixels with the corners in order and 6.1 with them turned by one.
- Flips: the flag word's low byte is CMDCTRL's. 0x10 flips left to right, 0x20 top to bottom, low four bits 2 are a textured quad (distorted sprite). Confirmed: it equals the command's low byte on all 66 polygons matched in the state; flip meanings from the VDP1 manual, and the drawings look right by eye. The flips do not change the corner order (see Axes). Seen on the dragons: 0x02, 0x12, 0x22, 0x32, 0x04.
- Drawing mode to material, from CMDPMOD (lead, by the VDP1 manual; `opaque` and `cutout` look right by eye):

| Bits | Meaning | Material |
|------|---------|----------|
| 0x0040 set | Index 0 is drawn | `opaque` |
| 0x0040 clear | Index 0 is transparent | `cutout` |
| Low three bits 3 or 7 | Half-transparent | `half`. Not seen on any file checked |
| 0x0100 set | Mesh | `mesh`. Only in `E03CMN` |

  Dragons hold only CMDPMOD 0x04CC (`opaque`, shaded), 0x048C (`cutout`, shaded), 0x04C4 (flat, shaded) and, in the shadow, 0x0488 and 0x04C8 (no shading). All body polygons are shaded, and nearly all are colour mode 1.
- Flat colour: flag word low four bits 4, CMDSIZE 0, CMDSRCA 0, CMDCOLR the colour, RGB555 with the top bit set. Lead, from the VDP1 manual. Six body polygons across the forms, including `DRA03C`, `DRA05E`, `DRA08H`.
- Three corners: one index repeated. Any corner may be the repeated one, D = C most often. Confirmed by count; none has fewer than three distinct corners. As the asset contract says, the repeat is kept.

### Shading

- Each polygon has 4 RGB555 words, one per corner A to D, top bit set: 8 bytes a polygon. This is VDP1's Gouraud shading, and is baked: the backs are bright and the bellies dark.
- The file's table is replaced at load. Confirmed: in the state, every mesh pointer at header + 20 in `DRA09I`'s body and in `COMMON` is rewritten into 0x20233000, the bytes there equal `DRA09E03.GRB`, and the body's pointers advance by 8 per polygon, starting at 0, in tree order. Rider's start at 0x5B0. The wing frames and shadow are not rewritten.
- `DRAnnE01`, `03`, `05`, `07` hold one table (body + rider + others); `E02` and `E04` are ten times the size, `E06` twenty, except that `DRA02E06.GRB` is a little over twenty tables (confirmed by file sizes on all ten). Lead: frames of a change of light through those episodes. The table used per episode is from 0x0605CD5C and the pointers from 0x0605CA8C and 0x0605CBCC, indexed by episode and form.
- How to use them: VDP1 adds `word - 16` per channel, a known hardware rule, so 0x4210 changes nothing (lead for this game). The drawings multiply by `word / 16`, as a close stand-in.

### Axes and the front

- The rest pose has **+Y up, +X to the dragon's right, head towards -Z**. This is a right-handed system, the same handedness as glTF. Confirmed:
  - In the state's fit, +Y moves up the screen.
  - Moving towards +X reduces the projection's depth, with +Z going to the left of the screen: right-handed.
  - For every non-degenerate polygon of all ten forms and the rider, (B-A) x (C-A) points opposite the stored normal, flips or not. So corners A to D go clockwise seen from outside, as VDP1 draws them. That needs a right-handed system.
  - In the state's commands the rider's head is above his feet on the screen, and by eye `DRA01A_side.png` is the right way up.
- To glTF with the front on +Z: `(x, y, z)` becomes `(-x, y, -z)`, a half-turn about Y, so it is not a mirror. The wing pose of the rest differs by form: roughly level in most, but raised in `DRA06F` and `DRA07G`, whose wings reach several times higher than the rest of the body (confirmed by measuring the rest pose; by eye on `forms_quarter_sheet.png`). `DRA10J`'s also sit wholly above the origin, at y 2,462 to 2,956.

### The rider

- The rider is `COMMON.MDB`, first layout, 11 nodes. 0 hips; 1, 2 and 3, 4 legs; 5 body; 6, 7 arm; 8, 9 arm and gun; 10 head. By eye: a man in blue with a red hat, holding a gun out in front. The save state holds his live tree at 0x0607E3E8, built from the root 0x2028130C (0x0601F826).
- Where he goes. Confirmed by code: 0x060218CA and 0x06023966 read the offset (x, y, z) from the table at 0x0605C840, indexed by the form byte (getter 0x0601EED0), translate by it, and then draw the rider's tree (object + 0x28). Confirmed by fit, for `DRA09I` only: the projection fitted to the dragon misses the rider by a mean 8.1 pixels with no offset, and by 1.2 with the table's entry. Searching for the best offset independently landed within 150 units of that entry on each axis, at 1.05. Both trees share the root's matrix: 0x0607CD88 and 0x0607E46C start with the same 48 bytes.

- The table is 10 entries of 3 longs (x, y, z), in the index order above, at 1ST_READ file offset 0x54840 on JP and 0x548DC on PAL. Its values are not copied here, because they come from the disc. x is 0 in all ten (confirmed by reading them). It was found on each disc by searching for its bytes, so it is known only for these two discs; what happens on any other disc is story 5's to define.
- Decision (author, 2026-10-30): the seat is the game's rider position for that form, read from this table in 1ST_READ on the player's disc. The seat point is the rider's origin from the table, which lies inside his hips, not on the dragon's surface: the hips' mesh spans both sides of that origin in y (confirmed from `COMMON.MDB`'s vertices). The tuning settings adjust from there. By eye on `DRA01A` he sits astride the neck, near the head, not mid-way along the back.

### Scale

Lead: **about 4,000 units to a metre**. Nothing in the data says what a unit is, so this assumes a rider 1.7 m tall, measured by standing his parts up. Story 4.6 confirms the divisor in the headset; the seat settings can also rescale.

### On the PAL disc

Confirmed: 131 of the 132 `DRA*`, `COMMON*` and `E03*` files are byte for byte the same on MK-81022 V1.000, including every `.MDB`, `.CGB`, `.MTB` and `.GRB`. Only `COMMON.DAT` differs.

### Not known

- Why the flag word is held twice, and the second layout's unknown word.
- The bytes before a dragon's first mesh (0x40 zeros) and the 0x20-byte zero blocks between some meshes. Lead: they stand for the empty joints. A few larger gaps in `DRA07G` and `DRA08H` were not looked at.
- Why `DRA02E06.GRB` is a little over twenty tables.
- Where `E03END`'s 64 comes from, and how `E03CMN`'s pieces are placed.
- `.MTB`: sizes are not a whole number of 27 x 12 bytes in general, so it is not simply that. Backlog 18.

### How the drawing was made

Walk the body root's tree, multiply matrices, take each polygon's four corners, sample its decoded texture (corner A top left, B top right, C bottom right, D bottom left, then the flips), multiply by shading, add the rider at the table's offset, z-buffered. `python3 draw2.py` in `extracted/spike-mdb/` writes `out/forms_quarter_sheet.png` (all ten, with the rider) and `out/DRA01A_above.png`, `_side.png`, `_behind.png`.

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
