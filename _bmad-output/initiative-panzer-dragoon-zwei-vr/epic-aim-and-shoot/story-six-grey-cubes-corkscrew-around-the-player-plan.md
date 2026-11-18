---
title: 'Six grey cubes corkscrew around the player'
type: 'feature'
ticket: '4'
created: '2026-11-18'
status: 'built'
baseline_revision: '919552990fae2f4ee910d5105f4f6eba1cb26b28'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** There is one grey cube circling the player, and nothing to shoot at once shooting arrives (5.6, 5.7). CAP-8 asks for targets: the epic settled on six cubes that corkscrew around the player, each of which can be destroyed and returns to its path a few seconds later.

**Approach:** An engine-free flight in `game/Logic/` gives each of six cubes its point on a corkscrew path at any time, and keeps which cubes are destroyed and when each returns. A game node replaces the one `Cube` with six cubes flown from it, each carrying its own copy of `e03.flyer` when the folder supplies one. Nothing in the game destroys a cube yet; the shots that do are 5.6 and 5.7, which call the flight's destroy.

## Boundaries & Constraints

**Always:**
- The six paths are spread round the player: each circles the player's origin, starting a sixth of a lap apart, at different radii and heights, and twists round its own circle (a corkscrew) as it goes. All six stay between 3.5 m and 5.5 m from the player's origin horizontally, so they are inside the reticle's 6 m sphere and clear of the dragon's head and wings, and between 0.5 m and 3.5 m above the play area's floor.
- The feel values (lap time, radii, heights, corkscrew radius and twists, the return time) are built-in constants in the flight, not tuning settings (epic decision, 2026-11-11). The return time is 4 s.
- A cube keeps flying its path while destroyed, unseen, and reappears where its path has reached when it returns; it is not reset to a start point.
- Each cube still tumbles as the one cube does today.
- `e03.flyer` keeps its one ID, its one load and its one line in the log. When the folder supplies it, each cube holds its own copy; the board and log report it once. If it cannot be flown, every cube shows the grey cube, through `CarryOrPlaceholder` as now.
- The flight is tested in `GameLogic.Tests` with `Subject_Behaviour` names.

**Never:**
- No shooting, hit test, colour change or sound; those are 5.5 onwards.
- No new asset ID, no tuning setting, no change to the asset rules.
- No physics bodies or collision shapes.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Flying | Any time t | Six positions, each within the bounds above, each path closed on itself over a lap | None |
| Destroyed | `Destroy(i)` at t | Cube i not present from t until t + 4 s; the others unaffected | None |
| Returned | t + 4 s reached | Cube i present again, at its path's point for that time | None |
| Destroyed twice | `Destroy(i)` while already destroyed | No change; it returns 4 s after the first | None |
| Bad index | `Destroy(-1)` or `Destroy(6)` | Throws `ArgumentOutOfRangeException` | Caller's bug |
| Model supplied | `e03.flyer` a valid `.glb` | Six copies, one per cube; the grey cubes hidden | None |
| Model cannot be flown | Carrying throws | Every cube grey; one log line names `e03.flyer` | `CarryOrPlaceholder` |
| After extraction | Folder read again | The six copies replaced, or all six grey | None |

</frozen-after-approval>

## Code Map

- `game/FlyingCube.cs` -- today's single cube: a `MeshInstance3D` whose own mesh is the grey cube, with its own circle (`PointOnPath`), a tumble, and `Carry(Node3D?)` that swaps the mesh for a model. Keep the tumble and `Carry`; its path moves into the flight, so it takes its position from its parent instead of computing one, and shows or hides itself when told.
- `game/Main.tscn:79-83` -- the `Cube` node (`BoxMesh_cube`, `Material_cube`, script `FlyingCube.cs`). Replace it with one `Cubes` node whose script builds the six cubes from the same box mesh and material, or keep six `FlyingCube` nodes in the scene under it; either way the mesh and material stay the author's own.
- `game/Main.cs:108-109` -- `var cube = GetNode<FlyingCube>("Cube"); CarryOrPlaceholder(AssetIds.E03Flyer, "flown", flyer, cube.Carry);`. The new node's `Carry(Node3D?)` takes the one loaded model, gives the first cube that model and each other cube `model.Duplicate()`, and with null shows all six grey. Also the class summary (lines 8-22) and `ShowAssets`' summary name "a flyer circling the player" and "the grey cube".
- `game/AssetFolder.cs:94` `Model(id)` builds the model with `GLTFDocument.GenerateScene`, whose nodes are owned by the model's root, so `Duplicate()` copies the whole tree.
- `game/Logic/ReticlePlacement.cs:16` -- its `Radius` comment names "the cube's path (4 m)"; update to the new radii.
- `game/Main.Reticle.cs:18` -- "the cube" becomes "the cubes".
- `game/Logic/SeatPlacement.cs`, `tests/GameLogic.Tests/SeatPlacementTests.cs` -- the pattern for an engine-free placement class and its tests. Use plain floats or a small record for points, as `ReticlePoint` does; `game/Logic/` uses no Godot types.
- Docs: `tools/README.md:120` ("a grey cube flying in a circle right around you") and its "Your own model in the headset" section; `asset-contract.md:106` ("The engine spike's flying grey cube is the placeholder for `e03.flyer`") -- now six grey cubes, each carrying the model.

## Tasks & Acceptance

**Execution:**
- [x] `game/Logic/CubeFlight.cs` -- the six paths, `Advance(seconds)`, `Position(i)`, `IsPresent(i)`, `Destroy(i)`, `Count = 6`, `ReturnSeconds = 4`, time kept within one lap's worth so it stays exact however long the app runs (as `FlyingCube` does).
- [x] `tests/GameLogic.Tests/CubeFlightTests.cs` -- bounds over many times, closed paths, spread (no two cubes within 1 m of each other at the start), the matrix's destroy, return and bad-index rows.
- [x] `game/FlyingCube.cs`, new `game/CubeFlock.cs` (or the like), `game/Main.tscn` -- six cubes flown from the flight, each tumbling, hidden while destroyed, carrying its copy of the model.
- [x] `game/Main.cs` -- carry the flyer to all six; summaries.
- [x] Docs -- the README and asset contract lines in the Code Map, `ReticlePlacement.cs` and `Main.Reticle.cs` comments.

**Acceptance Criteria:**
- Given the headset, when the app starts, then six grey cubes corkscrew around the author, spread all round, and the log reads `PDZ-FPS target=90` with an average of about 90.
- Given `tools/test.sh`, when it runs, then every suite passes.

## Implementation Notes

- The flight uses the Design Notes' starting set as is: lap 14 s, R 4 and 5 m in turn, H 1.4 and 2.4 m in turn, corkscrew radius 0.5 m, 3 twists a lap. The horizontal distance then reaches exactly 3.5 m and 5.5 m, so the bounds test allows 1e-4 m; heights run 0.9 m to 2.9 m.
- After review, the low circles were raised from 1.4 m to 1.8 m, to clear the dragon's head: no cube goes below 1.3 m, and the high circles' top stays 2.9 m.
- A destroyed cube's time away is counted down in `Advance`, so it is independent of the lap time kept within one lap. At 90 Hz it returns 360 or 361 frames after its destruction, by floating-point rounding.
- `Cubes` (`game/CubeFlock.cs`) builds the six `FlyingCube`s in code from the scene's `BoxMesh_cube` and `Material_cube`, given as exports. The material is the cubes' `MaterialOverride` rather than a surface override, so it survives `Carry` swapping the mesh away and back.
- `CubeFlock.Carry` gives the first cube the model and each other cube `model.Duplicate()`; with null, or when `CarryOrPlaceholder` falls back after a throw, all six go grey and any copies they held are freed.
- `AssetIds.cs`'s summary of `E03Flyer` also names the six cubes.
- `game/CubeFlock.cs.uid` and `game/Logic/CubeFlight.cs.uid` were generated by Godot's import.
- Checked 2026-11-18: `tools/test.sh` all pass (Python 108, GameLogic 377, Decoding 294); `dotnet build game/PanzaDragoonVR.csproj` 0 warnings, 0 errors; `tools/build-quest.sh` installed and started on the headset, the APK listing `PdzDecoding.dll` and no disc data, the log reading `PDZ-FPS target=90 avg=90.0` (`e03.flyer` on its placeholder). The headset look is the author's.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 1 medium patched, 1 low patched.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| `CubeFlight.cs`: with the low circles at 1.4 m, cube 4 is about 0.97 m up and 3.75 m ahead when straight ahead, inside the dragon's head, against the Always rule "clear of the dragon's head" | medium | patch | The head sits about 3.4–4.4 m ahead with its top near 1 m at the built-in seat. The low circles were raised to 1.8 m (lowest point 1.3 m), and `Paths_PassOverTheDragonsHead` holds every cube at 1.25 m or more. |
| `CubeFlightTests.Paths_StayWithinTheBoundsAllLap` said three laps but sampled 41.1 s of 42 s | low | patch | Now 3100 steps of 0.0137 s, 42.47 s. |

## Design Notes

A path, for cube i, at lap angle θ (θ = 2π·t/lap + i·2π/6): its circle's centre `C = (R·sinθ, H, −R·cosθ)` as the one cube flies today, with the corkscrew round it in the plane of the outward direction `u = (sinθ, 0, −cosθ)` and up: `P = C + r·(cos φ·u + sin φ·up)`, with φ = k·θ + i·π/3 and k whole, so the path closes each lap. A starting set to try: lap 14 s, R alternating 4 and 5 m, H alternating 1.4 and 2.4 m, r 0.5 m, k 3. These sit inside the bounds; the author judges the feel in the headset.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `dotnet build game/PanzaDragoonVR.csproj` -- expected: 0 warnings, 0 errors
- `tools/build-quest.sh` -- expected: installs and starts; the APK lists `PdzDecoding.dll` and no disc data

**Manual checks (if no CLI):**
- Headset (the author): six grey cubes corkscrew around, spread all round; 90 Hz.
