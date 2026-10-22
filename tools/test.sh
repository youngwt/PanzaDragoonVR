#!/usr/bin/env bash
# Runs the unit tests on this computer: the Python tests for the scripts in tools/, then the C# tests
# for the game's engine-free logic and for the decoding library. No headset is needed. See tools/README.md.
#
#   tools/test.sh                  run every test, Python and C#
#   tools/test.sh --filter Name    extra arguments go to 'dotnet test'; only the C# tests run

set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=toolchain-env.sh
. "$here/toolchain-env.sh"

python_status=0
if [ "$#" -eq 0 ]; then
    if command -v python3 >/dev/null 2>&1; then
        # -B: leave no __pycache__ folders behind.
        python3 -B -m unittest discover -s "$here/../tests/tools" -t "$here/../tests/tools" || python_status=1
    else
        echo "test: error: 'python3' was not found" >&2
        python_status=1
    fi
fi

command -v dotnet >/dev/null 2>&1 || {
    echo "test: error: 'dotnet' was not found; run tools/setup-toolchain.sh" >&2
    exit 1
}

dotnet_status=0
for project in GameLogic.Tests Decoding.Tests; do
    dotnet test "$here/../tests/$project" "$@" || dotnet_status=$?
done

if [ "$python_status" -ne 0 ]; then
    echo "test: error: the Python tests failed" >&2
    exit 1
fi
exit "$dotnet_status"
