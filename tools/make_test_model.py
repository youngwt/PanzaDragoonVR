#!/usr/bin/env python3
"""Write a small self-contained test model, a .glb, for trying model loading in the headset.

    python3 tools/make_test_model.py my-assets/models/e03.flyer.glb

The model is a four-sided pyramid about 0.5 m across, made here and owing nothing to the disc.
Each face is a different colour of one embedded 16 x 16 PNG: the four sides red, yellow, green and
white, the base orange. It follows the asset contract's Models section: one file with its
texture inside it, nearest filtering, unlit, double-sided, one material named "opaque", the root
node named "flyer". It also carries vertex colours (COLOR_0) of pure blue, to show that a file
with them is accepted. Godot 4.7.2 does not apply them on its own, so this model cannot show
whether the game ignores them. Standard library only.
See tools/README.md.
"""
import argparse
import json
import os
import struct
import sys
import zlib

SIZE = 16
# One 8 x 8 block per face colour in a 16 x 16 atlas, as (column, row) of blocks.
RED, YELLOW, GREEN, WHITE = (0, 0), (1, 0), (0, 1), (1, 1)
COLOURS = {RED: (220, 40, 40), YELLOW: (240, 210, 40), GREEN: (40, 170, 60), WHITE: (240, 240, 240)}
ORANGE = (250, 140, 30)


def atlas_png():
    """The texture: four 8 x 8 blocks of colour, with an orange 4 x 4 square in the white one for the base."""
    rows = []
    for y in range(SIZE):
        row = bytearray([0])  # filter type: none
        for x in range(SIZE):
            colour = COLOURS[(x // 8, y // 8)]
            if 10 <= x < 14 and 10 <= y < 14:
                colour = ORANGE
            row += bytes(colour) + b"\xff"
        rows.append(bytes(row))

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))

    header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(b"".join(rows), 9))
            + chunk(b"IEND", b""))


def block_uv(block, inset=1):
    """The UV corners of a colour block, kept a pixel inside it: top-left, top-right, bottom-right, bottom-left."""
    left, top = block[0] * 8 + inset, block[1] * 8 + inset
    right, bottom = left + 8 - 2 * inset, top + 8 - 2 * inset
    return [(left / SIZE, top / SIZE), (right / SIZE, top / SIZE), (right / SIZE, bottom / SIZE), (left / SIZE, bottom / SIZE)]


def geometry():
    """Positions, UVs and indices of the pyramid; each face has its own vertices."""
    h, base, apex = 0.25, -0.2, 0.35
    corners = [(-h, base, h), (h, base, h), (h, base, -h), (-h, base, -h)]  # front-left, front-right, back-right, back-left
    top = (0.0, apex, 0.0)
    positions, uvs, indices = [], [], []
    for side, block in enumerate([RED, YELLOW, GREEN, WHITE]):
        a, b = corners[side], corners[(side + 1) % 4]
        uv = block_uv(block)
        start = len(positions)
        positions += [a, b, top]
        uvs += [uv[3], uv[2], ((uv[0][0] + uv[1][0]) / 2, uv[0][1])]
        indices += [start, start + 1, start + 2]
    # The base, a quad as the contract writes one: four vertices, then triangles (0, 1, 2) and (0, 2, 3).
    start = len(positions)
    positions += [corners[3], corners[2], corners[1], corners[0]]
    uvs += [(10.5 / SIZE, 10.5 / SIZE), (13.5 / SIZE, 10.5 / SIZE), (13.5 / SIZE, 13.5 / SIZE), (10.5 / SIZE, 13.5 / SIZE)]
    indices += [start, start + 1, start + 2, start, start + 2, start + 3]
    return positions, uvs, indices


def pad(data, fill):
    return data + fill * (-len(data) % 4)


def build():
    """The whole .glb file, as bytes."""
    positions, uvs, indices = geometry()
    count = len(positions)
    views = []
    binary = b""

    def add(data, target=None):
        nonlocal binary
        binary = pad(binary, b"\x00")
        view = {"buffer": 0, "byteOffset": len(binary), "byteLength": len(data)}
        if target is not None:
            view["target"] = target
        views.append(view)
        binary += data
        return len(views) - 1

    position_view = add(b"".join(struct.pack("<3f", *p) for p in positions), 34962)
    uv_view = add(b"".join(struct.pack("<2f", *uv) for uv in uvs), 34962)
    colour_view = add(struct.pack("<4f", 0.0, 0.0, 1.0, 1.0) * count, 34962)
    index_view = add(b"".join(struct.pack("<H", i) for i in indices), 34963)
    image_view = add(atlas_png())
    binary = pad(binary, b"\x00")

    gltf = {
        "asset": {"version": "2.0", "generator": "make_test_model.py"},
        "scene": 0,
        "scenes": [{"nodes": [0]}],
        "nodes": [{"name": "flyer", "mesh": 0}],
        "meshes": [{"name": "flyer", "primitives": [{
            "attributes": {"POSITION": 0, "TEXCOORD_0": 1, "COLOR_0": 2},
            "indices": 3, "material": 0, "mode": 4}]}],
        "materials": [{
            "name": "opaque",
            "doubleSided": True,
            "alphaMode": "OPAQUE",
            "pbrMetallicRoughness": {"baseColorTexture": {"index": 0}, "metallicFactor": 0.0, "roughnessFactor": 1.0},
            "extensions": {"KHR_materials_unlit": {}}}],
        "textures": [{"sampler": 0, "source": 0}],
        "samplers": [{"magFilter": 9728, "minFilter": 9728, "wrapS": 33071, "wrapT": 33071}],
        "images": [{"bufferView": image_view, "mimeType": "image/png"}],
        "accessors": [
            {"bufferView": position_view, "componentType": 5126, "count": count, "type": "VEC3",
             "min": [min(p[i] for p in positions) for i in range(3)],
             "max": [max(p[i] for p in positions) for i in range(3)]},
            {"bufferView": uv_view, "componentType": 5126, "count": count, "type": "VEC2"},
            {"bufferView": colour_view, "componentType": 5126, "count": count, "type": "VEC4"},
            {"bufferView": index_view, "componentType": 5123, "count": len(indices), "type": "SCALAR"},
        ],
        "bufferViews": views,
        "buffers": [{"byteLength": len(binary)}],
        "extensionsUsed": ["KHR_materials_unlit"],
    }
    assert position_view == 0 and uv_view == 1 and colour_view == 2 and index_view == 3

    text = pad(json.dumps(gltf, separators=(",", ":")).encode("utf-8"), b" ")
    body = (struct.pack("<II", len(text), 0x4E4F534A) + text
            + struct.pack("<II", len(binary), 0x004E4942) + binary)
    return struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body


def main(argv=None):
    parser = argparse.ArgumentParser(description="Write a small self-contained test model (.glb).")
    parser.add_argument("output", help="the .glb file to write, such as my-assets/models/e03.flyer.glb")
    args = parser.parse_args(argv)
    if not args.output.endswith(".glb"):
        print("make_test_model: error: the output file's name must end in .glb", file=sys.stderr)
        return 1

    data = build()
    folder = os.path.dirname(args.output)
    if folder:
        os.makedirs(folder, exist_ok=True)
    with open(args.output, "wb") as f:
        f.write(data)
    print("wrote %s (%d bytes)" % (args.output, len(data)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
