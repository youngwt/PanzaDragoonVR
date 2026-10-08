#!/usr/bin/env bash
# Runs the unit tests for the game's engine-free logic on this computer. No headset is needed.
# See tools/README.md.
#
#   tools/test.sh                  run every test
#   tools/test.sh --filter Name    extra arguments go to 'dotnet test'

set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"

command -v dotnet >/dev/null 2>&1 || {
    echo "test: error: 'dotnet' was not found; run tools/setup-toolchain.sh" >&2
    exit 1
}

exec dotnet test "$here/../tests/GameLogic.Tests" "$@"
