#!/usr/bin/env python3
"""Decode textures from a Panzer Dragoon Zwei .CGB file using its .MDB model file.

    python3 tools/cgb_textures.py <disc_dir> <out_dir> [NAME ...]

A .CGB is raw VDP1 pixel data with no header. The paired .MDB holds, per polygon,
four big-endian words in VDP1 command order: CMDPMOD, CMDCOLR, CMDSRCA, CMDSIZE.
CMDSRCA and CMDCOLR are VRAM addresses divided by 8, relative to wherever the
game loads the .CGB, so the load address is inferred from the records themselves.
Only colour mode 1 (16 colours via a lookup table stored inside the .CGB) is
decoded in colour; other modes need a palette from elsewhere and come out grey.
"""
import struct
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from png import write_png


def find_records(mdb):
    """Yield (offset, pmod, colr, srca, width, height) for plausible texture records."""
    for o in range(0, len(mdb) - 8, 2):
        pmod, colr, srca, size = struct.unpack(">4H", mdb[o:o + 8])
        if pmod & 0xFF00 != 0x0400 or size >> 14:
            continue
        width, height = (size >> 8) * 8, size & 0xFF
        if width and height:
            yield o, pmod, colr, srca, width, height


def colour_mode(pmod):
    return (pmod >> 3) & 7


def bytes_needed(mode, width, height):
    return width * height // 2 if mode in (0, 1) else width * height * (2 if mode == 5 else 1)


def infer_base(records, cgb_size):
    """Pick the load address (in units of 8 bytes) that makes the most records fit the file."""
    best, best_score = None, -1
    for base in sorted({r[3] for r in records}):
        score = 0
        for _, pmod, colr, srca, w, h in records:
            off = (srca - base) * 8
            if off < 0 or off + bytes_needed(colour_mode(pmod), w, h) > cgb_size:
                continue
            if colour_mode(pmod) == 1 and not 0 <= (colr - base) * 8 <= cgb_size - 32:
                continue
            score += 1
        if score > best_score:
            best, best_score = base, score
    return best, best_score


def rgb555(word):
    r, g, b = word & 31, (word >> 5) & 31, (word >> 10) & 31
    return bytes((r * 255 // 31, g * 255 // 31, b * 255 // 31))


def decode(cgb, base, pmod, colr, srca, width, height):
    mode, off = colour_mode(pmod), (srca - base) * 8
    transparent = not pmod & 0x40
    out = bytearray()
    if mode in (0, 1):
        if mode == 1:
            lut = (colr - base) * 8
            palette = [rgb555(w) for w in struct.unpack(">16H", cgb[lut:lut + 32])]
        else:
            palette = [bytes((i * 17,) * 3) for i in range(16)]
        for byte in cgb[off:off + width * height // 2]:
            for index in (byte >> 4, byte & 15):
                out += palette[index] + bytes((0 if transparent and index == 0 else 255,))
    elif mode == 5:
        for (word,) in struct.iter_unpack(">H", cgb[off:off + width * height * 2]):
            out += rgb555(word) + bytes((255 if word & 0x8000 else 0,))
    else:
        for index in cgb[off:off + width * height]:
            out += bytes((index, index, index, 0 if transparent and index == 0 else 255))
    return bytes(out)


def contact_sheet(textures, columns=8, pad=2):
    cell_w = max(t[0] for t in textures) + pad
    cell_h = max(t[1] for t in textures) + pad
    rows = (len(textures) + columns - 1) // columns
    sheet_w, sheet_h = cell_w * columns, cell_h * rows
    sheet = bytearray(b"\x30\x30\x30\xff" * (sheet_w * sheet_h))
    for i, (w, h, rgba) in enumerate(textures):
        x0, y0 = (i % columns) * cell_w, (i // columns) * cell_h
        for y in range(h):
            start = ((y0 + y) * sheet_w + x0) * 4
            sheet[start:start + w * 4] = rgba[y * w * 4:(y + 1) * w * 4]
    return sheet_w, sheet_h, bytes(sheet)


def process(disc, out, name):
    mdb, cgb = (disc / f"{name}.MDB").read_bytes(), (disc / f"{name}.CGB").read_bytes()
    records = list(find_records(mdb))
    if not records:
        print(f"{name}: no texture records found")
        return
    base, _ = infer_base(records, len(cgb))
    unique = {}
    for _, pmod, colr, srca, w, h in records:
        off = (srca - base) * 8
        mode = colour_mode(pmod)
        if off < 0 or off + bytes_needed(mode, w, h) > len(cgb):
            continue
        if mode == 1 and not 0 <= (colr - base) * 8 <= len(cgb) - 32:
            continue
        unique.setdefault((srca, w, h, mode, colr if mode == 1 else 0), (pmod, colr))
    target = out / name
    target.mkdir(parents=True, exist_ok=True)
    textures = []
    for (srca, w, h, mode, _), (pmod, colr) in sorted(unique.items()):
        rgba = decode(cgb, base, pmod, colr, srca, w, h)
        write_png(target / f"{(srca - base) * 8:06x}_{w}x{h}_m{mode}.png", w, h, rgba)
        textures.append((w, h, rgba))
    write_png(out / f"{name}_sheet.png", *contact_sheet(textures))
    modes = Counter(k[3] for k in unique)
    print(f"{name}: base {base * 8:#x}, {len(unique)} textures, by colour mode {dict(sorted(modes.items()))}")


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 1
    disc, out = Path(argv[1]), Path(argv[2])
    names = argv[3:] or sorted(p.stem for p in disc.glob("*.MDB") if (disc / f"{p.stem}.CGB").exists())
    for name in names:
        process(disc, out, name)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
