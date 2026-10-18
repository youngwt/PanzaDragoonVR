"""Build and check an asset folder's manifest.json, to the asset contract.

The contract is _bmad-output/initiative-panzer-dragoon-zwei-vr/
spec-panzer-dragoon-zwei-vr/asset-contract.md. Every rule here is one it states.

    scan(folder)            the assets found by file name, and the files left out
    build(folder, pairs)    the manifest as a dict; raises ManifestError
    check(folder)           a list of problems, empty when the folder is good

This is a module; the command is write_manifest.py.
"""
import hashlib
import json
import os
import re
import struct

FORMAT = 1
MANIFEST_NAME = "manifest.json"
GENERATOR = "write_manifest.py 0.1"
# An empty 'assets' list is a problem, in the game's words (AssetManifest.NoAssets).
NO_ASSETS = "%s: lists no assets" % MANIFEST_NAME

# Extension to kind. See Kinds in the contract.
KINDS = {".png": "image", ".glb": "model"}
SUPPORTED = "supported kinds are .png (image) and .glb (model)"

ID_RULE = "an ID is lowercase letters, digits and underscores in levels separated by single dots"
_ID = re.compile(r"[a-z0-9_]+(\.[a-z0-9_]+)*")
_PATH_CHARS = re.compile(r"[a-z0-9_.\-/]*")
_SHA256 = re.compile(r"[0-9a-f]{64}")

_PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
_PNG_COLOUR_TYPES = {0: "greyscale", 2: "RGB", 3: "palette", 4: "greyscale with alpha", 6: "RGBA"}


class ManifestError(Exception):
    """One or more reasons a manifest cannot be built. `problems` has one line each."""

    def __init__(self, problems):
        self.problems = [problems] if isinstance(problems, str) else list(problems)
        super().__init__("; ".join(self.problems))


def id_problem(asset_id):
    """Why `asset_id` is not a valid ID, or None."""
    if not isinstance(asset_id, str) or not _ID.fullmatch(asset_id):
        return ID_RULE
    return None


def path_problem(path):
    """Why `path` breaks the contract's path rules, or None."""
    if not isinstance(path, str) or path == "":
        return "a path must not be empty"
    if "\\" in path:
        return "a path must use forward slashes, not backslashes"
    if path.startswith("/"):
        return "a path must be relative to the asset folder, with no leading slash"
    parts = path.split("/")
    if ".." in parts or "." in parts:
        return "a path must not contain '.' or '..' parts"
    if "" in parts:
        return "a path must not contain empty parts"
    if not _PATH_CHARS.fullmatch(path):
        return "a path may hold only lowercase letters, digits, underscores, hyphens and dots"
    return None


def kind_of(path):
    """The kind a path's extension stands for, or None."""
    return KINDS.get(os.path.splitext(path)[1])


def _inside(folder, path):
    root = os.path.realpath(folder)
    real = os.path.realpath(os.path.join(root, path))
    return real != root and os.path.commonpath([root, real]) == root


def png_problem(head):
    """Why the first bytes of a file are not an 8-bit RGB or RGBA PNG, or None. As the game words it."""
    if len(head) < 26 or head[:8] != _PNG_SIGNATURE or head[12:16] != b"IHDR":
        return "not a PNG file"
    depth, colour = head[24], head[25]
    if depth != 8 or colour not in (2, 6):
        name = _PNG_COLOUR_TYPES.get(colour, "colour type %d" % colour)
        return "a %d-bit %s PNG; it must be 8-bit RGB or RGBA" % (depth, name)
    return None


def contents_problem(file_path, kind):
    """Why the file cannot be used as `kind`, as the game's launch report words it after
    "<id>: <path>: ", or None. A model is checked by the contract's invalid-model table
    (glb_problem); an image by its PNG header. Other kinds are not checked."""
    if kind == "image":
        with open(file_path, "rb") as f:
            reason = png_problem(f.read(26))
        return None if reason is None else "unreadable image: %s" % reason
    if kind == "model":
        with open(file_path, "rb") as f:
            return glb_problem(f.read())
    return None


# The game reads JSON with System.Text.Json's JsonDocument, which allows at most this many
# nested arrays and objects and has no NaN or Infinity. Python's json allows both.
MAX_JSON_DEPTH = 64


def _no_constant(name):
    raise ValueError("%s is not a JSON value" % name)


def _depth(value):
    """How many arrays and objects are nested in `value`, the outermost counting as 1."""
    deepest = 0
    pending = [(value, 1)]
    while pending:
        item, level = pending.pop()
        if isinstance(item, dict):
            children = item.values()
        elif isinstance(item, list):
            children = item
        else:
            continue
        deepest = max(deepest, level)
        pending.extend((child, level + 1) for child in children)
    return deepest


def loads_strict(text):
    """json.loads, refusing what the game's JSON reader refuses: NaN, Infinity and -Infinity,
    and nesting deeper than MAX_JSON_DEPTH. Raises ValueError, or RecursionError when nested
    far too deeply to parse at all."""
    value = json.loads(text, parse_constant=_no_constant)
    if _depth(value) > MAX_JSON_DEPTH:
        raise ValueError("it is nested more than %d levels deep" % MAX_JSON_DEPTH)
    return value


def readable(value):
    """A JSON string's text, or None when it is not a string or holds a lone surrogate escape
    such as "\\ud800", which the game cannot read as text."""
    if not isinstance(value, str):
        return None
    try:
        value.encode("utf-8")
    except UnicodeEncodeError:
        return None
    return value


class _Unreadable(Exception):
    """A string in a model's JSON that the game cannot read, as its GlbCheck finds it."""


def _string(value):
    text = readable(value)
    if text is None:
        raise _Unreadable()
    return text


def _array_of(root, field):
    value = root.get(field)
    return value if isinstance(value, list) else []


def _is_control(c):
    return ord(c) < 0x20 or 0x7F <= ord(c) <= 0x9F


def _outside(item, what):
    """What a buffer or image refers to outside the file, or None when it has no uri."""
    if not isinstance(item, dict) or "uri" not in item:
        return None
    uri = item["uri"]
    if not isinstance(uri, str):
        return "%s has a uri; nothing it names was opened" % what
    text = _string(uri)
    if text[:5].isascii() and text[:5].lower() == "data:":
        return "%s has a data: URI" % what
    # Shortened as the game shortens it for the board, counting UTF-16 code units as C# does.
    units = text.encode("utf-16-le")
    shown = text if len(units) <= 120 else units[:114].decode("utf-16-le", "replace") + "..."
    shown = "".join(" " if _is_control(c) else c for c in shown)
    return "%s names the file '%s', which was not opened" % (what, shown)


_GLB_MAGIC = 0x46546C67  # "glTF"
_JSON_CHUNK = 0x4E4F534A  # "JSON"
_BIN_CHUNK = 0x004E4942   # "BIN\0"


def glb_problem(data):
    """Why a .glb file's bytes fall under the asset contract's "When a model is invalid" table,
    or None. The same checks, in the same order and words, as the game's GlbCheck
    (game/Logic/GlbCheck.cs): "unreadable model: ...", "not self-contained: ...",
    "unsupported model: ..." or "empty model: ...". Nothing the model names is opened."""
    def unreadable(detail):
        return "unreadable model: %s" % detail

    if len(data) < 12 or struct.unpack_from("<I", data, 0)[0] != _GLB_MAGIC:
        return unreadable("not a .glb file: no glTF header")
    version, declared = struct.unpack_from("<II", data, 4)
    if version != 2:
        return unreadable("a .glb of glTF version %d; it must be version 2" % version)
    if declared != len(data):
        return unreadable("the header gives a length of %d bytes but the file is %d bytes" % (declared, len(data)))
    if len(data) < 20:
        return unreadable("the file has no JSON chunk")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    if json_type != _JSON_CHUNK:
        return unreadable("the first chunk is not JSON")
    json_end = 20 + json_length
    if json_end > len(data):
        return unreadable("the JSON chunk runs past the end of the file")
    bin_length = None
    if len(data) - json_end >= 8:
        length, kind = struct.unpack_from("<II", data, json_end)
        if json_end + 8 + length > len(data):
            return unreadable("the chunk after the JSON runs past the end of the file")
        if kind == _BIN_CHUNK:
            bin_length = length

    try:
        # Bytes that are not UTF-8 become lone surrogates, so a string holding them cannot be
        # read, as in the game; outside a string they are not JSON.
        root = loads_strict(data[20:json_end].decode("utf-8", "surrogateescape"))
    except (ValueError, RecursionError):
        return unreadable("the JSON chunk is not valid JSON")
    try:
        return _glb_json_problem(root, bin_length)
    except _Unreadable:
        return unreadable("the JSON chunk could not be read")


def _glb_json_problem(root, bin_length):
    if not isinstance(root, dict):
        return "unreadable model: the JSON chunk is not one JSON object"
    asset = root.get("asset")
    if not isinstance(asset, dict) or not isinstance(asset.get("version"), str):
        return "unreadable model: it has no 'asset' object with a 'version'"
    version = _string(asset["version"])
    if version.split(".")[0] != "2":
        return "unreadable model: glTF version '%s'; it must be 2.0" % version

    # A reference outside the file is found before anything else.
    buffers, images = _array_of(root, "buffers"), _array_of(root, "images")
    for what, items in (("buffer", buffers), ("image", images)):
        for i, item in enumerate(items):
            outside = _outside(item, "%s %d" % (what, i))
            if outside:
                return "not self-contained: %s" % outside

    # With no uri, a buffer can only be the file's own binary chunk, and only the first buffer can be.
    for i, buffer in enumerate(buffers):
        if i > 0:
            return "unreadable model: buffer %d has no uri, and only buffer 0 can be the file's binary chunk" % i
        if bin_length is None:
            return "unreadable model: buffer 0 is the file's binary chunk, but the file has none"
        if not isinstance(buffer, dict):
            raise _Unreadable()
        length = buffer.get("byteLength")
        if "byteLength" in buffer and (isinstance(length, bool) or not isinstance(length, (int, float))):
            raise _Unreadable()  # the game cannot read a number from it at all
        if not _is_int(length) or not 0 <= length < 2 ** 63:
            return "unreadable model: buffer 0 has no valid 'byteLength'"
        if length > bin_length:
            return "unreadable model: buffer 0 is %d bytes but the binary chunk holds %d" % (length, bin_length)

    required = root.get("extensionsRequired")
    if isinstance(required, list) and required:
        names = ", ".join(_string(name) if isinstance(name, str) else json.dumps(name, ensure_ascii=False)
                          for name in required)
        noun = "extension" if len(required) == 1 else "extensions"
        return "unsupported model: it requires the %s %s" % (noun, names)

    if not _array_of(root, "meshes"):
        return "empty model: it has no mesh"
    if not any(isinstance(node, dict) and "mesh" in node for node in _array_of(root, "nodes")):
        return "empty model: no node uses any of its meshes"
    return None


def sha256_of(file_path):
    digest = hashlib.sha256()
    with open(file_path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def scan(folder):
    """Find the folder's assets by file name: the name without its extension is the ID.

    Returns (found, skipped). `found` is a list of (id, path); `skipped` is a list
    of (path, reason) for files that are not assets. The manifest itself is in neither.
    Paths are relative to the folder, with forward slashes, and are not yet validated.
    Raises ManifestError if a folder cannot be listed.
    """
    found, skipped = [], []

    def unreadable(error):
        raise ManifestError("%s: folder cannot be read: %s" % (error.filename, error.strerror or error))

    for here, dirs, files in os.walk(folder, onerror=unreadable):
        dirs.sort()
        for name in sorted(files):
            full = os.path.join(here, name)
            path = os.path.relpath(full, folder).replace(os.sep, "/")
            if path == MANIFEST_NAME:
                continue
            stem, extension = os.path.splitext(name)
            if extension not in KINDS:
                skipped.append((path, "not a .png or .glb file"))
            elif id_problem(stem):
                skipped.append((path, "its name is not a valid ID"))
            else:
                found.append((stem, path))
    return found, skipped


def build(folder, pairs=(), skipped=None, generator=GENERATOR):
    """Return the manifest for `folder` as a dict.

    The folder is scanned; `pairs` is (id, path) for files not named after their ID.
    A file named by a pair is listed under the pair's ID only. If `skipped` is a list,
    the (path, reason) of each file left out is appended to it.
    Raises ManifestError listing every problem; nothing is written here either way.
    """
    if not os.path.isdir(folder):
        raise ManifestError("%s: not a folder" % folder)
    problems = []
    entries = []  # (id, path)
    paired = {}   # real path of the file -> id, so a file reached two ways is one file

    def real(path):
        return os.path.realpath(os.path.join(folder, path))

    for asset_id, path in pairs:
        ok = True
        reason = id_problem(asset_id)
        if reason:
            problems.append("%s: not a valid ID: %s" % (asset_id, reason))
            ok = False
        reason = path_problem(path)
        if reason:
            problems.append("%s: %s" % (path, reason))
            continue
        if real(path) in paired:
            problems.append("%s: given twice, as %s and %s; one entry per file" % (path, paired[real(path)], asset_id))
            continue
        paired[real(path)] = asset_id
        if kind_of(path) is None and os.path.isfile(os.path.join(folder, path)):
            problems.append("%s: unknown kind; %s" % (path, SUPPORTED))
            continue
        if ok:
            entries.append((asset_id, path))

    found, left_out = scan(folder)
    for asset_id, path in found:
        if real(path) in paired:
            continue
        reason = path_problem(path)
        if reason:
            problems.append("%s: %s" % (path, reason))
            continue
        entries.append((asset_id, path))
    if skipped is not None:
        skipped.extend(item for item in left_out if real(item[0]) not in paired)

    by_id = {}
    assets = []
    for asset_id, path in entries:
        if asset_id in by_id:
            problems.append("%s: duplicate ID, given by both %s and %s" % (asset_id, by_id[asset_id], path))
            continue
        by_id[asset_id] = path
        full = os.path.join(folder, path)
        if not _inside(folder, path):
            problems.append("%s: is outside the asset folder" % path)
        elif not os.path.exists(full):
            problems.append("%s: no such file in the folder" % path)
        elif not os.path.isfile(full):
            problems.append("%s: not a file" % path)
        else:
            kind = kind_of(path)
            try:
                reason = contents_problem(full, kind)
                if reason:
                    # In the game's words, so a file is refused here for what the headset would say.
                    problems.append("%s: %s: %s" % (asset_id, path, reason))
                    continue
                assets.append({
                    "id": asset_id,
                    "kind": kind,
                    "path": path,
                    "bytes": os.path.getsize(full),
                    "sha256": sha256_of(full),
                })
            except OSError as e:
                problems.append("%s: cannot be read: %s" % (path, e.strerror or e))

    if not problems and not assets:
        problems.append("%s: no assets were found" % folder)
    if problems:
        raise ManifestError(problems)
    assets.sort(key=lambda a: a["id"])
    return {"format": FORMAT, "generator": generator, "source": None, "assets": assets}


def dumps(manifest):
    """The manifest as the bytes of manifest.json: UTF-8, no byte-order mark."""
    return (json.dumps(manifest, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def _is_int(value):
    return isinstance(value, int) and not isinstance(value, bool)


_ENTRY_FIELDS = (
    ("id", lambda v: isinstance(v, str), "a string"),
    ("kind", lambda v: isinstance(v, str), "a string"),
    ("path", lambda v: isinstance(v, str), "a string"),
    ("bytes", _is_int, "an integer"),
)


def _entry_problem(entry, asset_id, path):
    """Why an entry cannot be used whatever it is wanted for, or None: the game's EntryProblem."""
    reasons = []
    for field, good, wanted in _ENTRY_FIELDS:
        if field not in entry:
            reasons.append("required field '%s' is absent" % field)
        elif not good(entry[field]):
            reasons.append("field '%s' must be %s" % (field, wanted))
    # An unreadable kind is not a problem here: it reads as a kind nobody knows, as in the game.
    for field in ("id", "path"):
        if isinstance(entry.get(field), str) and readable(entry[field]) is None:
            reasons.append("field '%s' is not readable text: it holds a lone surrogate escape" % field)
    if reasons:
        return "; ".join(reasons)
    if id_problem(asset_id):
        return "not a valid ID: %s" % ID_RULE
    if entry["bytes"] < 0:
        return "'bytes' is negative"
    reason = path_problem(path)
    if reason:
        return "invalid path: %s" % reason
    return None


def check_report(folder, notices=None):
    """Check `folder` against its manifest. Returns (assets listed, problems).

    The rules the game also applies give the game's lines, in its order: first every problem
    with the entries themselves (as game/Logic/AssetManifest.cs reads them), then each usable
    entry's file problem, in manifest order (as its Find gives them). A duplicate ID drops every
    entry with that ID. Rules only this tool applies add lines of their own: a missing
    manifest.json, 'meta' that is not an object, 'sha256' and its value, and the files of
    entries whose kind this tool does not know.

    If `notices` is a list, lines worth telling the user that are not problems,
    such as an entry of a kind this tool does not know, are appended to it.
    """
    manifest_path = os.path.join(folder, MANIFEST_NAME)
    if not os.path.isdir(folder):
        return 0, ["%s: not a folder" % folder]
    if not os.path.isfile(manifest_path):
        return 0, ["%s: not found in %s" % (MANIFEST_NAME, folder)]
    try:
        with open(manifest_path, "rb") as f:
            raw = f.read()
    except OSError as e:
        return 0, ["%s: cannot be read: %s" % (MANIFEST_NAME, e.strerror or e)]
    if raw.startswith(b"\xef\xbb\xbf"):
        return 0, ["%s: starts with a byte-order mark; it must be UTF-8 without one" % MANIFEST_NAME]
    try:
        manifest = loads_strict(raw.decode("utf-8"))
    except UnicodeDecodeError:
        return 0, ["%s: is not UTF-8" % MANIFEST_NAME]
    except ValueError as e:
        return 0, ["%s: is not valid JSON: %s" % (MANIFEST_NAME, e)]
    except RecursionError:
        return 0, ["%s: is not valid JSON: it is nested too deeply to read" % MANIFEST_NAME]
    if not isinstance(manifest, dict):
        return 0, ["%s: must be one JSON object" % MANIFEST_NAME]

    top_problems = []
    top = (
        ("format", _is_int, "an integer"),
        ("generator", lambda v: isinstance(v, str), "a string"),
        ("source", lambda v: v is None or isinstance(v, dict), "an object or null"),
        ("assets", lambda v: isinstance(v, list), "an array"),
    )
    for field, good, wanted in top:
        if field not in manifest:
            top_problems.append("%s: required field '%s' is absent" % (MANIFEST_NAME, field))
        elif not good(manifest[field]):
            top_problems.append("%s: field '%s' must be %s" % (MANIFEST_NAME, field, wanted))
    if top_problems:
        # One line, as the game gives the reason the manifest cannot be read.
        return 0, ["; ".join(top_problems)]
    if manifest["format"] != FORMAT:
        return 0, ["%s: unsupported format %d; this tool understands format %d"
                   % (MANIFEST_NAME, manifest["format"], FORMAT)]

    assets = manifest["assets"]
    if not assets:
        # After the format check, as in the game, so a newer format is still reported as such.
        return 0, [NO_ASSETS]
    problems = []
    usable = {}      # id -> (entry, whether its sha256 can be compared), in manifest order
    first_path = {}  # id -> how its first entry was named
    for index, entry in enumerate(assets):
        label = "entry %d" % (index + 1)
        if not isinstance(entry, dict):
            problems.append("%s: must be an object" % label)
            continue
        asset_id, path = readable(entry.get("id")), readable(entry.get("path"))
        if asset_id is not None:
            shown = path if path is not None else label
            label = asset_id
        if "meta" in entry and not isinstance(entry["meta"], dict):
            problems.append("%s: field 'meta' must be an object" % label)
        sha_ok = "sha256" in entry
        if sha_ok and not (isinstance(entry["sha256"], str) and _SHA256.fullmatch(entry["sha256"])):
            where = "%s: %s" % (asset_id, path) if asset_id is not None and path is not None else label
            problems.append("%s: 'sha256' must be 64 lowercase hex digits" % where)
            sha_ok = False
        if asset_id is not None:
            if asset_id in first_path:
                # Neither entry is used: the game cannot know which one was meant.
                problems.append("%s: duplicate ID, listed for both %s and %s" % (asset_id, first_path[asset_id], shown))
                usable.pop(asset_id, None)
                continue
            first_path[asset_id] = shown
        invalid = _entry_problem(entry, asset_id, path)
        if invalid:
            problems.append("%s: invalid entry: %s" % (label, invalid))
            continue
        usable[asset_id] = (entry, sha_ok)

    for asset_id, (entry, sha_ok) in usable.items():
        path, kind, size = entry["path"], entry["kind"], entry["bytes"]
        where = "%s: %s" % (asset_id, path)
        if kind not in KINDS.values() and notices is not None:
            # Not a problem: the contract lets kinds be added without a new format.
            shown_kind = readable(kind) if readable(kind) is not None else json.dumps(kind)
            notices.append("%s: unknown kind '%s'; its contents were not checked" % (where, shown_kind))
        full = os.path.join(folder, path)
        if not _inside(folder, path):
            problems.append("%s: invalid entry: its path leaves the asset folder" % asset_id)
            continue
        if os.path.exists(full) and not os.path.isfile(full):
            problems.append("%s: missing: the path is not a file" % where)
            continue
        if not os.path.exists(full):
            problems.append("%s: missing" % where)
            continue
        try:
            actual = os.path.getsize(full)
            if actual != size:
                problems.append("%s: incomplete or changed: the file is %d bytes, the manifest says %d"
                                % (where, actual, size))
                continue
            if sha_ok and sha256_of(full) != entry["sha256"]:
                problems.append("%s: wrong checksum: the file's sha256 differs from the manifest's" % where)
            reason = contents_problem(full, kind)
            if reason:
                problems.append("%s: %s" % (where, reason))
        except OSError as e:
            problems.append("%s: cannot be read: %s" % (where, e.strerror or e))
    return len(assets), problems


def check(folder):
    """The problems with `folder` and its manifest, one line each. Empty when all is good."""
    return check_report(folder)[1]
