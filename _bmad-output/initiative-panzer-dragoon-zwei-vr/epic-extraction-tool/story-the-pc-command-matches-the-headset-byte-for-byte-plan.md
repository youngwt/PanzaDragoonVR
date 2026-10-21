---
title: 'The PC command matches the headset byte for byte'
type: 'feature'
ticket: '11'
created: '2026-10-21'
status: 'built'
baseline_revision: '433eebce7b2b341d919dd664ef068c48b2bb8625'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/tools/README.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The epic's Done when 4 asks that the PC command, given the same disc image, writes byte-for-byte the same files as the headset. The PC command already runs the whole extraction, and stories 2.2 and 2.9 compared the two by hand once each. Nothing repeatable shows it, and the headset's .NET 9 build of the library is never run by the tests (deferred from story 2.2's review).

**Approach:** A script in `tools/` that pulls the headset's extracted files and `manifest.json`, runs `pdz-extract` on the same disc image on the PC, and compares the two trees byte for byte, saying which files differ if any. Run it on the author's disc, and confirm nothing extracted is tracked by git or packed in the APK.

</frozen-after-approval>

## Implementation Notes

Oneshot: one script and a README section; the extraction itself is unchanged.

- `tools/compare-headset.sh <disc.cue|disc.iso>`: finds the headset (`quest-adb.sh`), checks it has a `manifest.json`, pulls `manifest.json` and `images/` with host `adb` into `extracted/compare-headset/headset/`, runs `pdz-extract` into `.../pc/`, and `diff -rq`s them; exit 0 and "identical: N files" or exit 1 with the differing files (all in `differences.txt`). Follows the `tools/` error and exit rules (no `-h`, as `AGENTS.md` says of the shell scripts).
- `tools/README.md`: a section "The PC and the headset write the same files", including that this is the only check running the library's .NET 9 build.
- **Result, 2026-10-21:** on the author's Japanese disc, against the headset's extraction of 2026-10-18 22:31 (written by the app at story 2.10's code, whose extraction output is unchanged since), the script said "identical: 421 files, the manifest included" and exited 0. Error paths checked: `-h`, no argument, no headset.
- No disc data is tracked by git (`git ls-files` holds nothing under `rom/` or `extracted/`, and no `.bin`, `.cue` or `.iso`). The APK holds `PdzDecoding.dll`; its only PNGs are Android's own drawables under `res/drawable-*`.
- This closes story 2.2's deferred review item: the APK's .NET 9 build of the library now has a repeatable byte-for-byte check against the PC's .NET 10 build.

## Review Triage Log

Quick lens, 2026-10-21: high 0, medium 1, low 3, false 0, maybe-false 0, plus one rejected. Four patched.

- medium, patched: the whole headset `images/` was compared, so stray files from an earlier push would fail the check though every extracted file matched. Files the headset's manifest does not list are now set aside, with a line saying how many.
- low, patched: the script took `-h`, against `AGENTS.md` ("the shell scripts take no `-h`"). Removed; `-h` is now an unknown option naming the usage.
- low, patched: `quest-adb.sh`'s header and the README named only two scripts as its users. Both now name `compare-headset.sh` too.
- low, patched: the diff lines went to stderr without the `compare-headset: error:` prefix, and more than 20 were cut off silently. Each line is now prefixed, with a count and "and N more".
- low, rejected: host `adb pull` relies on the repository being under the home folder, which the sandbox shares. It always is on this machine (`AGENTS.md`, the environment notes); `push-assets.sh` streams with `tar` only because its source can be the sandbox's `/tmp`.

Re-run after the patches: "identical: 421 files, the manifest included", exit 0; `-h` gives the unknown-option error.

## Verification

**Commands:**
- The new script with the author's Japanese disc, after a launch has extracted on the headset -- expected: identical, exit 0.
- `git ls-files | grep -iE 'rom/|extracted/|\.bin$|\.cue$|\.iso$'` and `unzip -l game/build/PanzaDragoonVR.apk` -- expected: no disc data or extracted image.
