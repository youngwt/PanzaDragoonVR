---
id: 6
type: bug
title: "The game cannot ask Android for its files folder on the Quest 3"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: low
severity: P3
---

# The game cannot ask Android for its files folder on the Quest 3

## Description

On the Quest 3 the game asks Android for its external files folder and gets nothing back, so it uses the fixed path `/storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files`. That path is right, but the request should work.

## Reproduction

1. Build and launch the game on the Quest 3 with `tools/build-quest.sh`.
2. Read `adb logcat -d | grep PDZ-ASSETS` while it runs.
3. Actual: `Android gave no external files folder; using /storage/emulated/0/Android/data/io.github.youngwt.panzadragoonvr/files`. Expected: the folder Android names, with no fallback line.
Seen on every headset launch from 2026-10-14 to 2026-10-16.

## Cause Hypothesis

The call through Godot's `AndroidRuntime` singleton (`getApplicationContext().getExternalFilesDir(null)`) returns a null object or a type the C# bridge cannot convert, rather than throwing.

## References

- parent — none
- plan — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/story-textures-from-the-app-s-files-folder-plan.md, Implementation Notes
- usage — tools/README.md, Your own images in the headset

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- The fixed path works, so players are unaffected; the extraction tool epic writes to the same folder and should know which route is real.
- Dropped: moved into the In-game extraction epic at its inception (user, 2026-10-16); see `_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/tickets.toml`, entries 1 and 7 for the empty asset list, entry 8 for the folder lookup.
