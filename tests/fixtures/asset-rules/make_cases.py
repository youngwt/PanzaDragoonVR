#!/usr/bin/env python3
"""Write the shared asset-rule cases: asset folders that both test suites check.

    python3 tests/fixtures/asset-rules/make_cases.py [output-folder]

Each case is a folder holding a manifest.json, the small files it lists, and expected.json:

    {"problems": [...]}                  every problem line, in order, as the game's launch
                                         report gives them and write_manifest.py --check must too
    {"unreadable_manifest": true, ...}   the manifest cannot be read at all; only the start of
                                         the one line, "manifest.json: is not valid JSON", is
                                         compared, as the parsers' own detail differs
    "whole_line": true                   with "unreadable_manifest": the whole of the one line
                                         is compared, as for a manifest that lists no assets
    "pc_notices": [...]                  optional: notices only write_manifest.py gives

tests/tools/test_asset_rules.py runs the cases through tools/manifest.py, and
tests/GameLogic.Tests/AssetRulesTests.cs through the game's AssetManifest. Every file here is
made by this script and owes nothing to the disc. To change or add a case, edit CASES below
and run the script again; with no argument it rewrites the case folders beside it. Running it
twice gives the same bytes. Standard library only.
"""
import json
import os
import shutil
import struct
import sys
import zlib
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parents[2] / "tools"))
import make_test_model  # noqa: E402

FLOOR, CANOPY, FLYER = "e03.floor", "e03.canopy", "e03.flyer"
FLOOR_PATH, CANOPY_PATH, FLYER_PATH = "images/e03.floor.png", "images/e03.canopy.png", "models/e03.flyer.glb"
NOT_JSON = "manifest.json: is not valid JSON"
ID_RULE = "an ID is lowercase letters, digits and underscores in levels separated by single dots"


def png(depth=8, colour=6, size=2):
    """A small PNG of the given bit depth and colour type."""
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))
    channels = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}[colour]
    row = b"\x00" + bytes(range(size * channels * depth // 8))
    header = struct.pack(">IIBBBBB", size, size, depth, colour, 0, 0, 0)
    body = chunk(b"IHDR", header)
    if colour == 3:
        body += chunk(b"PLTE", bytes(range(3 * size)))
    return b"\x89PNG\r\n\x1a\n" + body + chunk(b"IDAT", zlib.compress(row * size, 9)) + chunk(b"IEND", b"")


def glb(gltf, binary=b"", version=2, length_change=0):
    """A .glb of the given glTF JSON and binary chunk."""
    text = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    text += b" " * (-len(text) % 4)
    body = struct.pack("<II", len(text), 0x4E4F534A) + text
    if binary:
        binary += b"\x00" * (-len(binary) % 4)
        body += struct.pack("<II", len(binary), 0x004E4942) + binary
    return struct.pack("<III", 0x46546C67, version, 12 + len(body) + length_change) + body


def model(**changes):
    """A minimal model the game accepts: one node using one mesh. `changes` are set at the top
    level of its JSON; a value of None removes that field."""
    gltf = {"asset": {"version": "2.0"}, "scenes": [{"nodes": [0]}], "nodes": [{"name": "flyer", "mesh": 0}],
            "meshes": [{"name": "flyer", "primitives": [{"attributes": {"POSITION": 0}}]}]}
    for field, value in changes.items():
        if value is None:
            gltf.pop(field, None)
        else:
            gltf[field] = value
    return gltf


def entry(asset_id, file_path, data, kind="image", **changes):
    """A manifest entry for a file of these bytes; `changes` replace fields, None removes one."""
    item = {"id": asset_id, "kind": kind, "path": file_path, "bytes": len(data)}
    for field, value in changes.items():
        if value is None:
            item.pop(field, None)
        else:
            item[field] = value
    return item


def document(assets):
    return {"format": 1, "generator": "make_cases.py", "source": None, "assets": assets}


def as_json(value):
    """JSON text as the cases are written: ASCII only, so a lone surrogate is written as an escape."""
    return json.dumps(value, indent=2) + "\n"


def one_file(file_path, data, kind="image", asset_id=FLOOR, **changes):
    """A case with one file and its entry; `changes` are made to the entry, as in entry()."""
    return {"files": {file_path: data}, "manifest": document([entry(asset_id, file_path, data, kind, **changes)])}


def model_case(data):
    return one_file(FLYER_PATH, data, kind="model", asset_id=FLYER)


def nested(levels):
    """A good manifest whose deepest array or object is `levels` deep, through an entry's meta."""
    meta = {}
    for _ in range(levels - 4):  # the manifest, 'assets', the entry and 'meta' make four
        meta = {"a": meta}
    data = png()
    return {"files": {FLOOR_PATH: data}, "manifest": document([entry(FLOOR, FLOOR_PATH, data, meta=meta)])}


def unreadable(text):
    """A case whose manifest.json is `text` and cannot be read."""
    return {"files": {FLOOR_PATH: png()}, "manifest_text": text, "unreadable_manifest": True, "problems": [NOT_JSON]}


def with_bytes_value(value):
    data = png()
    return json.dumps(document([entry(FLOOR, FLOOR_PATH, data)]), indent=2).replace(
        '"bytes": %d' % len(data), '"bytes": %s' % value) + "\n"


def cases():
    """Every case, by folder name: its files, its manifest and the problems expected."""
    test_model = make_test_model.build()
    floor, canopy = png(colour=6), png(colour=2)
    found = {}

    def case(name, spec, problems=()):
        spec.setdefault("problems", list(problems))
        found[name] = spec

    # A good folder: two images and the test model.
    case("good-folder", {
        "files": {FLOOR_PATH: floor, CANOPY_PATH: canopy, FLYER_PATH: test_model},
        "manifest": document([entry(FLOOR, FLOOR_PATH, floor), entry(CANOPY, CANOPY_PATH, canopy),
                              entry(FLYER, FLYER_PATH, test_model, kind="model")])})

    # Models the game rejects: the contract's "When a model is invalid" table.
    where = "%s: %s: " % (FLYER, FLYER_PATH)
    case("model-external-image", model_case(glb(model(images=[{"uri": "../../etc/passwd"}]))),
         [where + "not self-contained: image 0 names the file '../../etc/passwd', which was not opened"])
    case("model-external-buffer", model_case(glb(model(buffers=[{"uri": "flyer.bin", "byteLength": 4}]))),
         [where + "not self-contained: buffer 0 names the file 'flyer.bin', which was not opened"])
    case("model-data-uri", model_case(glb(model(images=[{"uri": "data:image/png;base64,AAAA"}]))),
         [where + "not self-contained: image 0 has a data: URI"])
    case("model-extension-required", model_case(glb(model(extensionsUsed=["KHR_draco_mesh_compression"],
                                                          extensionsRequired=["KHR_draco_mesh_compression"]))),
         [where + "unsupported model: it requires the extension KHR_draco_mesh_compression"])
    short = glb(model(), length_change=8)
    case("model-wrong-length", model_case(short),
         [where + "unreadable model: the header gives a length of %d bytes but the file is %d bytes"
          % (len(short) + 8, len(short))])
    case("model-no-mesh", model_case(glb(model(meshes=None, nodes=[{"name": "flyer"}]))),
         [where + "empty model: it has no mesh"])
    case("model-no-node-uses-a-mesh", model_case(glb(model(nodes=[{"name": "flyer"}]))),
         [where + "empty model: no node uses any of its meshes"])
    case("model-glb-version-1", model_case(glb(model(), version=1)),
         [where + "unreadable model: a .glb of glTF version 1; it must be version 2"])
    case("model-gltf-version-1", model_case(glb(model(asset={"version": "1.0"}))),
         [where + "unreadable model: glTF version '1.0'; it must be 2.0"])
    case("model-not-a-glb", model_case(b"not a model, only some text"),
         [where + "unreadable model: not a .glb file: no glTF header"])

    # Manifests neither side can read.
    case("unreadable-nan", unreadable(with_bytes_value("NaN")))
    case("unreadable-infinity", unreadable(with_bytes_value("Infinity")))
    case("unreadable-minus-infinity", unreadable(with_bytes_value("-Infinity")))
    case("unreadable-nested-65-deep", unreadable(as_json(nested(65)["manifest"])))
    case("unreadable-not-json", unreadable('{"format": 1, "assets": [\n'))
    # ...and the deepest manifest both can read.
    case("nested-64-deep", nested(64))
    # An empty asset list is a problem, the manifest's as a whole (author, 2026-10-16).
    case("empty-asset-list", {"files": {}, "manifest": document([]), "unreadable_manifest": True, "whole_line": True},
         ["manifest.json: lists no assets"])

    # A lone surrogate escape: an invalid entry in 'id' or 'path', a kind nobody knows in 'kind'.
    case("lone-surrogate-in-id", one_file(FLOOR_PATH, floor, id="e03.\ud800"),
         ["entry 1: invalid entry: field 'id' is not readable text: it holds a lone surrogate escape"])
    case("lone-surrogate-in-path", one_file(FLOOR_PATH, floor, path="images/\ud800.png"),
         [FLOOR + ": invalid entry: field 'path' is not readable text: it holds a lone surrogate escape"])
    found_kind = one_file(FLOOR_PATH, floor, kind="\ud800")
    found_kind["pc_notices"] = ["%s: %s: unknown kind '\"\\ud800\"'; its contents were not checked" % (FLOOR, FLOOR_PATH)]
    case("lone-surrogate-in-kind", found_kind)

    # The entry rules both sides apply, one rule a case.
    case("entry-not-an-object", {"files": {}, "manifest": document([FLOOR])},
         ["entry 1: must be an object"])
    case("field-absent", one_file(FLOOR_PATH, floor, bytes=None),
         [FLOOR + ": invalid entry: required field 'bytes' is absent"])
    case("field-id-absent", one_file(FLOOR_PATH, floor, id=None),
         ["entry 1: invalid entry: required field 'id' is absent"])
    case("field-wrong-type", one_file(FLOOR_PATH, floor, bytes="%d" % len(floor)),
         [FLOOR + ": invalid entry: field 'bytes' must be an integer"])
    case("field-not-an-integer", one_file(FLOOR_PATH, floor, bytes=float(len(floor))),
         [FLOOR + ": invalid entry: field 'bytes' must be an integer"])
    case("fields-several-wrong", one_file(FLOOR_PATH, floor, kind=3, bytes=None),
         [FLOOR + ": invalid entry: field 'kind' must be a string; required field 'bytes' is absent"])
    case("bad-id", one_file(FLOOR_PATH, floor, id="E03.Floor"),
         ["E03.Floor: invalid entry: not a valid ID: " + ID_RULE])
    case("bytes-negative", one_file(FLOOR_PATH, floor, bytes=-1),
         [FLOOR + ": invalid entry: 'bytes' is negative"])
    case("bad-path-backslash", one_file(FLOOR_PATH, floor, path="images\\e03.floor.png"),
         [FLOOR + ": invalid entry: invalid path: a path must use forward slashes, not backslashes"])
    case("bad-path-characters", one_file(FLOOR_PATH, floor, path="Images/e03.floor.png"),
         [FLOOR + ": invalid entry: invalid path: a path may hold only lowercase letters, digits, underscores, hyphens and dots"])
    case("bad-path-empty-part", one_file(FLOOR_PATH, floor, path="images//e03.floor.png"),
         [FLOOR + ": invalid entry: invalid path: a path must not contain empty parts"])
    case("path-leaves-the-folder", one_file(FLOOR_PATH, floor, path="../e03.floor.png"),
         [FLOOR + ": invalid entry: invalid path: a path must not contain '.' or '..' parts"])
    case("path-from-the-root", one_file(FLOOR_PATH, floor, path="/etc/e03.floor.png"),
         [FLOOR + ": invalid entry: invalid path: a path must be relative to the asset folder, with no leading slash"])
    case("file-missing", {"files": {}, "manifest": document([entry(FLOOR, FLOOR_PATH, floor)])},
         ["%s: %s: missing" % (FLOOR, FLOOR_PATH)])
    # Git keeps no empty folder, so the folder in the file's place holds a file of its own.
    case("path-is-a-folder", {"files": {FLOOR_PATH + "/keep.txt": b"a folder, not a file\n"},
                              "manifest": document([entry(FLOOR, FLOOR_PATH, floor)])},
         ["%s: %s: missing: the path is not a file" % (FLOOR, FLOOR_PATH)])
    case("wrong-size", one_file(FLOOR_PATH, floor, bytes=len(floor) + 5),
         ["%s: %s: incomplete or changed: the file is %d bytes, the manifest says %d"
          % (FLOOR, FLOOR_PATH, len(floor), len(floor) + 5)])
    case("image-not-a-png", one_file(FLOOR_PATH, b"GIF89a, not a PNG at all, padded out a bit"),
         ["%s: %s: unreadable image: not a PNG file" % (FLOOR, FLOOR_PATH)])
    case("image-16-bit", one_file(FLOOR_PATH, png(depth=16)),
         ["%s: %s: unreadable image: a 16-bit RGBA PNG; it must be 8-bit RGB or RGBA" % (FLOOR, FLOOR_PATH)])
    case("image-palette", one_file(FLOOR_PATH, png(colour=3)),
         ["%s: %s: unreadable image: a 8-bit palette PNG; it must be 8-bit RGB or RGBA" % (FLOOR, FLOOR_PATH)])
    case("image-greyscale", one_file(FLOOR_PATH, png(colour=0)),
         ["%s: %s: unreadable image: a 8-bit greyscale PNG; it must be 8-bit RGB or RGBA" % (FLOOR, FLOOR_PATH)])
    other = "images/other.png"
    case("duplicate-id", {"files": {FLOOR_PATH: floor, other: canopy},
                          "manifest": document([entry(FLOOR, FLOOR_PATH, floor), entry(FLOOR, other, canopy)])},
         ["%s: duplicate ID, listed for both %s and %s" % (FLOOR, FLOOR_PATH, other)])
    # A problem with one entry leaves the others alone, and every problem is listed:
    # the entries' own problems first, then each file's, in manifest order.
    case("several-problems", {
        "files": {CANOPY_PATH: canopy, FLYER_PATH: glb(model(meshes=None, nodes=[]))},
        "manifest": document([entry(FLOOR, FLOOR_PATH, floor), entry(CANOPY, CANOPY_PATH, canopy, bytes=1),
                              entry("e03.sky", "../sky.png", floor), entry(FLYER, FLYER_PATH, glb(model(meshes=None, nodes=[])), kind="model")])},
         ["e03.sky: invalid entry: invalid path: a path must not contain '.' or '..' parts",
          "%s: %s: missing" % (FLOOR, FLOOR_PATH),
          "%s: %s: incomplete or changed: the file is %d bytes, the manifest says 1" % (CANOPY, CANOPY_PATH, len(canopy)),
          "%s: %s: empty model: it has no mesh" % (FLYER, FLYER_PATH)])
    return found


def write(output):
    """Write every case folder into `output`, replacing what was there."""
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    found = cases()
    for old in sorted(output.iterdir()):
        # A case folder this script no longer writes goes; anything else here is left alone.
        if old.is_dir() and (old / "expected.json").is_file() and old.name not in found:
            shutil.rmtree(old)
    for name, spec in found.items():
        folder = output / name
        if folder.exists():
            shutil.rmtree(folder)
        folder.mkdir()
        for path, data in spec["files"].items():
            target = folder / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        text = spec["manifest_text"] if "manifest_text" in spec else as_json(spec["manifest"])
        (folder / "manifest.json").write_bytes(text.encode("ascii"))
        expected = {}
        if spec.get("unreadable_manifest"):
            expected["unreadable_manifest"] = True
            if spec.get("whole_line"):
                expected["whole_line"] = True
        expected["problems"] = spec["problems"]
        if "pc_notices" in spec:
            expected["pc_notices"] = spec["pc_notices"]
        (folder / "expected.json").write_bytes(as_json(expected).encode("ascii"))
    return sorted(found)


def main(argv):
    if len(argv) > 2 or (len(argv) == 2 and argv[1].startswith("-")):
        print(__doc__.split("\n\n")[1], file=sys.stderr)
        return 1
    names = write(argv[1] if len(argv) == 2 else HERE)
    print("wrote %d cases" % len(names))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
