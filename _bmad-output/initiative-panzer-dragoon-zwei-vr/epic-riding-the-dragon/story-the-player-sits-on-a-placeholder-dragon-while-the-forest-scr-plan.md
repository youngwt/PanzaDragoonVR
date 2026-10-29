---
title: 'The player sits on a placeholder dragon while the forest scrolls'
type: 'feature'
ticket: '1'
created: '2026-10-29'
status: 'built'
baseline_revision: 'c5f4a25ec7d2cbc92a25c8f06de83113c3382115'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The player floats over the scrolling forest with nothing under them. CAP-5 needs them on the dragon's back, and the rest of the epic needs a place in the game where a dragon model loads by ID before any `.MDB` geometry is decoded.

**Approach:** The tracer bullet for epic 4. The game asks the asset folder for a model under the new ID `dragon.ride`. When the folder supplies one, it uses that model; otherwise it shows a built-in grey placeholder dragon with a distinct head, tail and two wings. Either way, the dragon is held fixed under the player at a built-in seat while the floor and canopy scroll on as now.

## Boundaries & Constraints

**Always:**
- Load the model the same way `e03.flyer` is loaded (`AssetFolder.Model`), with the same fallback, logging and launch-board lines. A model that fails to load falls back to the placeholder and leaves the other assets alone.
- Follow the glTF axes in the contract: a model's front is +Z. The game flies towards -Z, so whatever holds the dragon turns it 180° about Y. The placeholder is built in model axes (front +Z) and held by the same node, so a hand-made `.glb` tests the same convention.
- A model's origin is the seat: the holder puts the model's origin at the built-in seat point, and the placeholder's origin is on top of its back, half-way along.
- Decision (author, 2026-10-29): the built-in seat puts the top of the dragon's back 0.4 m above the play area's floor, directly below the play area's centre, so a seated rider is astride it. A standing player's legs pass into the body, and that is accepted.
- The placeholder is the author's own shapes in code (boxes are enough), grey, lit like the grey cube. The head is a different shade from the body, so front and back can be told apart at a glance.
- `dragon.ride` is added to the asset contract: the ID, its placeholder line beside the flyer's, and the asset-folder text that lists what the game uses. `tools/README.md` is updated to match.

**Never:**
- No tuning-file settings for the seat (story 4.4), no model decoding or extraction (4.5), and no wing motion (backlog 18).
- Do not move the player or the XR origin, and do not change the scroll, the flyer or the board.
- Nothing from the disc in the placeholder or the repo.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| No asset folder or no `dragon.ride` | Folder lacks the ID | Grey placeholder dragon under the player; board lists `dragon.ride` among the placeholders | No error expected |
| Own model | Manifest lists `dragon.ride` as a valid `.glb` | That model sits at the seat, turned to face the flight, and the placeholder is not drawn | No error expected |
| Bad model | `dragon.ride` file unreadable or not self-contained | Placeholder dragon | Board and log carry the model's problem line, as for `e03.flyer` |
| Model loads but cannot be placed | Exception while the dragon takes the model | Placeholder dragon | `PDZ-ASSETS dragon.ride: the model could not be shown (...); using the placeholder`, as the flyer's line |
| Extraction finishes | Folder read again | The dragon is re-dressed like the flyer; an earlier model is freed | As at launch |

</frozen-after-approval>

## Code Map

- `game/AssetIds.cs` -- add `DragonRide = "dragon.ride"` under a doc comment naming its placeholder, beside `E03Flyer`.
- `game/FlyingCube.cs` -- the pattern to copy: `Carry(Node3D?)` swaps the model in and frees the previous one. Do not change it.
- `game/Main.cs` -- `LoadAssets` returns a tuple of `(floor, canopy, flyer, report)`; add the dragon (`assets.Model(AssetIds.DragonRide)`), freeing it in the catch as the flyer is. `ShowAssets` carries the flyer inside a try/catch; do the same for the dragon. Update the class doc comment, which lists what the scene holds.
- `game/Main.Extraction.cs:43-50` -- the second caller of `LoadAssets`/`ShowAssets`; pass the dragon through.
- `game/Main.tscn` -- the scene; the XR origin is at the world origin, the camera is at 1.6 m, the board at (0, 1.6, -2.2) and the flyer circles at a radius of 4 m between 1.2 and 2.4 m high. Add a `Dragon` node; the placeholder must stay below the board and the flyer's path.
- `game/PlaceholderTextures.cs` -- the existing placeholder-making style (static, from code).
- `game/Logic/Tuning.cs` -- `EyeHeight` 1.6 m above the play area's floor is the standing assumption the scroll uses; read only.
- `_bmad-output/.../asset-contract.md` lines 20-40 (folder layout), 95-105 (ID prefixes and placeholders) -- add `dragon.ride`.
- `tools/README.md` lines 120, 311 and 405-435 -- where the game's IDs, the grey cube and "your own model" are described; add the dragon and how to supply `dragon.ride` (`make_test_model.py`, then `write_manifest.py my-assets dragon.ride=models/<file>.glb`).

## Tasks & Acceptance

**Execution:**
- [x] `game/AssetIds.cs` -- add `DragonRide` -- the one ride ID that epic 4 decided on.
- [x] `game/RiddenDragon.cs` (new) -- a `Node3D` that builds the grey placeholder in model axes on `_Ready`, is turned 180° about Y so that +Z points the way of flight, and has `Carry(Node3D?)`, which shows a model in place of the placeholder (or the placeholder again with null) and frees any earlier model -- it mirrors `FlyingCube` so the two read alike.
- [x] `game/Main.tscn` -- add a `Dragon` node using that script, at the built-in seat point -- puts the dragon under the player.
- [x] `game/Main.cs`, `game/Main.Extraction.cs` -- load `dragon.ride`, pass it through, and carry it in `ShowAssets` with the flyer's fallback -- the model path and the error path.
- [x] `_bmad-output/.../asset-contract.md`, `tools/README.md` -- document `dragon.ride` and its placeholder -- an ID is a promise.

**Acceptance Criteria:**
- Given no asset folder, when the app starts in the headset, then a grey dragon is under the player, with its wings either side, its head in front (the way the floor flows from) and its tail behind, and it stays in place while the forest scrolls for a minute at 90 Hz.
- Given `make_test_model.py`'s pyramid listed as `dragon.ride` and pushed with `push-assets.sh`, when the app starts, then the pyramid sits at the seat in place of the placeholder and the board does not list `dragon.ride` as a placeholder.
- Given the work is done, when `tools/test.sh` runs, then every suite passes.

## Design Notes

The placeholder's size gives the scale for later stories: a body about 4 m long and 1 m wide, a head on a short neck about 2.5–4 m in front of the seat, a thinner tail about 3 m behind, and flat wings about 3 m long either side. Low-poly boxes are enough; the tracer bullet only has to show which way the dragon faces, where it is, and that it stays put. With the back at 0.4 m, keep the head below about 1.2 m above the play area's floor so that it passes under the board.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all suites pass.
- `tools/build-quest.sh` -- expected: the APK builds, installs and starts; `unzip -l game/build/PanzaDragoonVR.apk` lists no disc data.

**Manual checks (if no CLI):**
- In the headset, as in the first two acceptance criteria; the log line `PDZ-ASSETS supplied: ...; placeholders: ...` names `dragon.ride` correctly in each case.


## Implementation Notes

- Matrix test audit: the matrix rows are scene behaviour in `game/` (engine code), which no test suite compiles; `GameLogic.Tests` covers only `game/Logic/`. The rows were checked by running the game instead: rows 1 to 3 on the desktop with no folder, the pyramid as `dragon.ride`, and a cut-short file; rows 1 and 5 in the headset (placeholder at launch and again after extraction re-read the folder, 90 Hz). Row 4 was not triggered; its code copies the flyer's.
- Godot created `game/RiddenDragon.cs.uid` on import; it is committed with the script.

## Review Triage Log

Pass 1 (quick): high 0, medium 0, low 6, false 0, maybe-false 0.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| Matrix row 1 and AC 2 say the board lists placeholders; `LaunchReport.Build` shows only problem lines, and only the log's `supplied: ...; placeholders: ...` line names them | low | rejected | Real, but the fix is to the plan's frozen wording; the Verification section already checks the log line, which the desktop and headset runs showed correct. |
| `game/Main.cs` ShowAssets doc comment lost its "and" | low | patch | Read the diff: "placeholders flies the model". |
| `tools/README.md:120` list has two "and"s | low | patch | Read the diff. |
| `tools/README.md` "When a model is rejected" says "keeps the grey cube" and "The ground and sky keep what they loaded", now also covering `dragon.ride` | low | patch | The section is shared by both models since this change. |
| `asset-contract.md:9` summarises placeholders as "plain images, grey cubes" | low | patch | The grey dragon is now a placeholder too. |
| A loaded dragon node leaks if anything before its try/catch in `ShowAssets` throws | low | rejected | Needs `Dress`, `ApplyScroll` or the flyer's fallback to throw, which no run has shown; the same holds for the flyer before this change; a guard adds complexity for an unseen case. |
