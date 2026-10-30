---
title: 'Extraction writes the dragon forms as textured models'
type: 'feature'
ticket: '5'
created: '2026-10-30'
status: 'built'
baseline_revision: '41ff70f6c155ecaeb2912a67398c75b46caecbe9'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The game can load a model and the library can write one, but nothing turns the disc's dragons into models, so stories 4.6 and 4.7 have nothing to check or ride.

**Approach:** The decoding library decodes `.MDB` geometry as `zwei-disc-formats.md`, Models, describes it, into the `Model` type, and the extraction writes all ten dragon forms as `.glb` files under `dragon.form01` to `dragon.form10`, listed in the manifest, on the PC and the headset.

## Boundaries & Constraints

**Always:**
- Only the first mesh layout: each form's body tree. No shadow tree, no extra wing frames, no rider mesh, no second layout.
- Each mesh-bearing node is one `ModelPart`, named `node` plus its two-digit depth-first index (`node13`), with its origin at the translation of its matrix product, its corners relative to that.
- A form's model origin is the seat: the rider's offset for that form, read from the table in `1ST_READ` on the disc in hand, is subtracted before the conversion to glTF, which is `(-x, y, -z)` then a division by 4,000 (the spike's lead, recorded in the asset contract and still provisional).
- The table is found, not looked up by disc: compute the ten body roots from the `.MDB` files, search `1ST_READ` for them in the game's form order, and read the rider table 0x50 bytes after the match. The form order is layout knowledge and may be in code.
- Disc values stay out of git: no rider offsets, sizes or counts in code, tests, notes or commit messages. Tests build their own small `.MDB` and `1ST_READ` data.
- Output is byte-for-byte the same on the PC and the headset: matrix products use integer arithmetic, with one conversion to `float` at the end.

**Never:**
- No change to texture numbering, `FindRecords`, or the `.png` files or IDs already written. No shading: the contract reserves `COLOR_0`, so models show no per-corner light. No `dragon.ride` (story 4.7), no tuning settings (story 4.4), no game code that shows these models. No config file: nothing here needs one, because the seat is in each model's origin.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Both discs | Japanese or European disc | Ten `.glb` files and ten manifest entries of kind `model` | No error expected |
| Form's files missing | One `DRA` `.MDB` or `.CGB` absent | That form and the seat search are left out: no dragons, and textures, floor and canopy are still written | The result names the reason in one line, as the planes' notice does |
| Table not found | `1ST_READ` absent, or holds no match | No dragons; the rest is written | As above |
| Malformed geometry | A pointer or corner index out of range | No dragons; the rest is written | `DecodingException` caught into the notice, one line |
| Texture cannot be decoded | A polygon's colour mode is not decodable from its `.CGB` | That polygon is drawn in flat grey, and the count is in the result's line | No failure |
| Flat polygon | `CMDSIZE` 0 | A `ModelColour` from RGB555 `CMDCOLR`, as the contract says | No error expected |
| Three corners | Repeated index | Repeat kept | No error expected |

</frozen-after-approval>

## Code Map

- `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md`, Models -- the format: read it first. The form order and the table spacing (body roots table, then the rider table 0x50 later) are in The ten forms. The earlier version with measurements is `extracted/spike-mdb/zwei-disc-formats-with-measurements.md`; scripts there, especially `draw2.py`, are a working reference. Never commit them.
- `lib/PdzDecoding/Model.cs`, `GlbWriter.cs` -- the target. `ModelQuad` takes corners in Saturn order and flips; do not change the writer.
- `lib/PdzDecoding/Textures.cs` -- reuse the record type and colour mode 1 decoding by the polygon's four words. Do not change `FindRecords` or `Number`.
- `lib/PdzDecoding/Extraction.cs`, `Run` -- staging, progress, manifest, notices (`PlaneNotice`, `PlaneFiles`, `PlaneLine`, `Details`): follow the planes' pattern. A new file `lib/PdzDecoding/Dragons.cs` holds the geometry.
- `lib/PdzDecoding/Manifest.cs` -- check it lists kind `model` as the contract's manifest example does; `tools/manifest.py` and `game/Logic/` already read that.
- `tools/pdz-extract/`, `game/DiscExtraction.cs`, `game/Main.Extraction.cs` -- print or log the new line; the game only needs the result's line. Check what decides a re-extraction, and bump `Extraction.Version` if the existing rule needs it.
- `tools/validate-gltf.sh`, `tools/compare-headset.sh` -- the checks. `tests/Decoding.Tests/` -- synthetic style.
- `_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md`, `tools/README.md` -- IDs, paths (`models/dragon_form01.glb`), origin, the divisor.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Dragons.cs` -- decode a form's tree, matrices, vertices and polygons into a `Model`, and find the seat table -- the story's core.
- [x] `lib/PdzDecoding/Extraction.cs`, `tools/pdz-extract/`, `game/` -- write the forms through staging, list them, report the line -- PC and headset alike.
- [x] `tests/Decoding.Tests/` -- a test for each matrix row, with synthetic data, plus matrix order, flips, the axis conversion and the origin -- the format knowledge held in tests.
- [x] `asset-contract.md`, `tools/README.md` -- as the Code Map says -- the contract's rule on the divisor asks for it.

**Acceptance Criteria:**
- Given the Japanese and the European disc, when `pdz-extract` runs with output under `extracted/`, then each writes ten `.glb` files and `tools/validate-gltf.sh` reports no errors on any.
- Given the output on the PC and the headset, when `tools/compare-headset.sh` runs, then it reports identical.
- Given a form's `.glb` under `dragon.ride` in a pushed folder, or in an ordinary viewer, when the author looks, then the dragon is textured and recognisable, with its head on +Z.
- Given `pdz-extract` run twice, when the output is compared, then it is identical, and the existing PNGs are unchanged from the baseline.
- Given the staged files and `unzip -l game/build/PanzaDragoonVR.apk`, when listed, then neither holds disc data.

## Implementation Notes

- `Extraction.Version` went to 0.3: the release-mode rule skips extraction only for the current generator, so a folder from 0.2 would otherwise never gain the dragons. The generator is inside every `.glb`, so `tests/Decoding.Tests/expected/` was recorded again; only the version changed.
- `tools/compare-headset.sh` pulled `images/` only, so it now pulls and sets aside `models/` too.
- A vertex's translation column is whole units, not 16.16 as the format notes' matrix line could be read: the three-by-three is 16.16 and the translation adds to the vertex directly, as `draw2.py` does.
- Colour mode 5 is decoded as well as mode 1, since the matrix's rule is "not decodable from its `.CGB`" and mode 5 is.
- All-zero triples are not counted as roots in the node scan: a matrix's tail can read as one, and it has nothing to walk.
- The dragons add no progress stage, as the planes add none; the time is small (the whole of extraction is about 1.2 s on the headset).

Open at commit: the author's look at the forms in a viewer or under `dragon.ride`.

## Plan Change Log

## Review Triage Log

Quick lens, one pass: 1 medium, 1 low patched; 1 low rejected; none deferred.

- medium, patch: `tools/compare-headset.sh` reported a false difference when only an older `models/` remained on the headset. Empty folders are now removed after setting files aside, and the comment corrected.
- low, patch: `Extraction.cs` comments said a manifest needs a PNG. Reworded.
- low, rejected: in `Dragons.BodyRoot`, an out-of-range pointer in a real node stops the backward scan, so the notice may blame `1ST_READ` instead. Still no dragons and one line, only on a corrupted disc; telling a bad pointer from the end of the nodes would add complexity.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
- `tools/validate-gltf.sh <the ten .glb files, from each disc>` -- expected: 0 errors
- `tools/build-quest.sh`, then `tools/compare-headset.sh` -- expected: identical

**Manual checks (if no CLI):**
- The author opens the forms in a viewer and recognises them.
