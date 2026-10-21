#!/usr/bin/env bash
# Shows that the PC command and the headset extract the same files from the same disc image: pulls the
# headset's extracted textures and manifest.json, runs pdz-extract on the disc image on this computer,
# and compares the two byte for byte. See tools/README.md.
#
#   tools/compare-headset.sh <disc.cue|disc.iso>   the same disc image the headset extracted from
#
# Launch the app on the headset first, so it has extracted. Both copies are written under
# extracted/compare-headset/ (git-ignored): headset/ and pc/. Only manifest.json and the files it
# lists are compared; other files under the headset's images/ (left by an earlier push) are set aside.
#
# Exit status: 0 identical, 1 something failed or the two differ; the files that differ are listed.

set -u -o pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"
# shellcheck source=quest-adb.sh
. "$here/quest-adb.sh"

REMOTE="/sdcard/Android/data/$PACKAGE/files"
USAGE="usage: compare-headset.sh <disc.cue|disc.iso>"
OUT="$here/../extracted/compare-headset"

die() {
    echo "compare-headset: error: $*" >&2
    exit 1
}

say() {
    echo "compare-headset: $*"
}

case "${1:-}" in
    "") die "give the disc image the headset extracted from ($USAGE)" ;;
    -*) die "unknown option '$1' ($USAGE)" ;;
esac
[ $# -eq 1 ] || die "give one disc image only ($USAGE)"
image="$1"
[ -f "$image" ] || die "$image is not a file"
command -v dotnet >/dev/null 2>&1 || die "'dotnet' was not found; run tools/setup-toolchain.sh"
command -v python3 >/dev/null 2>&1 || die "'python3' is needed but was not found"

find_headset

rm -rf "$OUT" || die "could not empty $OUT"
mkdir -p "$OUT/headset" || die "could not create $OUT"
OUT="$(cd "$OUT" && pwd)"

host_adb -s "$serial" shell "test -f '$REMOTE/manifest.json'" \
    || die "the headset has no $REMOTE/manifest.json; launch the app so it extracts, then run this again"
say "pulling the headset's manifest.json and images/ from $serial"
# The host's adb writes to a path under the home folder, which the Flatpak sandbox shares.
host_adb -s "$serial" pull "$REMOTE/manifest.json" "$OUT/headset/manifest.json" >/dev/null \
    || die "could not pull $REMOTE/manifest.json"
host_adb -s "$serial" pull "$REMOTE/images" "$OUT/headset/" >/dev/null \
    || die "could not pull $REMOTE/images"

# Set aside what the headset's manifest does not list, so stray files from earlier pushes do not count.
unlisted="$(python3 - "$OUT/headset" <<'PY'
import json, os, sys
root = sys.argv[1]
listed = {a["path"] for a in json.load(open(os.path.join(root, "manifest.json"), encoding="utf-8"))["assets"]}
n = 0
for folder, _, names in os.walk(os.path.join(root, "images")):
    for name in names:
        rel = os.path.relpath(os.path.join(folder, name), root).replace(os.sep, "/")
        if rel not in listed:
            os.remove(os.path.join(folder, name))
            n += 1
print(n)
PY
)" || die "could not read the headset's manifest.json"
[ "$unlisted" = "0" ] || say "set aside $unlisted file(s) under the headset's images/ that its manifest does not list"

say "extracting $image on this computer"
dotnet run --project "$here/pdz-extract" -- "$image" "$OUT/pc" >"$OUT/pc-extract.log" 2>&1 \
    || die "pdz-extract failed; see $OUT/pc-extract.log"

if diff -rq "$OUT/headset" "$OUT/pc" >"$OUT/differences.txt" 2>&1; then
    count="$(find "$OUT/pc" -type f | wc -l | tr -d ' ')"
    say "identical: $count files, the manifest included, in $OUT/headset and $OUT/pc"
    exit 0
fi

total="$(wc -l <"$OUT/differences.txt" | tr -d ' ')"
echo "compare-headset: error: the headset's files and the PC's differ in $total place(s); all are in $OUT/differences.txt" >&2
head -n 20 "$OUT/differences.txt" | sed 's/^/compare-headset: error: /' >&2
[ "$total" -le 20 ] || echo "compare-headset: error: and $((total - 20)) more, in $OUT/differences.txt" >&2
exit 1
