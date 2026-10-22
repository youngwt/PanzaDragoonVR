# Sourced by build-quest.sh, push-assets.sh and compare-headset.sh: the app's package, adb on the host, and finding the
# one connected headset. See tools/README.md. Needs ANDROID_HOME (from toolchain-env.sh) and a 'die'
# function from the script that sources it.

PACKAGE="io.github.youngwt.panzadragoonvr"

# adb runs on the host when this shell is inside the Flatpak sandbox, which has no USB access.
host_adb() {
    if command -v flatpak-spawn >/dev/null 2>&1 && flatpak-spawn --host true >/dev/null 2>&1; then
        flatpak-spawn --host "$ANDROID_HOME/platform-tools/adb" "$@"
    else
        "$ANDROID_HOME/platform-tools/adb" "$@"
    fi
}

# find_headset [<suffix>]: sets 'serial' to the first connected, authorised headset, or dies saying why.
# <suffix> is added to the end of the "not authorised" and "no headset found" messages.
find_headset() {
    local suffix="${1:-}" devices
    devices="$(host_adb devices 2>/dev/null)" \
        || die "adb could not run on the host; check that $ANDROID_HOME/platform-tools/adb exists there"
    serial="$(printf '%s\n' "$devices" | awk 'NR > 1 && $2 == "device" { print $1; exit }')"
    if [ -z "$serial" ]; then
        if printf '%s\n' "$devices" | awk 'NR > 1 && $2 == "unauthorized" { found = 1 } END { exit !found }'; then
            die "the headset has not authorised this computer; put it on and accept the USB debugging prompt$suffix"
        fi
        die "no headset found; connect the Quest 3 by USB and enable developer mode$suffix"
    fi
}
