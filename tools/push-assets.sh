#!/usr/bin/env bash
# Checks an asset folder of your own files and copies its contents to the game's asset folder on the
# connected Quest 3. See tools/README.md.
#
#   tools/push-assets.sh <folder>           check the folder, then push what is inside it
#   tools/push-assets.sh --clear <folder>   empty the headset's asset folder first, then push
#   tools/push-assets.sh --clear            empty the headset's asset folder; the game runs on placeholders
#                                           (as does --clear with a folder that holds no files)
#
# The folder is checked with 'write_manifest.py --check' first; nothing is pushed when it has a problem.
# Its contents go straight into the app's files folder, so manifest.json lands at the top of it.
# The app is not restarted; start it again to see the change.
#
# Exit status: 0 pushed (or cleared), 1 something failed; one line on stderr says what.

set -u -o pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"
# shellcheck source=quest-adb.sh
. "$here/quest-adb.sh"

REMOTE="/sdcard/Android/data/$PACKAGE/files"
USAGE="usage: push-assets.sh [--clear] [<folder>]"

die() {
    echo "push-assets: error: $*" >&2
    exit 1
}

say() {
    echo "push-assets: $*"
}

clear=0
folder=""
for arg in "$@"; do
    case "$arg" in
        --clear) clear=1 ;;
        -*) die "unknown option '$arg' ($USAGE)" ;;
        *)
            [ -z "$folder" ] || die "give one folder only ($USAGE)"
            folder="$arg"
            ;;
    esac
done
[ "$clear" -eq 1 ] || [ -n "$folder" ] || die "give a folder to push, or --clear ($USAGE)"

if [ -n "$folder" ]; then
    [ -d "$folder" ] || die "$folder is not a folder"
    # With --clear, a folder holding no files means: clear the headset's folder and push nothing.
    if [ "$clear" -eq 1 ] && [ -z "$(find "$folder" \( -type f -o -type l \) -print -quit)" ]; then
        say "$folder holds no files; the headset's folder is cleared and nothing is pushed"
        folder=""
    fi
fi

if [ -n "$folder" ]; then
    command -v python3 >/dev/null 2>&1 || die "'python3' is needed but was not found"
    python3 -B "$here/write_manifest.py" --check "$folder" \
        || die "the folder has problems, listed above; fix them (or run tools/write_manifest.py $folder again) and nothing was pushed"
fi

find_headset

if [ "$clear" -eq 1 ]; then
    say "emptying $REMOTE on $serial"
    # The folder itself stays: the app's runtime made it and owns it.
    host_adb -s "$serial" shell "mkdir -p '$REMOTE' && find '$REMOTE' -mindepth 1 -delete" >/dev/null \
        || die "could not empty $REMOTE on the headset"
fi

if [ -n "$folder" ]; then
    say "pushing the contents of $folder to $REMOTE on $serial"
    host_adb -s "$serial" shell "mkdir -p '$REMOTE'" || die "could not create $REMOTE on the headset"
    # The folder's contents, not the folder itself, go over as a tar stream unpacked on the headset.
    # 'adb push' would need a path the host can see, and the Flatpak sandbox's /tmp is its own.
    command -v tar >/dev/null 2>&1 || die "'tar' is needed but was not found"
    tar -C "$folder" -cf - . \
        | host_adb -s "$serial" exec-in "tar -xf - -C '$REMOTE'" \
        || die "adb could not copy $folder to $REMOTE"
    host_adb -s "$serial" shell "test -f '$REMOTE/manifest.json'" \
        || die "the push ran, but $REMOTE/manifest.json is not on the headset"
fi

say "done; start the app again to see the change (tools/build-quest.sh, or launch it in the headset)"
