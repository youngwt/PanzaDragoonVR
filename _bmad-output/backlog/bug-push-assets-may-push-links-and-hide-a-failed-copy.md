---
id: 5
type: bug
title: "`push-assets.sh` may push symbolic links and report success over a failed copy"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: low
severity: P3
---

# `push-assets.sh` may push symbolic links and report success over a failed copy

## Description

When a folder holding a symbolic link is pushed, or the copy fails on the headset, `push-assets.sh` may still say it is done. The game would then report files as missing although the push looked fine.

## Reproduction

1. Make a folder with `write_manifest.py` where one listed image is a symbolic link to a file elsewhere; `--check` passes.
2. Run `tools/push-assets.sh <folder>` with the Quest 3 connected.
3. Expected: the real file arrives, or the push fails with a message. Suspected: a link arrives, or nothing, and the script says done.
4. Separately, make the copy fail on the headset (for example a full or read-only folder) after an earlier good push. Expected: the push fails. Suspected: it says done, because `manifest.json` from the earlier push is still there.

## Cause Hypothesis

`tar` is run without following links, Android's shared storage likely refuses links, and `adb exec-in` probably does not pass back the remote exit status, so the only check is that a `manifest.json` exists.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md, finding R4; action A3
- usage — tools/README.md, Your own images in the headset

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Unverified: found by reading, not run.
