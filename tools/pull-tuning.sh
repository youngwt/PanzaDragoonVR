#!/usr/bin/env bash
# Copies tuning.json from the top of the game's asset folder on the connected Quest 3 to a file on this
# computer, such as one saved by the development mode's tuning list. See tools/README.md and the asset
# contract's "Tuning" section.
#
#   tools/pull-tuning.sh <file>      pull tuning.json to <file>
#
# <file> is replaced only after a full copy; if anything fails it is left as it was.
#
# Exit status: 0 pulled, 1 something failed; one line on stderr says what.

set -u -o pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"
# shellcheck source=quest-adb.sh
. "$here/quest-adb.sh"

USAGE="usage: pull-tuning.sh <file>"

die() {
    echo "pull-tuning: error: $*" >&2
    exit 1
}

say() {
    echo "pull-tuning: $*"
}

file=""
for arg in "$@"; do
    case "$arg" in
        -*) die "unknown option '$arg' ($USAGE)" ;;
        *)
            [ -z "$file" ] || die "give one file only ($USAGE)"
            file="$arg"
            ;;
    esac
done
[ -n "$file" ] || die "give a file to pull to ($USAGE)"
[ ! -d "$file" ] || die "$file is a folder"
[ -d "$(dirname -- "$file")" ] || die "the folder $(dirname -- "$file") does not exist; nothing was pulled"

find_headset

host_adb -s "$serial" shell "test -f '$REMOTE/tuning.json'" \
    || die "$REMOTE/tuning.json is not on the headset, or adb could not check; nothing was pulled"

partial="$file.pulling.$$"
trap 'rm -f "$partial"' EXIT

say "pulling $REMOTE/tuning.json on $serial to $file"
# Through a stream rather than 'adb pull', which would need a path the host can see; the Flatpak
# sandbox's /tmp is its own.
host_adb -s "$serial" exec-out "cat '$REMOTE/tuning.json'" > "$partial" \
    || die "adb could not copy $REMOTE/tuning.json to $file; $file is unchanged"

# exec-out can end early with no error, so the sizes are compared.
remote_size="$(host_adb -s "$serial" shell "wc -c < '$REMOTE/tuning.json'" | tr -d ' \r')" \
    || die "could not read the size of $REMOTE/tuning.json; $file is unchanged"
local_size="$(wc -c < "$partial" | tr -d ' ')"
[ "$remote_size" = "$local_size" ] \
    || die "the copy holds $local_size bytes but the headset's file $remote_size; $file is unchanged"

mv -f "$partial" "$file" || die "could not move the copy into place as $file; $file is unchanged"

say "done"
