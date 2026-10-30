# Agent notes

## Nothing copyrighted in git

This is a hard rule. Never commit the disc image, anything extracted or decoded from it, BIOS files, or the soundtrack. The project is shared on a bring-your-own-disc basis: the game ships only the author's own placeholders, and the player's own disc supplies the real assets.

- The author's own dumps live in `rom/JP/` (the Japanese GS-9049 V1.001 as `.cue` and per-track `.bin`) and `rom/PAL/` (the European MK-81022 V1.000 as a 2048-byte-sector `.iso`).
- Keep `rom/`, `extracted/` and `tools/mednafen/` git-ignored, and write decoded output only under `extracted/`. That includes `pdz-extract`'s output folder.
- The APK must hold no disc data: the game extracts on the headset into its asset folder, which is outside the APK. After a build, `unzip -l game/build/PanzaDragoonVR.apk` should list `PdzDecoding.dll` and no disc data: no `.cue`, no track `.bin` from the disc (`assets/.godot/uid_cache.bin` is Godot's own and always there), and nothing under `disc/` or `images/e03/`.
- Before any commit, check the staged file list for game data.
- Values copied or measured from the disc stay out of git too, in notes as well as code: positions, colours, sizes, counts. A constant the game needs from the disc is read by the extractor, written to a config file in the asset folder and read at launch. Layouts, and where to look on the disc, are format notes and are fine.
- Tool source code, format notes, and test files the project generates itself (such as `tests/fixtures/asset-rules/`) are fine to commit.

## Conventions

These describe what the code does today. Follow them in new code. The exceptions listed are known and left as they are; do not copy them.

### PC tools (`tools/`)

- An error is printed to stderr as `<tool>: error: <line>`, one problem per line, for example `write_manifest: error: ...`.
- Exit status is 0 on success and 1 on any error. In the epic-era Python tools, `-h` prints the usage and exits 0; the shell scripts take no `-h` and name their usage in the error line for an unknown argument.
- Shell scripts define `die` (prints the error line, exits 1) and `say` (prints `<tool>: <line>`), each with the script's own name as the prefix. `toolchain-env.sh` and `quest-adb.sh` are sourced, not run; `quest-adb.sh` uses the `die` of the script that sources it.
- C# tools (`tools/pdz-extract/`) follow the same rules: `pdz-extract: error: <line>` on stderr, exit 1 on any error, `-h` prints the usage and exits 0.
- Known exceptions:
  - `make_test_model.py` uses `argparse`, so a usage error exits 2.
  - `build-quest.sh` exits 2 when the app was installed but has not started.
  - `setup-toolchain.sh` exits 130 or 143 when interrupted.
  - `test.sh` defines no `die` or `say`, prints its errors with `echo`, passes extra arguments to each `dotnet test` and exits with the status of the last one that failed.
  - `write_manifest.py` with no arguments or an unknown option prints only the usage to stderr and exits 1, with no `error:` line.
  - The older extraction scripts (`saturn_disc.py`, `cgb_textures.py`, `mednafen_state.py`) predate these rules: on `-h` or a bad argument they print the usage to stdout and exit 1, with no `error:` line.
  - `plane_probe.py` puts its usage inside the error line on a usage error (`plane_probe: error: usage: ...`), and exits 1 when the planes it unpacks differ from the save state's.

### Python style

- Epic-era Python (`manifest.py`, `write_manifest.py`, `make_test_model.py`) formats strings with `%`. The older extraction scripts use f-strings, and so does `plane_probe.py`, written beside them. Match the file you are in.

### The decoding library (`lib/PdzDecoding/`)

- All Saturn-format knowledge lives here: disc images, file systems, `.MDB`/`.CGB` textures, PNG writing. The game (`game/DiscExtraction.cs`) and the PC command (`tools/pdz-extract/`) only call it. It uses no Godot types.
- It targets `net9.0` (the APK) and `net10.0`. The game references it as a project.
- A disc image or disc file it cannot read throws `DecodingException`, whose message is one line with no full stop, so callers can put it after `<tool>: error: ` or a `PDZ-ASSETS` tag.
- Its output must be byte-for-byte the same on the PC and the headset. Do not use .NET's compressors for files it writes: they differ between runtimes (the PNG writer stores its data uncompressed for this reason).
- How textures are found and de-duplicated fixes their numbers, and so the asset IDs (`Textures.FindRecords` and `Textures.Number`). Change them only with the author's agreement.

### Game log lines (`game/`)

- Every line starts with a tag, `PDZ-ASSETS` or `PDZ-FPS`, is one line, and ends without a full stop.
- Known exceptions: the warning in `game/Main.cs` when OpenXR is not initialised and the refresh-rate choice warning in `game/Main.RefreshRate.cs` have no tag and end in a full stop, and the warning in `game/FrameRateReadout.cs` when Android's log library cannot be called has its tag mid-line and ends in a full stop.

### Tests

- Python: `unittest`, in `tests/tools/`, with `test_snake_case` names.
- C#: xUnit, in `tests/GameLogic.Tests/` and `tests/Decoding.Tests/`, with `Subject_Behaviour` names (two older tests, `CasesAreFound` and `NoReportBeforeTheWindowIsFull`, are a single phrase). `game/Logic/` uses no engine code and is compiled straight into `GameLogic.Tests`, which also references the library's project, for the tests only (to check the game's strings against the library's); `game/Logic/` itself references neither the library nor Godot. `Decoding.Tests` references the library's project. Decoding tests build their own small disc images and `.MDB`/`.CGB` pairs and never read the disc.
- `tools/test.sh` runs all three.

### Rules kept in two places

The asset rules are in `tools/manifest.py` for the PC and in `game/Logic/` for the game, and the two must agree. A new or changed rule gets a shared case in `tests/fixtures/asset-rules/`, which both test suites run.

The tuning file (`tuning.json`) is read in two places: `tools/push-tuning.sh` checks it is JSON as the game reads it (a byte-order mark allowed, `NaN` and `Infinity` not), and `game/Logic/Tuning.cs` reads it. Its built-in values are in four: `Tuning.BuiltIn` in `game/Logic/Tuning.cs`, `tools/tuning.example.json` (a test holds it to `Tuning.BuiltIn`), the tuning section of `tools/README.md` and the asset contract (`_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md`). A change to a setting, a range or a built-in value changes them all.
