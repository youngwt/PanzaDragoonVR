---
title: 'The floor scrolls endlessly beneath the player in the headset'
type: 'feature'
ticket: '1'
created: '2026-10-22'
status: 'built'
baseline_revision: 'f3bf37325a8cc905b310054ff93fc0d51ca6bdae'
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

**Problem:** The ground and sky stand still: the player is meant to be flying through the Episode 3 forest (CAP-4), and nothing in the scene says they are moving. This is the epic's tracer bullet: motion first, on whatever images are there, before the real planes exist.

**Approach:** Scroll the image on each plane rather than move the planes: each frame the tiling shader's image is shifted along the direction of flight, wrapped within one tile so it repeats for ever with no seam. The ground moves at a steady flight speed; the sky moves as a flat backdrop at the floor's on-screen speed, that is at the same angle per second overhead as the floor below (the epic's canopy Decision). Speeds and distances are constants for now; story 3.3 moves them into a tuning file.

## Boundaries & Constraints

**Always:**
- Works the same on the placeholders and on any `e03.floor` / `e03.canopy` the folder supplies, and keeps scrolling smoothly across the re-dress after extraction.
- Flight is toward the play area's forward direction (−Z): the patterns travel towards +Z, under and over the player.
- The scroll maths (wrap within a tile, the sky's speed from the floor's) is engine-free in `game/Logic/` with xUnit tests.
- One frame's step comes from that frame's delta, so the motion keeps pace with 90 Hz.

**Never:**
- No tuning file, no new asset IDs, no change to the asset contract or the decoding library (stories 3.3 and 3.4).
- The planes, their sizes and positions in `Main.tscn` do not change; the flyer, board and readout are untouched.
- Do not rely on the texture's own repeat mode; keep the shader's wrap (backlog spike 10).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Steady step | offset 0.25 tile, speed 5 m/s, delta 1/90 s, tile 2 m | offset 0.25 − (5/90)/2 tiles, in [0, 1) | No error expected |
| Wrap | offset 0.01, step 0.03 tile | 0.98, never negative or ≥ 1 | No error expected |
| Long frame | delta 0.5 s (hitch) | step wraps to [0, 1) however many tiles it spans | No error expected |
| Sky speed | floor 5 m/s, floor 1.6 m below the eye, sky 38.4 m above | sky 120 m/s: the same angle per second | No error expected |
| Bad distances | floor depth ≤ 0 or not finite | sky speed equals the floor speed | No error expected |

</frozen-after-approval>

## Code Map

- `game/Main.cs` -- `TilingShader` (uniforms `image`, `tiles`; `uv = UV * tiles`, `textureGrad(image, fract(uv), ...)`), `Dress(plane, image, tileMetres)` makes a new `ShaderMaterial` per call (called from `ShowAssets`, at launch and after extraction), `GroundTileMetres` 2, `SkyTileMetres` 20, `_Process` (board, then `WatchExtraction`).
- `game/Main.tscn` -- `Ground` at y 0 and `Sky` at y 40 (flip_faces), both `PlaneMesh` 2000×2000 m; camera default height 1.6. Do not edit.
- `game/Main.*.cs` -- partial-class files by job (ticket 3.7); a new job goes in its own `Main.<Job>.cs`.
- `game/Logic/BoardPlacement.cs` + `tests/GameLogic.Tests/BoardPlacementTests.cs` -- the pattern for engine-free maths with tests (`game/Logic` is compiled straight into `GameLogic.Tests`).
- `tools/build-quest.sh`, `tools/quest-adb.sh` -- build, install and launch on the Quest 3; logcat with tag `godot`.

## Tasks & Acceptance

**Execution:**
- [ ] `game/Logic/PlaneScroll.cs` -- engine-free: the next offset in tiles from offset, speed (m/s), delta (s) and tile size (m), wrapped to [0, 1); the sky's speed from the floor's speed, the floor's depth below the eye and the sky's height above it, falling back to the floor speed on bad distances -- the matrix rows.
- [ ] `tests/GameLogic.Tests/PlaneScrollTests.cs` -- one test per matrix row.
- [ ] `game/Main.cs` -- the shader gains a `uniform vec2 offset` (tiles) added after `UV * tiles`; `Dress` keeps the material it makes so the scroll can reach it; `_Process` calls the scroll.
- [ ] `game/Main.Scroll.cs` -- the scroll job: flight speed and assumed distances as named constants (floor 5 m/s, eye 1.6 m above the floor, sky at the scene's 40 m), the two offsets, and each frame's update of both materials; offsets persist across a re-dress.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then all tests pass, the new ones among them.
- Given the game project, when it is built, then it has 0 warnings.
- Given the Quest 3 running the app on the placeholders, when the author watches for a minute looking down, ahead and up, then the floor and sky flow past continuously towards and under them with no seam, jump or judder, the readout holds 90 Hz, and the sky seems to move at the same speed as the floor.

## Implementation Notes
- **Headset, 2026-10-22, with the author:** on the placeholders the floor flowed towards the author and under their feet with no jumps or judder. The author asked for 4 m/s rather than 5 for comfort, so `FlightSpeed` is now 4 m/s and the sky 96 m/s; story 3.3 makes both tunable. The game log showed only the known first "running at 72 Hz" warning before the session's focus request; the implementer's earlier run held 90 Hz on the readout. The sky's apparent speed was not remarked on.

## Plan Change Log

## Review Triage Log

Quick lens, 2026-10-22: high 0, medium 0, low 2, false 0, maybe-false 0. The reviewer re-ran the GameLogic tests and the game build, and confirmed from Godot's PlaneMesh that V runs along +Z and flip_faces leaves the UVs alone, so a falling V offset moves both patterns towards +Z.

- low, patch: `PlaneScroll.Next`'s summary did not say a speed or delta that is not finite gives 0. Fixed by one clause in the summary; Godot never hands `_Process` such a delta, so no test was added.
- low, rejected: `EyeHeight` and `SkyHeight` in `Main.Scroll.cs` repeat the scene's 1.6 and 40 with nothing linking them. A stopgap the plan asked for: story 3.3 replaces them with tuning values that set the planes' positions.
- Acceptance criterion 3 (the author in the headset) is not a defect; it is checked with the author before the plan is presented.

## Design Notes

- Scrolling the image, not the plane, keeps the 2000 m planes centred on the player for ever; the offset is wrapped each frame so it never grows and loses precision.
- The sky's speed: a point straight overhead at height h crosses the same angle per second as one straight below at depth d when `sky = floor × h / d`. With the scene's numbers that is 24× the floor's speed, which is what makes a far plane read as a flat backdrop. The ratio is the canopy tuning value story 3.3 exposes.
- The sign: PlaneMesh's V runs along +Z, so a pattern moving towards +Z needs the V offset to decrease; check it in the headset and flip the sign if the floor runs backwards.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass.
- `dotnet build game/PanzaDragoonVR.csproj --no-incremental` -- expected: 0 warnings, 0 errors.
- `tools/build-quest.sh` -- expected: installs and launches; logcat shows no new warnings.

**Manual checks (if no CLI):**
- The author in the headset, as in the third acceptance criterion.
