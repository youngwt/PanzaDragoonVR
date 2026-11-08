---
title: "Extraction's dragons fail safely and a change in output needs a new version"
type: 'bugfix'
ticket: ''
created: '2026-11-08'
status: 'done'
baseline_revision: 'c4bfa0378360b643abac772f00158d7c8a2767e2'
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

**Problem:** Action item A3 of the riding the dragon retrospective (`epic-riding-the-dragon/epic-riding-the-dragon-retrospective.md`, R1, R2, R3, R8, R11). A run that writes everything but the dragons tells the player nothing on the board, and in release mode it deletes the disc image all the same. An error from the model writer stops the whole extraction, although `Dragons.FromDisc` promises it cannot. And nothing makes a change in what extraction writes raise `Extraction.Version`, which release mode trusts.

**Approach:** A missing dragon or floor and canopy becomes a line on the launch board. The writer's errors become the dragons' one-line notice. A recorded test ties extraction's whole output to the version. Two small tidy-ups ride along: the ridden form is no longer decoded twice, and two stale lines are corrected.

## Boundaries & Constraints

**Always:**
- Extraction's output stays byte-identical on both discs; `Extraction.Version` stays 0.4.
- The new board line follows the existing extraction lines in `game/Logic/` (`ExtractionLines`), tagged log lines as `AGENTS.md` says, with game strings checked against the library's where the tests already do so.
- Tests build their own data; nothing from the disc is committed, including in the recorded file.
- Decision (author, 2026-11-08): release mode still deletes the disc image after a run with a dragon or plane notice; the notice is a board line only, and the contract's release-mode rule is unchanged.
- Decision (author, 2026-11-08): the plan is kept whole, over the token guide, as one story.

**Never:** No change to texture numbering, to the decoders' output, to development mode, or to the rule that a cue naming an `.iso` is one image. No .NET compressor in the recorded test.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Dragons missing | Textures written, `DragonNotice` set | Board shows a problem naming the dragons and the notice; the ride is the placeholder | Log line as now |
| Planes missing | Textures written, `PlaneNotice` set | Board shows a problem naming the floor and canopy and the notice | Log line as now |
| Writer rejects a model | `GlbWriter.Write` or `Atlas` throws | No dragons, the rest written, one-line notice | Caught into the notice |
| Output changes, version does not | A decoder's bytes change | The recorded-output test fails, saying to raise `Extraction.Version` and record again | None |
| Version raised | `Extraction.Version` differs from the recorded one | The test fails, saying to record again with `PDZ_RECORD_EXPECTED=1` | None |
| All written | Both notices null | No new board line | None |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/Dragons.cs` -- `FromDisc` (about `:104-160`): its catch takes only `DecodingException`, and the writer is called at `:146` and `:153`. The ridden form is decoded again at `:152` only to change its root name. `Model` is a record, so `with { Name = RootName(RideId) }` renames it. Keep the decoded models from the first loop; the grey count is unchanged, because the ride was never counted.
- `lib/PdzDecoding/GlbWriter.cs:39,439`, `Atlas.cs:76,120` -- `ArgumentException` and `InvalidOperationException`. Do not change the writer.
- `lib/PdzDecoding/Extraction.cs` -- `ExtractionResult` (`:45-60`, with `PlaneNotice`, `DragonNotice`, `WroteAnything`), `PlaneLine()` and `DragonLine()` (`:120-123`), and `Version` (`:158`).
- `lib/PdzDecoding/PdzDecoding.csproj:11` -- `<Version>` says 0.3.0; make it 0.4.0.
- `game/DiscExtraction.cs` -- `Problem` (`:58`) is set only on failure, in `Report` (`:162-191`); `Succeeded` (`:195`) logs. `Main.Extraction.cs:41-46` puts `Problem` on the board through `WithExtractionProblem`.
- `game/Logic/ExtractionMode.cs:136-143` -- `DeleteAfterExtraction`'s summary says "a texture or a plane"; the caller passes `WroteAnything`, which counts models too.
- `game/Logic/` `ExtractionLines` and `tests/GameLogic.Tests/` -- the pattern for board lines and their tests.
- `tests/Decoding.Tests/DragonsTests.cs` (`DiscWithForms`, `Run`, `Run_MalformedGeometry_NoDragonsAndTheRestWritten` at about `:183`), `SyntheticDragon.cs`, `SyntheticDisc.cs` -- a synthetic disc with all ten forms and a seat table.
- `tests/Decoding.Tests/GlbWriterTests.cs:590-640` -- the `PDZ_RECORD_EXPECTED=1` recording pattern, and `expected/models/two_atlases.glb.sha256` for a recorded hash.
- `asset-contract.md`, Extraction and the launch board text; `tools/README.md`'s board and notices text -- add the new board line.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Dragons.cs` -- catch the writer's two exceptions into the notice, as `"{reading}: {message}"`; rename the ridden form with `with`. Expose the conversion so a test can reach it (for example a public `Dragons.Write(Model)` that throws a one-line `DecodingException`), as no synthetic disc can make the writer reject a decoded model.
- [x] `game/Logic/` (`ExtractionLines`), `game/DiscExtraction.cs` -- after a successful run, a problem line for each notice, as the matrix says.
- [x] `game/Logic/ExtractionMode.cs`, `lib/PdzDecoding/PdzDecoding.csproj` -- the summary and the version, as mapped.
- [x] `tests/Decoding.Tests/` -- the writer's rejection reaching a one-line notice; a recorded-output test: extract `DiscWithForms()` (textures, planes if the synthetic disc has them, the ten forms and the ride), hash every file written, in ordinal path order, with SHA-256, and compare with a recorded file `expected/extraction-output.sha256` that holds the version and the hash.
- [x] `tests/GameLogic.Tests/` -- the new lines, and no line when both notices are null.
- [x] `asset-contract.md`, `tools/README.md` -- the new board line.

**Acceptance Criteria:**
- Given `tools/test.sh`, when it runs, then every suite passes.
- Given `pdz-extract` on the JP cue and the PAL ISO, when the output is compared with the baseline's, then each is identical (`diff -r`).
- Given a one-byte change to a dragon's output in a scratch edit, when the Decoding tests run, then the recorded-output test fails with the "raise `Extraction.Version`" message; the edit is then reverted.

## Implementation Notes

- `Dragons.Write(Model)` is the new public seam: it wraps the writer's `ArgumentException` and `InvalidOperationException` in a one-line `DecodingException` ("the model cannot be written: ..."), and `FromDisc` writes every model through it. The writer's rejection is tested through `Write` directly; that `FromDisc` turns it into the notice rests on reading, as no synthetic disc reaches a rejection.
- The board shows the left-out parts under a new heading, `Part of the disc image was not extracted`, as details rather than problems, so they show however many problems the folder has (`LaunchReport.WithExtractionLeftOut`). A failure's `Problem` still takes precedence.
- The recorded-output test extracts a synthetic disc with one texture file, the planes' program, the ten forms and the ride. Its hash was recorded from the changed code; byte-identical output on both discs against the baseline shows nothing changed. A scratch one-byte edit made it fail with the "raise `Extraction.Version`" message, and was reverted.
- `tools/README.md`'s board section was renamed to "The board while extracting, and when it fails or leaves a part out", links updated.
- Checks: `tools/test.sh` 105 Python, 341 GameLogic, 253 Decoding, all pass; `pdz-extract` on both discs identical to `extracted/retro4-*`.
- Not run on the headset: the new board lines cannot be triggered by either of the author's discs, which both decode fully.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: low 3; 1 patched, 2 rejected.

| Finding | Verdict | Route | Evidence |
|---------|---------|-------|----------|
| `tools/README.md` board table: both links' text still named the old heading | low | patch | Read the rows; the implementer changed both links' text to the new heading. |
| The matrix row "writer rejects a model" is tested only at `Dragons.Write`, not through `FromDisc`; `OneLine` never sees a multi-line message | low | rejected | `FromDisc` calls `Write` inside the `try` whose `catch (DecodingException)` builds the notice (`Dragons.cs`); no synthetic disc reaches a rejection, as the plan says, and decoded names are `nodeNN`, so no multi-line message can arise. A seam to test it adds complexity. |
| `Dragons.Write(null)` gives .NET's parameter suffix in the message | low | rejected | `FromDisc` never passes null; only a direct caller of the new method could, and none exists. |

## Design Notes

The recorded file is one line, `<version> <sha256>`. When `Extraction.Version` equals the recorded version and the hash differs, the test fails with "extraction's output changed: raise `Extraction.Version` and record again". When the versions differ, it fails with "record again with `PDZ_RECORD_EXPECTED=1`". The generator is inside every `.glb` and the manifest, so raising the version changes the hash.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `pdz-extract` on both discs into `extracted/a3-*`, then `diff -r` against `extracted/retro4-JP` and `extracted/retro4-PAL` -- expected: identical
- `git diff --cached --name-only` before commit -- expected: no game data
