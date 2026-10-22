#!/usr/bin/env bash
# Copies a tuning file to the top of the game's asset folder on the connected Quest 3, as tuning.json.
# See tools/README.md and the asset contract's "Tuning" section.
#
#   tools/push-tuning.sh <file>      push <file> as tuning.json (tools/tuning.example.json holds the built-ins)
#   tools/push-tuning.sh --remove    remove tuning.json from the headset; the game uses its built-in values
#
# The file is checked to be JSON first; the game checks each setting at launch and puts any problem on
# the board. The app is not restarted; start it again to see the change.
#
# Exit status: 0 pushed (or removed), 1 something failed; one line on stderr says what.

set -u -o pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"
# shellcheck source=quest-adb.sh
. "$here/quest-adb.sh"

REMOTE="/sdcard/Android/data/$PACKAGE/files"
USAGE="usage: push-tuning.sh <file> | --remove"

die() {
    echo "push-tuning: error: $*" >&2
    exit 1
}

say() {
    echo "push-tuning: $*"
}

remove=0
file=""
for arg in "$@"; do
    case "$arg" in
        --remove) remove=1 ;;
        -*) die "unknown option '$arg' ($USAGE)" ;;
        *)
            [ -z "$file" ] || die "give one file only ($USAGE)"
            file="$arg"
            ;;
    esac
done
[ "$remove" -eq 1 ] || [ -n "$file" ] || die "give a tuning file to push, or --remove ($USAGE)"
[ "$remove" -eq 0 ] || [ -z "$file" ] || die "give a file or --remove, not both ($USAGE)"

if [ -n "$file" ]; then
    [ -f "$file" ] || die "$file is not a file"
    command -v python3 >/dev/null 2>&1 || die "'python3' is needed but was not found"
    # As the game reads it: a byte-order mark is allowed; NaN and Infinity are not JSON numbers.
    reason="$(python3 -B -c '
import json, sys
def constant(name):
    raise ValueError("%s is not a JSON number" % name)
try:
    with open(sys.argv[1], encoding="utf-8-sig") as f:
        json.load(f, parse_constant=constant)
except (ValueError, OSError) as e:
    print(" ".join(str(e).split()).rstrip("."))
    sys.exit(1)
' "$file")" || die "$file is not valid JSON (${reason:-no reason was given}); nothing was pushed"
fi

find_headset

if [ "$remove" -eq 1 ]; then
    say "removing $REMOTE/tuning.json on $serial"
    host_adb -s "$serial" shell "rm -f '$REMOTE/tuning.json'" || die "could not remove $REMOTE/tuning.json on the headset"
else
    say "pushing $file to $REMOTE/tuning.json on $serial"
    host_adb -s "$serial" shell "mkdir -p '$REMOTE'" || die "could not create $REMOTE on the headset"
    # Through a stream rather than 'adb push', which would need a path the host can see; the Flatpak
    # sandbox's /tmp is its own.
    host_adb -s "$serial" exec-in "cat > '$REMOTE/tuning.json'" < "$file" \
        || die "adb could not copy $file to $REMOTE/tuning.json"
    host_adb -s "$serial" shell "test -f '$REMOTE/tuning.json'" \
        || die "the push ran, but $REMOTE/tuning.json is not on the headset"
fi

say "done; start the app again to see the change (tools/build-quest.sh, or launch it in the headset)"
