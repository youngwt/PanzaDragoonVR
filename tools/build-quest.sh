#!/usr/bin/env bash
# Builds the debug APK of game/ and installs and launches it on a connected Quest 3.
# See tools/README.md.
#
#   tools/build-quest.sh                build, install and launch on the headset
#   tools/build-quest.sh --no-install   build only; adb is not used
#
# The APK lands in game/build/. Each step's full output is kept in game/build/logs/.
#
# Exit status:
#   0  built; and, unless --no-install, installed and still running after 5 seconds
#   1  something failed; one line on stderr says what
#   2  built and installed, but the app had not started within 10 seconds of the launch
#      (the headset is usually asleep)

set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"

PRESET="Quest 3"
PACKAGE="io.github.youngwt.panzadragoonvr"
APK_NAME="PanzaDragoonVR.apk"
ASSEMBLY="PanzaDragoonVR.dll"

game="$(cd "$here/.." && pwd)/game"
build="$game/build"
logs="$build/logs"
apk="$build/$APK_NAME"

die() {
    echo "build-quest: error: $*" >&2
    exit 1
}

say() {
    echo "build-quest: $*"
}

# step <log name> <what it is doing> <command...>: run quietly, keep the output in a log.
step() {
    local log="$logs/$1.log" what="$2"
    shift 2
    say "$what"
    "$@" >"$log" 2>&1 </dev/null || die "$what failed; see $log"
}

# adb runs on the host when this shell is inside the Flatpak sandbox, which has no USB access.
host_adb() {
    if command -v flatpak-spawn >/dev/null 2>&1 && flatpak-spawn --host true >/dev/null 2>&1; then
        flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" "$@"
    else
        "$ANDROID_HOME/platform-tools/adb" "$@"
    fi
}

install=1
case "${1:-}" in
    "") ;;
    --no-install) install=0 ;;
    *) die "unknown argument '$1' (usage: build-quest.sh [--no-install])" ;;
esac
[ $# -le 1 ] || die "too many arguments (usage: build-quest.sh [--no-install])"

# ---------------------------------------------------------------- build

"$here/setup-toolchain.sh" --check >/dev/null 2>&1 \
    || die "the build toolchain is missing or incomplete; run tools/setup-toolchain.sh"
[ -f "$game/project.godot" ] || die "$game/project.godot is missing; restore the game folder from git"
command -v unzip >/dev/null 2>&1 || die "'unzip' is needed but was not found; install it"

mkdir -p "$logs" || die "could not create $logs; check the folder's permissions"
# A stale APK must never be mistaken for the result of this run.
rm -f "$apk" "$apk.idsig" || die "could not remove the old $apk; check its permissions"

step import "importing the project" \
    "$GODOT" --headless --path "$game" --import
step build-solutions "building the C# solution" \
    "$GODOT" --headless --path "$game" --build-solutions --quit
# OpenXR needs Godot's Gradle build. Its template is unpacked into game/android/ on the
# first run, and again whenever it was made by another Godot version.
template_flag=()
if [ "$(cat "$game/android/.build_version" 2>/dev/null)" != "${GODOT_VERSION}.stable.mono" ]; then
    rm -rf "$game/android" || die "could not remove the old $game/android; check its permissions"
    template_flag=(--install-android-build-template)
fi
# Without this the Gradle daemon outlives the export and the headless export never returns.
export GRADLE_OPTS="${GRADLE_OPTS:+$GRADLE_OPTS }-Dorg.gradle.daemon=false"
step export "exporting the debug APK (the first run downloads Gradle and its libraries)" \
    "$GODOT" --headless --path "$game" "${template_flag[@]}" --export-debug "$PRESET" "build/$APK_NAME"

# Godot can exit 0 from a failed export, so check what it produced.
[ -s "$apk" ] || die "the export produced no APK; see $logs/export.log"
unzip -l "$apk" 2>/dev/null | grep -q "/$ASSEMBLY\$" \
    || die "the APK does not contain $ASSEMBLY, so C# would not run; see $logs/export.log"
say "built $apk"

[ "$install" -eq 1 ] || exit 0

# ---------------------------------------------------------------- install and launch

devices="$(host_adb devices 2>/dev/null)" \
    || die "adb could not run on the host; check that $ANDROID_HOME/platform-tools/adb exists there"
serial="$(printf '%s\n' "$devices" | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
if [ -z "$serial" ]; then
    if printf '%s\n' "$devices" | awk 'NR > 1 && $2 == "unauthorized" { found = 1 } END { exit !found }'; then
        die "the headset has not authorised this computer; put it on and accept the USB debugging prompt, then run this again (the APK is in $apk)"
    fi
    die "no headset found; connect the Quest 3 by USB and enable developer mode, then run this again (the APK is in $apk)"
fi

say "installing on $serial"
host_adb -s "$serial" install -r "$apk" >"$logs/install.log" 2>&1 \
    || die "adb could not install the APK: $(tail -n 1 "$logs/install.log")"

say "launching $PACKAGE"
host_adb -s "$serial" shell am force-stop "$PACKAGE" >/dev/null 2>&1
# Start from an empty log so a crash log holds only this launch.
host_adb -s "$serial" logcat -c >/dev/null 2>&1
host_adb -s "$serial" shell monkey -p "$PACKAGE" -c android.intent.category.LAUNCHER 1 >"$logs/launch.log" 2>&1 \
    && ! grep -q "No activities found" "$logs/launch.log" \
    || die "the app was installed but would not launch; see $logs/launch.log and capture 'adb logcat'"

app_pid() {
    host_adb -s "$serial" shell pidof "$PACKAGE" 2>/dev/null | tr -d '\r\n'
}

# The headset can hold a launch back, for example while it is asleep.
started=0
for _ in 1 2 3 4 5 6 7 8 9 10; do
    if [ -n "$(app_pid)" ]; then
        started=1
        break
    fi
    sleep 1
done
if [ "$started" -eq 0 ]; then
    echo "build-quest: installed and asked to launch, but the app has not started yet; wake the headset or put it on, and answer any prompt it shows" >&2
    exit 2
fi

# A crash while .NET starts shows up a moment after the process appears.
sleep 5
if [ -z "$(app_pid)" ]; then
    host_adb -s "$serial" logcat -d >"$logs/logcat-crash.log" 2>&1
    die "the app started and then stopped; the device log is in $logs/logcat-crash.log"
fi
say "running on the headset; put it on to see the scene"
