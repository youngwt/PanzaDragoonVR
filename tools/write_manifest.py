#!/usr/bin/env python3
"""Write or check the manifest.json of an asset folder of your own files.

    python3 tools/write_manifest.py <folder> [ID=path ...]
    python3 tools/write_manifest.py --check <folder>

Writing scans the folder: each .png or .glb file whose name without the extension
is a valid asset ID is listed under that ID, so images/e03.floor.png is e03.floor.
Give ID=path, with the path relative to the folder, for a file not named after its ID.
On any error nothing is written and the exit status is 1.
"""
import os
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import manifest

USAGE = __doc__.split("\n\n")[1]


def _fail(problems):
    for line in problems:
        print("write_manifest: error: %s" % line, file=sys.stderr)
    return 1


def _write(folder, data):
    """Replace the folder's manifest in one step, through a temporary file."""
    handle, temporary = tempfile.mkstemp(dir=folder, prefix=".manifest-", suffix=".tmp")
    try:
        with os.fdopen(handle, "wb") as f:
            f.write(data)
        # mkstemp makes the file owner-only; give it the mode an ordinary new file gets.
        umask = os.umask(0)
        os.umask(umask)
        os.chmod(temporary, 0o666 & ~umask)
        os.replace(temporary, os.path.join(folder, manifest.MANIFEST_NAME))
    except BaseException:
        if os.path.exists(temporary):
            os.unlink(temporary)
        raise


def check(folder):
    notices = []
    count, problems = manifest.check_report(folder, notices)
    for line in notices:
        print("write_manifest: notice: %s" % line, file=sys.stderr)
    if problems:
        return _fail(problems)
    print("%d asset%s checked in %s: no problems" % (count, "" if count == 1 else "s", folder))
    return 0


def write(folder, arguments):
    pairs = []
    for argument in arguments:
        asset_id, equals, path = argument.partition("=")
        if not asset_id or not path:
            return _fail(["%s: expected ID=path" % argument])
        pairs.append((asset_id, path))
    skipped = []
    target = os.path.join(folder, manifest.MANIFEST_NAME)
    try:
        built = manifest.build(folder, pairs, skipped)
    except manifest.ManifestError as e:
        return _fail(e.problems)
    except OSError as e:
        return _fail(["%s: %s" % (e.filename or folder, e.strerror or e)])
    try:
        _write(folder, manifest.dumps(built))
    except OSError as e:
        return _fail(["%s: cannot be written: %s" % (target, e.strerror or e)])
    for path, reason in skipped:
        print("write_manifest: left out %s: %s" % (path, reason), file=sys.stderr)
    for asset in built["assets"]:
        print("%-24s %-6s %10d  %s" % (asset["id"], asset["kind"], asset["bytes"], asset["path"]))
    count = len(built["assets"])
    print("wrote %s with %d asset%s" % (target, count, "" if count == 1 else "s"))
    return 0


def main(argv):
    arguments = argv[1:]
    if len(arguments) == 2 and arguments[0] == "--check":
        return check(arguments[1])
    if arguments and arguments[0] in ("-h", "--help"):
        print(USAGE)
        return 0
    if not arguments or arguments[0].startswith("-"):
        print(USAGE, file=sys.stderr)
        return 1
    return write(arguments[0], arguments[1:])


if __name__ == "__main__":
    sys.exit(main(sys.argv))
