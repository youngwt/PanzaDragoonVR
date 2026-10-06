#!/usr/bin/env python3
"""Unpack a Mednafen Saturn save state and render its VDP2 rotating planes.

    python3 tools/mednafen_state.py <state.mc0> <out_dir>

Writes the gunzipped state (for Ghidra's Saturn loader), each memory region as
a big-endian .bin, the state's preview screenshot, and RBG0 plane A and B as
PNGs. In the Episode 3 forest, plane A is the floor and plane B is the canopy.
Only cell-format RBG0 planes of one page are rendered.
"""
import gzip
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from png import write_png

REGIONS = {  # (section, variable) -> output name; stored as host-endian 16-bit words
    ("MAIN", "WorkRAML"): "WorkRAML",
    ("MAIN", "WorkRAMH"): "WorkRAMH",
    ("VDP1", "VRAM"): "VDP1_VRAM",
    ("VDP2", "VRAM"): "VDP2_VRAM",
    ("VDP2", "CRAM"): "VDP2_CRAM",
}


def swap16(data):
    out = bytearray(data)
    out[0::2], out[1::2] = data[1::2], data[0::2]
    return bytes(out)


def parse(state):
    """Return (preview_w, preview_h, preview_rgb, {(section, variable): bytes})."""
    if state[:8] != b"MDFNSVST":
        raise ValueError("not a Mednafen save state")
    width, height = struct.unpack("<II", state[24:32])
    pos = 32 + width * height * 3
    preview = state[32:pos]
    variables = {}
    while pos < len(state):
        section = state[pos:pos + 32].split(b"\0")[0].decode("latin1")
        end = pos + 36 + struct.unpack("<I", state[pos + 32:pos + 36])[0]
        p = pos + 36
        while p < end:
            name_len = state[p]
            name = state[p + 1:p + 1 + name_len].decode("latin1")
            size = struct.unpack("<I", state[p + 1 + name_len:p + 5 + name_len])[0]
            variables[(section, name)] = state[p + 5 + name_len:p + 5 + name_len + size]
            p += 5 + name_len + size
        pos = end
    return width, height, preview, variables


def rgb555(word):
    return bytes(((word & 31) * 255 // 31, ((word >> 5) & 31) * 255 // 31, ((word >> 10) & 31) * 255 // 31, 255))


def render_rbg0_plane(vram, cram, regs, which):
    """Render one page of RBG0 rotation parameter A (which=0) or B (which=1)."""
    reg = lambda offset: regs[offset // 2]
    chctlb, pncr = reg(0x2A), reg(0x38)
    big_cells = bool(chctlb & 0x0100)          # 2x2 cells (16x16 pixels)
    colours_256 = (chctlb >> 12) & 7 == 1
    one_word, aux = bool(pncr & 0x8000), bool(pncr & 0x4000)
    supp, palette_supp = pncr & 0x1F, (pncr >> 5) & 7
    colour_offset = (reg(0xE6) & 7) << 8
    map_offset = (reg(0x3E) >> (4 * which)) & 7
    map_number = (map_offset << 6) | (reg(0x60 if which else 0x50) & 0x3F)
    cells, cell_px, entry = (32, 16, 2 if one_word else 4) if big_cells else (64, 8, 2 if one_word else 4)
    base = map_number * cells * cells * entry
    size = cells * cell_px
    image = bytearray(size * size * 4)
    for cy in range(cells):
        for cx in range(cells):
            a = (base + (cy * cells + cx) * entry) & 0x7FFFF
            if one_word:
                w = struct.unpack(">H", vram[a:a + 2])[0]
                if not aux:
                    flip = (w >> 10) & 3
                    char = ((w & 0x3FF) << 2 | supp & 3 | (supp & 0x1C) << 10) if big_cells else (w & 0x3FF | (supp & 0x1F) << 10)
                else:
                    flip = 0
                    char = ((w & 0xFFF) << 2 | supp & 3 | (supp & 0x10) << 10) if big_cells else (w & 0xFFF | (supp & 0x1C) << 10)
                palette = (w >> 12) & 7 if colours_256 else (w >> 12) & 0xF | palette_supp << 4
            else:
                w = struct.unpack(">I", vram[a:a + 4])[0]
                char, flip, palette = w & 0x7FFF, (w >> 30) & 3, (w >> 16) & 0x7F
            for sub in range(4 if big_cells else 1):
                ca = (char + sub * (2 if colours_256 else 1)) * 0x20
                for y in range(8):
                    for x in range(8):
                        if colours_256:
                            colour = palette << 8 | vram[(ca + y * 8 + x) & 0x7FFFF]
                        else:
                            byte = vram[(ca + y * 4 + x // 2) & 0x7FFFF]
                            colour = palette << 4 | (byte & 15 if x & 1 else byte >> 4)
                        px, py = (sub & 1) * 8 + x, (sub >> 1) * 8 + y
                        if flip & 1:
                            px = cell_px - 1 - px
                        if flip & 2:
                            py = cell_px - 1 - py
                        o = ((cy * cell_px + py) * size + cx * cell_px + px) * 4
                        image[o:o + 4] = rgb555(cram[(colour + colour_offset) & 0x7FF])
    return size, bytes(image)


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 1
    out = Path(argv[2])
    out.mkdir(parents=True, exist_ok=True)
    raw = Path(argv[1]).read_bytes()
    state = gzip.decompress(raw) if raw[:2] == b"\x1f\x8b" else raw
    (out / "state.mc").write_bytes(state)
    width, height, preview, variables = parse(state)
    rgba = b"".join(preview[i:i + 3] + b"\xff" for i in range(0, len(preview), 3))
    write_png(out / "preview.png", width, height, rgba)
    for key, name in REGIONS.items():
        (out / f"{name}.bin").write_bytes(swap16(variables[key]))
    regs = struct.unpack("<256H", variables[("VDP2", "RawRegs")])
    vram = swap16(variables[("VDP2", "VRAM")])
    cram = struct.unpack(">2048H", swap16(variables[("VDP2", "CRAM")]))
    for which, label in enumerate("AB"):
        size, image = render_rbg0_plane(vram, cram, regs, which)
        write_png(out / f"rbg0_plane_{label}.png", size, size, image)
        print(f"plane {label}: {size}x{size}")
    print(f"wrote state, memory regions, preview and planes to {out}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
