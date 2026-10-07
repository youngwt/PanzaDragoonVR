#!/usr/bin/env bash
# Installs everything needed to build a Godot C# app for the Quest 3:
# Godot 4.7.2 (.NET build) with export templates, the .NET 9 SDK, JDK 17 and
# the Android SDK packages. See tools/README.md.
#
#   tools/setup-toolchain.sh           install whatever is missing
#   tools/setup-toolchain.sh --check   print the versions found; non-zero if any part is missing
#
# Running the installer accepts the Android SDK licence terms.
#
# Each part is built in a scratch folder and only moved into place, with its
# ".installed" marker, once it is complete. A part without a marker is treated
# as not installed and is rebuilt from a fresh download.

set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"

GODOT_BASE_URL="https://github.com/godotengine/godot/releases/download/${GODOT_RELEASE}"
GODOT_ZIP="Godot_v${GODOT_RELEASE}_mono_linux_x86_64.zip"
GODOT_TEMPLATES="Godot_v${GODOT_RELEASE}_mono_export_templates.tpz"

DOTNET_CHANNEL="9.0"
DOTNET_INSTALL_URL="https://dot.net/v1/dotnet-install.sh"

JDK_API_URL="https://api.adoptium.net/v3/assets/latest/17/hotspot?architecture=x64&image_type=jdk&os=linux&vendor=eclipse"

# File name and SHA-256 as published on https://developer.android.com/studio
ANDROID_TOOLS_ZIP="commandlinetools-linux-15859902_latest.zip"
ANDROID_TOOLS_SHA256="4e4c464f145a7512b57d088ac6c278c03c9eea610886b35a5e0804e74eedf583"
ANDROID_TOOLS_URL="https://dl.google.com/android/repository/${ANDROID_TOOLS_ZIP}"
ANDROID_PACKAGES=("platform-tools" "build-tools;36.1.0" "platforms;android-36")

MARKER=".installed"
SCRATCH="$TOOLCHAIN_ROOT/.toolchain-setup-tmp"

die() {
    echo "setup-toolchain: error: $*" >&2
    exit 1
}

say() {
    echo "setup-toolchain: $*"
}

cleanup() {
    rm -rf "$SCRATCH"
}

# download <url> <file>: fetch to <file>; the caller verifies it before use.
download() {
    local url="$1" file="$2"
    say "downloading $(basename "$file")"
    curl --fail --silent --location --retry 3 --output "$file" "$url" \
        || die "could not download $url"
}

# verify <sha256sum|sha512sum> <expected hash> <file>
verify() {
    local tool="$1" expected="$2" file="$3" actual
    actual="$("$tool" "$file" | cut -d' ' -f1)" || die "could not hash $(basename "$file")"
    [ -n "$expected" ] && [ "$actual" = "$expected" ] \
        || die "checksum mismatch for $(basename "$file"); nothing from it was installed"
}

# installed <dir> [expected marker text]: true if the part's marker is present
# (and, when given, says what we expect).
installed() {
    local dir="$1" want="${2:-}"
    [ -f "$dir/$MARKER" ] || return 1
    [ -z "$want" ] || [ "$(cat "$dir/$MARKER")" = "$want" ]
}

# publish <built dir> <final dir> <marker text>: mark the build complete and move it into place.
publish() {
    local built="$1" final="$2" text="$3"
    printf '%s\n' "$text" > "$built/$MARKER" || die "could not write the marker for $final"
    rm -rf "$final" || die "could not remove the incomplete $final"
    mv "$built" "$final" || die "could not move the finished install to $final"
}

# ---------------------------------------------------------------- Godot

install_godot() {
    if installed "$GODOT_DIR"; then
        say "Godot ${GODOT_RELEASE} (mono) already installed, skipping"
        return
    fi
    local dl="$SCRATCH/godot-dl" built="$SCRATCH/godot" sums zip_sum tpz_sum
    mkdir -p "$dl" || die "could not create $dl"

    download "$GODOT_BASE_URL/SHA512-SUMS.txt" "$dl/SHA512-SUMS.txt"
    sums="$dl/SHA512-SUMS.txt"
    zip_sum="$(awk -v f="$GODOT_ZIP" '$2 == f { print $1 }' "$sums")"
    tpz_sum="$(awk -v f="$GODOT_TEMPLATES" '$2 == f { print $1 }' "$sums")"
    [ -n "$zip_sum" ] && [ -n "$tpz_sum" ] || die "SHA512-SUMS.txt does not list the Godot files"

    download "$GODOT_BASE_URL/$GODOT_ZIP" "$dl/$GODOT_ZIP"
    verify sha512sum "$zip_sum" "$dl/$GODOT_ZIP"
    download "$GODOT_BASE_URL/$GODOT_TEMPLATES" "$dl/$GODOT_TEMPLATES"
    verify sha512sum "$tpz_sum" "$dl/$GODOT_TEMPLATES"

    say "unpacking Godot"
    mkdir -p "$dl/editor" "$dl/tpz" || die "could not create the Godot unpack folders"
    unzip -q "$dl/$GODOT_ZIP" -d "$dl/editor" || die "could not unpack $GODOT_ZIP"
    # The zip holds one top-level folder with the binary and GodotSharp inside.
    local top
    top="$(find "$dl/editor" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
    [ -n "$top" ] && [ -f "$top/$(basename "$GODOT")" ] || die "$GODOT_ZIP does not contain $(basename "$GODOT")"
    mv "$top" "$built" || die "could not stage the Godot editor"
    chmod +x "$built/$(basename "$GODOT")" || die "could not make Godot executable"

    # Self-contained mode: settings and templates live in editor_data beside the binary.
    : > "$built/._sc_" || die "could not enable Godot self-contained mode"

    say "unpacking Godot export templates"
    unzip -q "$dl/$GODOT_TEMPLATES" -d "$dl/tpz" || die "could not unpack $GODOT_TEMPLATES"
    [ -d "$dl/tpz/templates" ] || die "$GODOT_TEMPLATES does not contain a templates folder"
    local templates="$built/editor_data/export_templates"
    mkdir -p "$templates" || die "could not create $templates"
    mv "$dl/tpz/templates" "$templates/$(basename "$GODOT_EXPORT_TEMPLATES")" \
        || die "could not stage the Godot export templates"
    [ -f "$templates/$(basename "$GODOT_EXPORT_TEMPLATES")/android_source.zip" ] \
        || die "the Godot export templates have no Android templates"

    publish "$built" "$GODOT_DIR" "$GODOT_RELEASE mono"
    rm -rf "$dl"
    say "installed Godot ${GODOT_RELEASE} (mono) in $GODOT_DIR"
}

# ---------------------------------------------------------------- .NET

install_dotnet() {
    if installed "$DOTNET_ROOT"; then
        say ".NET ${DOTNET_CHANNEL} SDK already installed, skipping"
        return
    fi
    local dl="$SCRATCH/dotnet-dl" built="$SCRATCH/dotnet"
    mkdir -p "$dl" || die "could not create $dl"

    # Microsoft publishes no checksum for the install script; the script itself
    # checks the SDK archive it downloads.
    download "$DOTNET_INSTALL_URL" "$dl/dotnet-install.sh"
    say "installing the .NET ${DOTNET_CHANNEL} SDK"
    bash "$dl/dotnet-install.sh" --channel "$DOTNET_CHANNEL" --install-dir "$built" --no-path >"$dl/install.log" 2>&1 \
        || die "dotnet-install.sh failed: $(tail -n 1 "$dl/install.log")"
    [ -x "$built/dotnet" ] || die "dotnet-install.sh did not produce a dotnet binary"

    local version
    # Run from / so a global.json in the current folder cannot change the answer.
    version="$(cd / && DOTNET_ROOT="$built" "$built/dotnet" --version 2>/dev/null)" \
        || die "the freshly installed dotnet does not run"
    publish "$built" "$DOTNET_ROOT" "$version"
    rm -rf "$dl"
    say "installed .NET SDK $version in $DOTNET_ROOT"
}

# ---------------------------------------------------------------- JDK 17

install_jdk() {
    if installed "$JAVA_HOME"; then
        say "JDK 17 already installed, skipping"
        return
    fi
    local dl="$SCRATCH/jdk-dl" built="$SCRATCH/jdk"
    mkdir -p "$dl/unpacked" || die "could not create $dl"

    download "$JDK_API_URL" "$dl/release.json"
    local link sum name
    link="$(jq -r '.[0].binary.package.link // empty' "$dl/release.json" 2>/dev/null)"
    sum="$(jq -r '.[0].binary.package.checksum // empty' "$dl/release.json" 2>/dev/null)"
    name="$(jq -r '.[0].release_name // empty' "$dl/release.json" 2>/dev/null)"
    [ -n "$link" ] && [ -n "$sum" ] && [ -n "$name" ] || die "the Adoptium API gave no JDK 17 download"

    download "$link" "$dl/jdk17.tar.gz"
    verify sha256sum "$sum" "$dl/jdk17.tar.gz"

    say "unpacking $name"
    tar -xzf "$dl/jdk17.tar.gz" -C "$dl/unpacked" || die "could not unpack the JDK 17 archive"
    local top
    top="$(find "$dl/unpacked" -mindepth 1 -maxdepth 1 -type d | head -n 1)"
    [ -n "$top" ] && [ -x "$top/bin/java" ] || die "the JDK 17 archive does not contain bin/java"
    mv "$top" "$built" || die "could not stage JDK 17"

    publish "$built" "$JAVA_HOME" "$name"
    rm -rf "$dl"
    say "installed $name in $JAVA_HOME"
}

# ---------------------------------------------------------------- Android SDK

install_android() {
    local want="${ANDROID_TOOLS_ZIP} ${ANDROID_PACKAGES[*]}"
    if installed "$ANDROID_HOME" "$want"; then
        say "Android SDK packages already installed, skipping"
        return
    fi
    installed "$JAVA_HOME" || die "JDK 17 must be installed before the Android SDK"
    local dl="$SCRATCH/android-dl" built="$SCRATCH/android"
    mkdir -p "$dl/unpacked" "$built/cmdline-tools" || die "could not create $dl"

    download "$ANDROID_TOOLS_URL" "$dl/$ANDROID_TOOLS_ZIP"
    verify sha256sum "$ANDROID_TOOLS_SHA256" "$dl/$ANDROID_TOOLS_ZIP"

    say "unpacking the Android command-line tools"
    unzip -q "$dl/$ANDROID_TOOLS_ZIP" -d "$dl/unpacked" || die "could not unpack $ANDROID_TOOLS_ZIP"
    [ -x "$dl/unpacked/cmdline-tools/bin/sdkmanager" ] || die "$ANDROID_TOOLS_ZIP does not contain sdkmanager"
    mv "$dl/unpacked/cmdline-tools" "$built/cmdline-tools/latest" || die "could not stage the Android command-line tools"

    local sdkmanager="$built/cmdline-tools/latest/bin/sdkmanager"
    say "accepting the Android SDK licences"
    # `yes` ends with a broken pipe once sdkmanager is done; only sdkmanager's status matters.
    yes 2>/dev/null | "$sdkmanager" --sdk_root="$built" --licenses >"$dl/licences.log" 2>&1
    [ "${PIPESTATUS[1]}" -eq 0 ] || die "sdkmanager could not accept the Android SDK licences: $(tail -n 1 "$dl/licences.log")"

    say "installing ${ANDROID_PACKAGES[*]}"
    "$sdkmanager" --sdk_root="$built" "${ANDROID_PACKAGES[@]}" >"$dl/sdkmanager.log" 2>&1 </dev/null \
        || die "sdkmanager could not install the Android packages: $(tail -n 1 "$dl/sdkmanager.log")"
    [ -x "$built/platform-tools/adb" ] || die "sdkmanager did not install adb"
    [ -d "$built/build-tools/36.1.0" ] || die "sdkmanager did not install build-tools 36.1.0"
    [ -d "$built/platforms/android-36" ] || die "sdkmanager did not install platform android-36"

    publish "$built" "$ANDROID_HOME" "$want"
    rm -rf "$dl"
    say "installed the Android SDK packages in $ANDROID_HOME"
}

# ---------------------------------------------------------------- check

# Prints one line per part and returns non-zero if any part is missing.
check() {
    local missing=0 v

    if installed "$GODOT_DIR" && v="$("$GODOT" --headless --version 2>/dev/null | tail -n 1)" \
        && [[ "$v" == "${GODOT_VERSION}.stable.mono"* ]] \
        && [ -f "$GODOT_EXPORT_TEMPLATES/android_source.zip" ]; then
        echo "Godot:   $v (Android export templates present)"
    else
        echo "Godot:   MISSING (expected ${GODOT_VERSION}.stable.mono with export templates in $GODOT_DIR)"
        missing=1
    fi

    if installed "$DOTNET_ROOT" && v="$(cd / && "$DOTNET_ROOT/dotnet" --version 2>/dev/null)" && [[ "$v" == 9.* ]]; then
        echo ".NET:    SDK $v"
    else
        echo ".NET:    MISSING (expected a .NET 9 SDK in $DOTNET_ROOT)"
        missing=1
    fi

    if installed "$JAVA_HOME" && v="$("$JAVA_HOME/bin/java" -version 2>&1 | grep -m 1 ' version "')" && [[ "$v" == *'"17.'* ]]; then
        echo "Java:    $v"
    else
        echo "Java:    MISSING (expected JDK 17 in $JAVA_HOME)"
        missing=1
    fi

    if installed "$ANDROID_HOME" "${ANDROID_TOOLS_ZIP} ${ANDROID_PACKAGES[*]}" \
        && [ -d "$ANDROID_HOME/build-tools/36.1.0" ] && [ -d "$ANDROID_HOME/platforms/android-36" ] \
        && v="$("$ANDROID_HOME/platform-tools/adb" version 2>/dev/null | head -n 1)" && [ -n "$v" ]; then
        echo "Android: $v; build-tools 36.1.0; platform android-36"
    else
        echo "Android: MISSING (expected ${ANDROID_PACKAGES[*]} in $ANDROID_HOME)"
        missing=1
    fi

    return "$missing"
}

# ---------------------------------------------------------------- main

case "${1:-}" in
    --check)
        [ $# -eq 1 ] || die "--check takes no other arguments"
        check
        exit $?
        ;;
    "")
        ;;
    *)
        die "unknown argument '$1' (usage: setup-toolchain.sh [--check])"
        ;;
esac

for tool in curl unzip tar jq sha256sum sha512sum awk find; do
    command -v "$tool" >/dev/null 2>&1 || die "'$tool' is needed but was not found"
done

mkdir -p "$TOOLCHAIN_ROOT" || die "could not create $TOOLCHAIN_ROOT"
# Leftovers from an interrupted run are never reused.
rm -rf "$SCRATCH" || die "could not clear $SCRATCH"
mkdir -p "$SCRATCH" || die "could not create $SCRATCH"
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

install_godot
install_dotnet
install_jdk
install_android

say "done; versions found:"
check || die "the toolchain is still incomplete after setup"
