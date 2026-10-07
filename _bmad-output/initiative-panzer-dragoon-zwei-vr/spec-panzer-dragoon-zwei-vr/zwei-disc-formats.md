# Zwei disc formats

What is known about the disc as of 2026-10-06, worked out from the image and one Episode 3 save state. "By eye" means confirmed only by looking at the output.

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
| 4: 256 colours | 4,033 | Output is grey and unverified. Palette is not in the file; the save state's CRAM is the likely source, not yet applied |
| 0 | 100 | Not handled |
| 5 | 13 | Not handled |

- `.MDB` geometry is undecoded beyond the texture records. Vertices appear to be big-endian signed 16-bit. The Saturn draws quads, not triangles, so expect four-cornered faces.

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

1. Colour the 256-colour textures with the save state's CRAM.
2. Find where on the disc the floor and canopy data is stored.
3. Disassemble the Episode 3 region, then trace the code that reads `EPISODE3.DAT`.
4. Decode `.MDB` geometry.
