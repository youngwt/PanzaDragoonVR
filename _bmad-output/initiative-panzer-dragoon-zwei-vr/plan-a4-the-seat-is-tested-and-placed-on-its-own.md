---
title: 'The seat is tested and placed on its own'
type: 'refactor'
ticket: ''
created: '2026-11-08'
status: 'built'
baseline_revision: '46e3fad0716036f075aae272f706e40311595bba'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Action item A4 of the riding the dragon retrospective (`epic-riding-the-dragon/epic-riding-the-dragon-retrospective.md`, R4, R5, R6, R9, R10). Several things about the seat and its tuning list are wrong or untested:
- The seat's arithmetic is in engine code that no test compiles: the signs of `seat_forward` and `seat_height`, the scale about the seat, the dragon's half-turn, and how a recentre and a tuning change keep each other's values.
- The seat waits on the board: a recentre while looking steeply up or down leaves the dragon where it was.
- `SitUnder` writes a global position and `Adjust` a local one.
- The tuning list shows two decimals where the built-ins have three.
- An A press on the frame that shows the list saves at once.

**Approach:** Move the seat's position, facing and scale into an engine-free helper in `game/Logic/`, as `BoardPlacement` is, with tests. `RiddenDragon` applies its result in one space. Seat the dragon as soon as the head is tracked, whether or not the board can be placed. In the tuning list, show three decimals and ignore A on the showing frame.

## Boundaries & Constraints

**Always:** What the player sees is unchanged for every tuning: the same position, facing and scale as today when the `Dragon` node's parent is at the origin, as it is. `game/Logic/` uses no engine code. Tests use `Subject_Behaviour` names. The tuning file's format, ranges, built-ins and log lines stay as they are.

**Never:** No change to the board's placement, the XR origin, the scroll, the models or the extraction. No new tuning setting. No rename of public tuning names.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Built-ins | Offsets 0, scale 1, head at (x, z) | Seat at (x, scene height, z), yaw a half-turn, scale 1 | None |
| Forward | `seat_forward` > 0 | Dragon moves towards +Z by that much, so the player is nearer its head | None |
| Height | `seat_height` > 0 | Dragon moves down by that much | None |
| Scale | `dragon_scale` 2 | Basis scaled 2 about the seat; position unchanged | None |
| Either order | Head placed then tuning changed, or tuning changed then head placed | The same pose | None |
| Looking straight down | Head tracked, no board pose | Seat placed; board placed when it can be | None |
| A on the showing frame | B and A pressed together while hidden | List shows; nothing saved | None |
| A held after showing | A pressed on a later frame | Saved, as now | None |

</frozen-after-approval>

## Code Map

- `game/RiddenDragon.cs` -- `Facing` (`:25`), `_sceneHeight`, `_headZ`, `_forward`, `SitUnder` (`:42-46`, writes `GlobalPosition`), `Adjust` (`:52-57`, writes `Position` and `Basis`). Keep `Carry`, the placeholder and the class summary's conventions; it applies the helper's pose and keeps no arithmetic of its own.
- `game/Logic/BoardPlacement.cs` -- the pattern: an engine-free static or small class returning a pose record, tested in `tests/GameLogic.Tests/`.
- `game/Main.Board.cs:36-62` -- `OnPoseRecentered` clears `_boardAndSeatPlaced`; `PlaceBoardAndSeat` returns before the seat when `BoardPlacement.InFrontOf` gives null. Give the seat and the board a flag each, both cleared on a recentre, and the seat placed once tracking exists. `game/Main.cs:68` tests the flag. Keep both log lines as they are.
- `game/Main.Tuning.cs:56-61` `PlacePlanesAndSeat`, and `game/Main.TuningMenu.cs:64-70` -- callers of `Adjust`; unchanged in meaning.
- `game/Logic/TuningMenu.cs` -- `Step` (`:54-72`): `pressedA` is computed after the B toggle; `Text()` (`:81-99`) formats with `"0.00"`. The log's `Tuning.Describe()` already uses three places (`Tuning.cs`, `Shown`).
- `tests/GameLogic.Tests/TuningMenuTests.cs` -- the existing list tests; update any that hold two-decimal text.
- `tools/README.md`, tuning list section -- say three decimals if it names the format.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/` (a new seat helper) -- the pose (position, yaw, scale) from the head's X and Z, the scene height and the three settings, remembering the last of each so either order gives the same pose.
- [x] `game/RiddenDragon.cs` -- apply that pose as one global transform; no arithmetic of its own.
- [x] `game/Main.Board.cs`, `game/Main.cs` -- seat and board each with its own flag, the seat placed as soon as the head is tracked.
- [x] `game/Logic/TuningMenu.cs` -- three decimals; A ignored on the frame that shows the list.
- [x] `tests/GameLogic.Tests/` -- the matrix's rows.
- [x] `tools/README.md` -- as mapped.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every suite passes.
- Given the build on the headset at the built-ins, when the author looks around after a recentre, then the dragon sits as before: wings either side, head in front, tail behind, under the player.
- Given the tuning list, when the author moves `seat_forward`, `seat_height` and `dragon_scale`, then the dragon moves and scales as before, and the values show three decimals.

## Implementation Notes

- `game/Logic/SeatPlacement.cs`: `SeatPose(X, Y, Z, Yaw, Scale)` and a small class that remembers the last head X and Z and the last three settings; `Under` and `Adjust` each return the pose, `Facing` is the half-turn. It starts at the scene's X and Z (both 0 today), as `Adjust` before the head was tracked kept them.
- `game/RiddenDragon.cs`: builds the helper from its global position in `_Ready` and sets `GlobalTransform` from each pose (`new Basis(Up, yaw).Scaled(scale)` at the seat); `SitUnder` and `Adjust` keep their signatures, so `Main.Tuning.cs` and `Main.TuningMenu.cs` are unchanged.
- `game/Main.Board.cs`: `_boardPlaced` and `_seatPlaced`, both cleared on a recentre; `PlaceBoardAndSeat` tries the board (now `PlaceBoard`) and then the seat, each while its flag is clear, so a head looking straight up or down still gets its seat. Both log lines are unchanged; when both are placed on one frame they come in the old order. `game/Main.cs` runs it until both flags are set.
- `game/Logic/TuningMenu.cs`: values as `"0.000"`; an A press on the frame whose B shows the list is dropped.
- The built-in `floor_depth` is 8.999 and `dragon_scale` 0.997, which the two-decimal list showed as 9.00 and 1.00; `Text_ListsEverySettingAndMarksThePick` now holds the three-decimal text.
- `game/Logic/SeatPlacement.cs.uid` was made by Godot's headless import.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: low 4; 3 patched, 1 rejected.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| The matrix row "looking straight down" has no test | low | rejected | The rule lives in `Main.Board.cs`, engine code no test project compiles; the seat needs only the head's X and Z, and `BoardPlacement`'s tests cover the null pose. A seam to test it adds complexity; the headset check covers it. |
| `Main.cs` `_Process` summary said "until the head is tracked" | low | patch | The loop now runs until both flags are set; summary reworded. |
| `Main.Board.cs` class summary said the seat is placed "at the same moments" as the board | low | patch | The seat now comes as soon as the head is tracked; summary reworded. |
| `TuningMenu.cs`: two edited doc lines ran to about 160 characters where the file wraps at about 110 | low | patch | Rewrapped, wording unchanged. |

## Verification

- 2026-11-08: `tools/test.sh`: 105 Python, 351 GameLogic, 253 Decoding, all pass. `dotnet build game/PanzaDragoonVR.csproj`: 0 warnings, 0 errors. Not yet run on the headset (`tools/build-quest.sh` and the manual checks are the author's).

**Commands:**
- `tools/test.sh` -- expected: all pass
- `tools/build-quest.sh` -- expected: installs and starts; the log's seat line appears after the head is tracked
- `git diff --cached --name-only` before commit -- expected: no game data

**Manual checks (if no CLI):**
- The second and third acceptance criteria, by the author in the headset.
