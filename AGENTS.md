# Agent notes

## Conventions

These describe what the code does today. Follow them in new code. The exceptions listed are known and left as they are; do not copy them.

### PC tools (`tools/`)

- An error is printed to stderr as `<tool>: error: <line>`, one problem per line, for example `write_manifest: error: ...`.
- Exit status is 0 on success and 1 on any error. In the epic-era Python tools, `-h` prints the usage and exits 0; the shell scripts take no `-h` and name their usage in the error line for an unknown argument.
- Shell scripts define `die` (prints the error line, exits 1) and `say` (prints `<tool>: <line>`), each with the script's own name as the prefix. `toolchain-env.sh` and `quest-adb.sh` are sourced, not run; `quest-adb.sh` uses the `die` of the script that sources it.
- Known exceptions:
  - `make_test_model.py` uses `argparse`, so a usage error exits 2.
  - `build-quest.sh` exits 2 when the app was installed but has not started.
  - `setup-toolchain.sh` exits 130 or 143 when interrupted.
  - `test.sh` defines no `die` or `say`, prints its errors with `echo`, passes extra arguments to `dotnet test` and exits with `dotnet test`'s status.
  - `write_manifest.py` with no arguments or an unknown option prints only the usage to stderr and exits 1, with no `error:` line.
  - The older extraction scripts (`saturn_disc.py`, `cgb_textures.py`, `mednafen_state.py`) predate these rules: on `-h` or a bad argument they print the usage to stdout and exit 1, with no `error:` line.

### Python style

- Epic-era Python (`manifest.py`, `write_manifest.py`, `make_test_model.py`) formats strings with `%`. The older extraction scripts use f-strings. Match the file you are in.

### Game log lines (`game/`)

- Every line starts with a tag, `PDZ-ASSETS` or `PDZ-FPS`, is one line, and ends without a full stop.
- Known exceptions: the two warnings in `game/Main.cs` (OpenXR not initialised, and the refresh-rate choice warning) have no tag and end in a full stop, and the warning in `game/FrameRateReadout.cs` when Android's log library cannot be called has its tag mid-line and ends in a full stop.

### Tests

- Python: `unittest`, in `tests/tools/`, with `test_snake_case` names.
- C#: xUnit, in `tests/GameLogic.Tests/`, with `Subject_Behaviour` names (two older tests, `CasesAreFound` and `NoReportBeforeTheWindowIsFull`, are a single phrase). `game/Logic/` uses no engine code and is compiled straight into the tests.
- `tools/test.sh` runs both.

### Rules kept in two places

The asset rules are in `tools/manifest.py` for the PC and in `game/Logic/` for the game, and the two must agree. A new or changed rule gets a shared case in `tests/fixtures/asset-rules/`, which both test suites run.
