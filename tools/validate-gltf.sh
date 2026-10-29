#!/usr/bin/env bash
# Runs the Khronos glTF Validator on .glb files: the asset contract's Models section says every model
# the tool writes passes it with no errors. See tools/README.md.
#
#   tools/validate-gltf.sh                   the decoding library's sample models, written afresh
#   tools/validate-gltf.sh a.glb [b.glb ...]  the given files
#
# Prints one line per file with its counts of errors, warnings, infos and hints, and an error line
# on stderr for each file with an error. Exits 0 when every file has none, else 1.
#
# The validator is pinned to one release and checked against its SHA-256. When it is missing it is
# downloaded into the toolchain folder ($TOOLCHAIN_ROOT, as tools/toolchain-env.sh sets it), never
# into the project, and it is not committed.

set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"

VALIDATOR_VERSION="2.0.0-dev.3.10"
VALIDATOR_TAR="gltf_validator-${VALIDATOR_VERSION}-linux64.tar.xz"
VALIDATOR_SHA256="168eba887964125abe17ae97899b38d0b3cfd73c266c78424c194929ddcbc522"
VALIDATOR_URL="https://github.com/KhronosGroup/glTF-Validator/releases/download/${VALIDATOR_VERSION}/${VALIDATOR_TAR}"
VALIDATOR_DIR="$TOOLCHAIN_ROOT/gltf-validator-${VALIDATOR_VERSION}"
VALIDATOR="$VALIDATOR_DIR/gltf_validator"
VALIDATOR_MARKER_TEXT="$VALIDATOR_VERSION $VALIDATOR_SHA256"
MARKER=".installed"
ROOT="$(cd "$here/.." && pwd)"
SCRATCH="$TOOLCHAIN_ROOT/.gltf-validator-tmp"
SAMPLES=""

die() {
    echo "validate-gltf: error: $*" >&2
    exit 1
}

say() {
    echo "validate-gltf: $*"
}

cleanup() {
    rm -rf "$SCRATCH"
    [ -z "$SAMPLES" ] || rm -rf "$SAMPLES"
}
trap cleanup EXIT

# Writes the sample models of tests/Decoding.Tests/GlbWriterTests.cs into $SAMPLES: the test that
# compares them with their recorded files also writes them when PDZ_GLB_SAMPLES names a folder.
write_samples() {
    command -v dotnet >/dev/null 2>&1 || die "'dotnet' was not found; run tools/setup-toolchain.sh"
    SAMPLES="$(mktemp -d)" || die "could not create a temporary folder"
    say "writing the sample models"
    PDZ_GLB_SAMPLES="$SAMPLES" dotnet test "$ROOT/tests/Decoding.Tests" --nologo -v q \
        --filter "FullyQualifiedName~GlbWriterTests.Write_SampleEqualsTheRecordedFile" >/dev/null \
        || die "the sample models' test failed; run tools/test.sh --filter GlbWriterTests"
}

# True when the pinned validator is installed: its marker names this release and checksum.
installed() {
    [ -f "$VALIDATOR_DIR/$MARKER" ] && [ "$(cat "$VALIDATOR_DIR/$MARKER")" = "$VALIDATOR_MARKER_TEXT" ] && [ -x "$VALIDATOR" ]
}

install_validator() {
    for tool in curl tar xz sha256sum find; do
        command -v "$tool" >/dev/null 2>&1 || die "'$tool' is needed to download the validator but was not found"
    done

    rm -rf "$SCRATCH" || die "could not clear $SCRATCH"
    mkdir -p "$SCRATCH/unpacked" || die "could not create $SCRATCH"

    say "downloading $VALIDATOR_TAR"
    curl --fail --silent --location --retry 3 --output "$SCRATCH/$VALIDATOR_TAR" "$VALIDATOR_URL" \
        || die "could not download $VALIDATOR_URL"
    local actual
    actual="$(sha256sum "$SCRATCH/$VALIDATOR_TAR" | cut -d' ' -f1)" || die "could not hash $VALIDATOR_TAR"
    [ "$actual" = "$VALIDATOR_SHA256" ] || die "checksum mismatch for $VALIDATOR_TAR; nothing from it was installed"

    tar -xJf "$SCRATCH/$VALIDATOR_TAR" -C "$SCRATCH/unpacked" || die "could not unpack $VALIDATOR_TAR"
    local binary built
    binary="$(find "$SCRATCH/unpacked" -type f -name gltf_validator | head -n 1)"
    [ -n "$binary" ] || die "$VALIDATOR_TAR does not contain gltf_validator"
    built="$(dirname "$binary")"
    chmod +x "$binary" || die "could not make the validator executable"
    printf '%s\n' "$VALIDATOR_MARKER_TEXT" > "$built/$MARKER" || die "could not write the validator's marker"

    mkdir -p "$TOOLCHAIN_ROOT" || die "could not create $TOOLCHAIN_ROOT"
    rm -rf "$VALIDATOR_DIR" || die "could not remove the incomplete $VALIDATOR_DIR"
    mv "$built" "$VALIDATOR_DIR" || die "could not move the validator to $VALIDATOR_DIR"
    say "installed the glTF Validator $VALIDATOR_VERSION in $VALIDATOR_DIR"
}

# ---------------------------------------------------------------- main

files=()
for arg in "$@"; do
    case "$arg" in
        -*) die "unknown argument '$arg' (usage: validate-gltf.sh [file.glb ...])" ;;
        *) files+=("$arg") ;;
    esac
done

if [ "${#files[@]}" -eq 0 ]; then
    write_samples
    for f in "$SAMPLES"/*.glb; do
        [ -f "$f" ] && files+=("$f")
    done
    [ "${#files[@]}" -gt 0 ] || die "the test wrote no sample models"
fi

command -v jq >/dev/null 2>&1 || die "'jq' is needed but was not found"
installed || install_validator

status=0
for f in "${files[@]}"; do
    if [ ! -f "$f" ]; then
        echo "validate-gltf: error: $f: no such file" >&2
        status=1
        continue
    fi

    # -o prints the report to stdout instead of writing it beside the file; the summary goes to stderr.
    report="$("$VALIDATOR" -o "$f" 2>/dev/null)"
    counts="$(printf '%s' "$report" | jq -r '.issues | "\(.numErrors) \(.numWarnings) \(.numInfos) \(.numHints)"' 2>/dev/null)"
    if [ -z "$counts" ] || [ "$counts" = "null null null null" ]; then
        echo "validate-gltf: error: $f: the validator gave no report" >&2
        status=1
        continue
    fi

    read -r errors warnings infos hints <<< "$counts"
    if [ "$errors" -eq 0 ]; then
        say "$f: $errors errors, $warnings warnings, $infos infos, $hints hints"
    else
        first="$(printf '%s' "$report" | jq -r '[.issues.messages[] | select(.severity == 0)][0] | "\(.code) at \(.pointer // .offset // "?")"' 2>/dev/null)"
        echo "validate-gltf: error: $f: $errors errors, $warnings warnings, $infos infos, $hints hints; the first is ${first:-not shown}" >&2
        status=1
    fi
done

exit "$status"
