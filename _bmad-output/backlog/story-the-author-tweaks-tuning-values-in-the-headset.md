---
id: 21
type: story
title: "The author tweaks tuning values in the headset"
parent: none
covers: []
after: [20]
assignee: ""
refined: false
hitl: true
risk: medium
---

# The author tweaks tuning values in the headset

## Description

In a development build, the author changes any of the nine tuning settings with a controller and sees the effect at once, saves them to `tuning.json` in the asset folder, and pulls that file to the PC with a tool. A player sees no menu unless an empty `development` file is put in their asset folder; the code ships in every APK, as the modes do.

## Acceptance Criteria

- Given the asset folder is in development mode, when the author presses B on the right controller, then a list of all nine settings shows or hides above the left controller.
- Given the list is showing, when the author moves the right stick up or down, then the picked setting changes; left or right changes its value within the setting's range, and the scene follows at once.
- Given the list is showing, when the author presses A, then `tuning.json` is written with all nine values and the list says it saved, or gives the reason it did not.
- Given a saved file, when the app is started again, then it uses those values with no problem on the board.
- Given a saved file on the headset, when the author runs the new pull tool on the PC, then the file is copied to the path given, byte for byte.
- Given the asset folder is not in development mode, then there is no list and the controller does nothing to the tuning.
- Given the settings' names and ranges, then the menu uses `Tuning`'s own table rather than a copy, and that table gains each setting's rate of change.
- Given any tuning, when `game/Logic/` writes it as text, then `Tuning.Parse` reads back the same values with no problems, as a test in `tests/GameLogic.Tests/`.
- Every statement that the file is never changed in the headset (the asset contract's Tuning section and its "Not in the contract" section) and README say it now can in development mode, and `AGENTS.md`'s "Rules kept in two places" names the new writer.

## References

- spike — _bmad-output/backlog/spike-a-debug-menu-for-tweaking-values-in-the-headset-plan.md (the answers)
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Tuning, Extraction and Not in the contract
- code — game/Logic/Tuning.cs, game/Main.Tuning.cs, game/Main.cs (`Dress` sets the `tiles` shader parameter), game/Logic/ExtractionMode.cs, tools/push-tuning.sh

## Notes

- Source: the author, 2026-11-01: "Keep the debug menu in for the development build".
- Assumption to confirm: "development build" means the asset folder's `development` mode, and the list starts hidden.
- Known: `game/openxr_action_map.tres` already binds `primary`, `ax_button` and `by_button`, so it needs no change. Controller readings are empty until the controllers wake.
- Known: development mode extracts again on every launch when a disc image is present, so using the menu carries that cost. The author should weigh this when confirming the assumption above.
- Known: the trial changed values at a rate set per setting, in units a second at full stick: 1 for `seat_forward`, 0.25 for `seat_height` and `dragon_scale`, 3 for `floor_depth` and `flight_speed`, 12 for `floor_tile`. A stick push past 0.7 moved the pick once, and it re-armed below 0.3. The author found these worked. The other three settings have no tried rate.
- Known: `adb` runs on the host from inside the Flatpak sandbox and the host cannot see the sandbox's temporary files, which is why `tools/push-tuning.sh` streams its file; the pull tool will likely need to stream too. `push-tuning.sh` has no check-only mode, and its check is of JSON only.
- Unknown: whether a development-mode extraction leaves a saved `tuning.json` in place; check it.
