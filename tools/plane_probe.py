#!/usr/bin/env python3
"""Unpack Episode 3's floor and canopy planes (VDP2 RBG0) from the disc and check them.

    python3 tools/plane_probe.py [-h] <disc image> <out_dir> [<state_dir>]

The disc image is the data track: a MODE1/2352 track 1 .bin or a 2048-byte-sector
.iso. out_dir must be inside the repository's extracted/ folder. With state_dir
(the output of tools/mednafen_state.py), each unpacked region is compared byte for
byte with the save state's VDP2 VRAM and colour RAM.

What EPISODE3.PRG does (traced in GS-9049 V1.001, file offsets 0x15CF0 to 0x15E80):
- unpacks the cells with 1ST_READ's routine at 0x0600FD1C into VRAM 0x25E40000;
- unpacks each map in a zero-ended list of pointers to VRAM 0x25E60000, one
  0x800-byte page after another (plane A's map, then plane B's);
- copies 256 colours into colour RAM 0x25F00C00 (codes 0x600 to 0x6FF) and 128
  into 0x25F00E00 (codes 0x700 to 0x77F).
This script finds that literal pool by its two VRAM addresses, and the two colour
RAM addresses 0x3C and 0x30 bytes before it, so it also works on discs where the
data sits at other offsets (the PAL MK-81022 V1.000).

The packing is an LZ77 with flag bits read least significant first, eight to a
byte: 1 is a literal byte; 0 then 0 is a short copy (2 bits of length + 2, then a
byte of offset - 256); 0 then 1 is a long copy (a little-endian word: offset is
its top 13 bits - 8192, length its low 3 bits + 2, or when those are 0 the next
byte + 1); a long word of 0 ends the stream.

Writes rbg0_plane_A.png and rbg0_plane_B.png (one 512x512 page each), rendered
with the VDP2 settings the save state's registers hold, since the code that sets
those registers is not traced.
"""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from mednafen_state import render_rbg0_plane
from png import write_png
from saturn_disc import SaturnDisc

LOAD_ADDRESS = 0x06080000      # where EPISODE3.PRG is loaded
CELLS_VRAM = 0x25E40000        # VDP2 VRAM bank B0
MAPS_VRAM = 0x25E60000         # VDP2 VRAM bank B1
# Colour RAM copies: (destination, number of colours, distance of the literal before CELLS_VRAM's).
COLOUR_COPIES = [
    (0x25F00C00, 256, 0x3C),   # colour codes 0x600 to 0x6FF: the planes' colours
    (0x25F00E00, 128, 0x30),   # colour codes 0x700 to 0x77F
]
MAP_PAGE = 0x800               # one 32x32 page of one-word pattern names
VDP2_VRAM, VDP2_CRAM = 0x25E00000, 0x25F00000

# VDP2 registers as the Episode 3 save state holds them (word index = byte offset / 2).
STATE_REGISTERS = {
    0x2A: 0x1100,  # CHCTLB: RBG0 256 colours, 2x2 cells
    0x38: 0x8008,  # PNCR: RBG0 one-word pattern names, supplement 8
    0x3E: 0x0033,  # MPOFR: map offset 3 for parameters A and B
    0x50: 0x0000,  # MPABRA: parameter A map 0 (0xC0 with the offset): VRAM 0x60000
    0x60: 0x0001,  # MPABRB: parameter B map 1 (0xC1): VRAM 0x60800
    0xE6: 0x0000,  # CRAOFB: no colour RAM offset for RBG0
}


class DiscImage(SaturnDisc):
    """SaturnDisc that also reads 2048-byte-sector images."""

    def __init__(self, path):
        with open(path, "rb") as f:
            f.seek(16 * 2048 + 1)
            iso = f.read(5) == b"CD001"
        self.sector_raw, self.sector_header = (2048, 0) if iso else (2352, 16)
        super().__init__(path)

    def read_sectors(self, lba, count=1):
        out = bytearray()
        for i in range(lba, lba + count):
            self.f.seek(i * self.sector_raw + self.sector_header)
            out += self.f.read(2048)
        return bytes(out)

    def file(self, name):
        for entry, lba, size in self.files:
            if entry == name:
                return self.read_file(lba, size)
        raise ValueError(f"no {name} on the disc")


def unpack(data, pos):
    """Unpack one stream starting at data[pos]; return (bytes, packed length)."""
    start, out = pos, bytearray()
    flags, left = data[pos], 9
    pos += 1

    def bit():
        nonlocal flags, left, pos
        left -= 1
        if left == 0:
            flags, left = data[pos], 8
            pos += 1
        b = flags & 1
        flags >>= 1
        return b

    while True:
        if bit():
            out.append(data[pos])
            pos += 1
            continue
        if not bit():
            length = (bit() << 1 | bit()) + 2
            offset = data[pos] - 256
            pos += 1
        else:
            word = data[pos] | data[pos + 1] << 8
            pos += 2
            if word == 0:
                return bytes(out), pos - start
            offset = (word >> 3) - 0x2000
            length = (word & 7) + 2 if word & 7 else data[pos] + 1
            if not word & 7:
                pos += 1
        if -offset > len(out):
            raise ValueError(f"copy from before the start of the stream at 0x{start:X}")
        for _ in range(length):
            out.append(out[offset])


def word(data, offset):
    return struct.unpack(">I", data[offset:offset + 4])[0]


def find_sources(prg):
    """Return (cells offset, [map offsets], [(colour RAM address, colours, offset)]) in prg."""
    end = LOAD_ADDRESS + len(prg)
    to_file = lambda address: address - LOAD_ADDRESS if LOAD_ADDRESS <= address < end else None
    for pool in range(0, len(prg) - 20, 2):
        if word(prg, pool) == CELLS_VRAM and word(prg, pool + 12) == MAPS_VRAM:
            cells, table = to_file(word(prg, pool + 4)), to_file(word(prg, pool + 16))
            if cells is not None and table is not None:
                break
    else:
        raise ValueError("no literal pool with the cells' and maps' VRAM addresses in EPISODE3.PRG")
    maps = []
    while word(prg, table):
        at = to_file(word(prg, table))
        if at is None:
            raise ValueError(f"map list entry 0x{word(prg, table):08X} at 0x{table:X} is outside EPISODE3.PRG")
        maps.append(at)
        table += 4
    colours = []
    for address, count, before in COLOUR_COPIES:
        literal = pool - before
        at = to_file(word(prg, literal + 4)) if literal >= 0 and word(prg, literal) == address else None
        if at is None:
            raise ValueError(f"no colour RAM 0x{address:08X} source 0x{before:X} bytes before the literal pool at 0x{pool:X}")
        colours.append((address, count, at))
    return cells, maps, colours


def compare(label, ours, theirs):
    if ours == theirs:
        print(f"{label}: identical ({len(ours):#x} bytes)")
        return True
    first = next((i for i, (a, b) in enumerate(zip(ours, theirs)) if a != b), min(len(ours), len(theirs)))
    print(f"{label}: differs from byte {first:#x}")
    return False


def main(argv):
    if len(argv) == 2 and argv[1] in ("-h", "--help"):
        print(__doc__)
        return 0
    if len(argv) not in (3, 4) or argv[1].startswith("-"):
        print("plane_probe: error: usage: plane_probe.py [-h] <disc image> <out_dir> [<state_dir>]",
              file=sys.stderr)
        return 1
    out = Path(argv[2]).resolve()
    extracted = (Path(__file__).resolve().parent.parent / "extracted").resolve()
    if extracted not in out.parents and out != extracted:
        print(f"plane_probe: error: out_dir must be inside {extracted}", file=sys.stderr)
        return 1
    disc = DiscImage(argv[1])
    header = disc.header()
    prg = disc.file("EPISODE3.PRG")
    cells_at, maps_at, colours_at = find_sources(prg)
    print(f"{header['product']} {header['version']}: cells at 0x{cells_at:X}, "
          f"maps at {', '.join(f'0x{m:X}' for m in maps_at)}, "
          f"colours at {', '.join(f'0x{at:X}' for _, _, at in colours_at)}")

    vram, cram = bytearray(0x80000), bytearray(0x1000)
    cells, packed = unpack(prg, cells_at)
    print(f"cells: 0x{packed:X} packed bytes to 0x{len(cells):X}")
    base = CELLS_VRAM - VDP2_VRAM
    vram[base:base + len(cells)] = cells
    regions = [("cells", base, len(cells))]
    for i, at in enumerate(maps_at):
        page, packed = unpack(prg, at)
        print(f"map {i}: 0x{packed:X} packed bytes to 0x{len(page):X}")
        base = MAPS_VRAM - VDP2_VRAM + i * MAP_PAGE
        vram[base:base + len(page)] = page
        regions.append((f"map {i}", base, len(page)))
    for address, count, at in colours_at:
        base = address - VDP2_CRAM
        cram[base:base + count * 2] = prg[at:at + count * 2]
        regions.append((f"colours (CRAM 0x{base:X})", base, count * 2))

    same = True
    if len(argv) == 4:
        state = Path(argv[3])
        state_vram = (state / "VDP2_VRAM.bin").read_bytes()
        state_cram = (state / "VDP2_CRAM.bin").read_bytes()
        for label, start, size in regions:
            if label.startswith("colours"):
                same &= compare(label, bytes(cram[start:start + size]), state_cram[start:start + size])
            else:
                same &= compare(f"{label} (VRAM 0x{start:X})", bytes(vram[start:start + size]),
                                state_vram[start:start + size])

    regs = [0] * 256
    for offset, value in STATE_REGISTERS.items():
        regs[offset // 2] = value
    colours = struct.unpack(">2048H", bytes(cram))
    out.mkdir(parents=True, exist_ok=True)
    for which, label in enumerate("AB"):
        size, image = render_rbg0_plane(bytes(vram), colours, regs, which)
        write_png(out / f"rbg0_plane_{label}.png", size, size, image)
    print(f"wrote rbg0_plane_A.png and rbg0_plane_B.png to {out}")
    return 0 if same else 1


if __name__ == "__main__":
    try:
        sys.exit(main(sys.argv))
    except (OSError, ValueError, IndexError, struct.error) as e:
        print(f"plane_probe: error: {e}", file=sys.stderr)
        sys.exit(1)
