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


def contents_problem(file_path, kind):
    """Why the file is not of `kind`, judged by its header alone, or None."""
    with open(file_path, "rb") as f:
        head = f.read(26)
    if kind == "image":
        if head[:8] != _PNG_SIGNATURE or head[12:16] != b"IHDR" or len(head) < 26:
            return "not a PNG file"
        depth, colour = head[24], head[25]
        if depth != 8 or colour not in (2, 6):
            name = _PNG_COLOUR_TYPES.get(colour, "colour type %d" % colour)
            return "a %d-bit %s PNG; it must be 8-bit RGB or RGBA" % (depth, name)
        return None
    if kind == "model":
        if head[:4] != b"glTF" or len(head) < 12:
            return "not a .glb file: no glTF header"
        version = struct.unpack("<I", head[4:8])[0]
        if version != 2:
            return "a .glb of glTF version %d; it must be version 2" % version
        return None
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
                    problems.append("%s: %s" % (path, reason))
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


def check_report(folder, notices=None):
    """Check `folder` against its manifest. Returns (assets listed, problems).

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
        manifest = json.loads(raw.decode("utf-8"))
    except UnicodeDecodeError:
        return 0, ["%s: is not UTF-8" % MANIFEST_NAME]
    except ValueError as e:
        return 0, ["%s: is not valid JSON: %s" % (MANIFEST_NAME, e)]
    except RecursionError:
        return 0, ["%s: is not valid JSON: it is nested too deeply to read" % MANIFEST_NAME]
    if not isinstance(manifest, dict):
        return 0, ["%s: must be one JSON object" % MANIFEST_NAME]

    problems = []
    top = (
        ("format", _is_int, "an integer"),
        ("generator", lambda v: isinstance(v, str), "a string"),
        ("source", lambda v: v is None or isinstance(v, dict), "an object or null"),
        ("assets", lambda v: isinstance(v, list), "an array"),
    )
    for field, good, wanted in top:
        if field not in manifest:
            problems.append("%s: required field '%s' is absent" % (MANIFEST_NAME, field))
        elif not good(manifest[field]):
            problems.append("%s: field '%s' must be %s" % (MANIFEST_NAME, field, wanted))
    if problems:
        return 0, problems
    if manifest["format"] != FORMAT:
        return 0, ["%s: unsupported format %d; this tool understands format %d"
                   % (MANIFEST_NAME, manifest["format"], FORMAT)]

    assets = manifest["assets"]
    first_path = {}
    for index, entry in enumerate(assets):
        label = "entry %d" % (index + 1)
        if not isinstance(entry, dict):
            problems.append("%s: must be an object" % label)
            continue
        asset_id = entry.get("id")
        if isinstance(asset_id, str):
            shown = entry["path"] if isinstance(entry.get("path"), str) else label
            label = asset_id
            if asset_id in first_path:
                problems.append("%s: duplicate ID, listed for both %s and %s" % (asset_id, first_path[asset_id], shown))
            else:
                first_path[asset_id] = shown
        fields = (
            ("id", lambda v: isinstance(v, str), "a string"),
            ("kind", lambda v: isinstance(v, str), "a string"),
            ("path", lambda v: isinstance(v, str), "a string"),
            ("bytes", _is_int, "an integer"),
        )
        bad = False
        for field, good, wanted in fields:
            if field not in entry:
                problems.append("%s: required field '%s' is absent" % (label, field))
                bad = True
            elif not good(entry[field]):
                problems.append("%s: field '%s' must be %s" % (label, field, wanted))
                bad = True
        if "meta" in entry and not isinstance(entry["meta"], dict):
            problems.append("%s: field 'meta' must be an object" % label)
        if bad:
            continue

        path, kind, size = entry["path"], entry["kind"], entry["bytes"]
        where = "%s: %s" % (asset_id, path)
        if id_problem(asset_id):
            problems.append("%s: not a valid ID: %s" % (asset_id, ID_RULE))
        if kind not in KINDS.values() and notices is not None:
            # Not a problem: the contract lets kinds be added without a new format.
            notices.append("%s: unknown kind '%s'; its contents were not checked" % (where, kind))
        if size < 0:
            problems.append("%s: 'bytes' is negative" % where)
        sha = entry.get("sha256")
        if "sha256" in entry and not (isinstance(sha, str) and _SHA256.fullmatch(sha)):
            problems.append("%s: 'sha256' must be 64 lowercase hex digits" % where)
            sha = None
        reason = path_problem(path)
        if reason:
            problems.append("%s: invalid path: %s" % (where, reason))
            continue
        full = os.path.join(folder, path)
        if not _inside(folder, path):
            problems.append("%s: invalid path: it leaves the asset folder" % where)
            continue
        if not os.path.exists(full):
            problems.append("%s: missing" % where)
            continue
        if not os.path.isfile(full):
            problems.append("%s: not a file" % where)
            continue
        try:
            actual = os.path.getsize(full)
            if size >= 0 and actual != size:
                problems.append("%s: wrong size: the file is %d bytes, the manifest says %d" % (where, actual, size))
            if sha is not None and sha256_of(full) != sha:
                problems.append("%s: wrong checksum: the file's sha256 differs from the manifest's" % where)
            reason = contents_problem(full, kind)
            if reason:
                problems.append("%s: wrong contents for kind '%s': %s" % (where, kind, reason))
        except OSError as e:
            problems.append("%s: cannot be read: %s" % (where, e.strerror or e))
    return len(assets), problems


def check(folder):
    """The problems with `folder` and its manifest, one line each. Empty when all is good."""
    return check_report(folder)[1]
