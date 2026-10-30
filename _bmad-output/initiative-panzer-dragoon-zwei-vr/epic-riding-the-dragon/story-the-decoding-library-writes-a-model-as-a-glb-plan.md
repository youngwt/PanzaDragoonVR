---
title: 'The decoding library writes a model as a .glb'
type: 'feature'
ticket: 3
created: '2026-10-29'
status: done
baseline_revision: 'b1f1fe3032ddc40a9d71c21a92ed051bb819b63a'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
  - '{project-root}/AGENTS.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The decoding library can write images but not models, so nothing the dragon's or the enemies' geometry decoder produces later could reach the game as the asset contract's `model` kind.

**Approach:** Add to `lib/PdzDecoding/` a model type (parts, each a list of quads with a texture or flat colour, a drawing mode and flip flags) and a writer that turns it into one self-contained `.glb` exactly as the contract's Models section says, deterministically; record small synthetic models' output as test files, and add a tool that runs the Khronos glTF Validator on them.

## Boundaries & Constraints

**Always:** Follow the Models section as written, provisional rules included: atlas ≤ 2048 x 2048 with a 2-pixel edge-repeat border, more atlases (`opaque_2` …) when one is full; pixels copied unchanged; flips in UVs only; flat colour as a small atlas block; one nearest/clamp sampler, no mipmaps; quad `k` = vertices `4k..4k+3`, triangles (0,1,2),(0,2,3), Saturn corner order kept; `doubleSided` true; no normals, no `COLOR_0`; `KHR_materials_unlit` in `extensionsUsed` only; materials `opaque`/`cutout`/`half`/`mesh` with the table's settings, unused ones left out; one scene, one root node named by the caller, one node per part under it when there is more than one part, one primitive per material; no `uri` anywhere. Same bytes on every runtime: PNGs from `PngWriter`, floats formatted invariantly, no .NET compressors, no hash-ordered collections in output order. Errors in caller-supplied models are `ArgumentException`; nothing here reads the disc.

**Never:** Decode `.MDB` geometry, choose the Saturn-to-metres divisor or axis conversion (the later geometry decoder does), add the model to extraction or the manifest, change the asset contract, `GlbCheck` or texture numbering, or commit the validator's binary.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| One part | Two textured quads sharing one texture, one flat-coloured quad | Root node holds the one mesh; atlas holds the texture once and one colour block | None |
| Parts | Three parts with origins | Root node with three named child nodes, each with its translation and mesh | None |
| All four modes | Quads of each drawing mode | Primitives in order opaque, cutout, half, mesh, each with its material | None |
| Flipped | Quad flipped horizontally and/or vertically | Same atlas pixels as unflipped; UV corners swapped | None |
| Triangle | Quad with a repeated corner | Written as four vertices like any quad | None |
| Too many textures | Textures that overflow 2048 x 2048 | Second atlas, image and texture; materials `opaque_2` etc. | None |
| Bad model | No parts, a part with no quads, bad/duplicate names, a texture wider or taller than 2044, wrong pixel count | Nothing written | `ArgumentException` naming the problem |

</frozen-after-approval>

## Code Map

- `lib/PdzDecoding/PngWriter.cs` -- `PngWriter.Encode(width, height, rgba)`: reuse for atlas PNGs, unchanged.
- `lib/PdzDecoding/Manifest.cs` -- hand-written deterministic JSON with `StringBuilder` and `CultureInfo.InvariantCulture`; follow that style (its `Quote` is `internal`, reusable).
- `lib/PdzDecoding/Textures.cs` -- decoded textures are row-major RGBA byte arrays, alpha 0 or 255; the model's texture type takes exactly that.
- `lib/PdzDecoding/PdzDecoding.csproj` -- net9.0/net10.0, `ImplicitUsings` disabled: write `using`s explicitly. Do not change.
- `game/Logic/GlbCheck.cs` -- the game's pre-engine check; read only. `tests/GameLogic.Tests/GlbCheckTests.cs` -- add the "accepts a written model" case here (the project already references the library).
- `tests/Decoding.Tests/` -- xUnit, `Subject_Behaviour` names, `expected/**` copied to output (`Decoding.Tests.csproj`); `ExtractionTests` compares against `expected/fixed-disc-manifest.json` the same way.
- `tools/toolchain-env.sh` -- `TOOLCHAIN_ROOT` (default `~/.local/opt`); the validator installs to `$TOOLCHAIN_ROOT/gltf-validator-2.0.0-dev.3.10/`. `tools/setup-toolchain.sh` -- `die`/`say`/`download`/`verify` pattern to copy.
- Validator: Khronos `gltf_validator` 2.0.0-dev.3.10, `https://github.com/KhronosGroup/glTF-Validator/releases/download/2.0.0-dev.3.10/gltf_validator-2.0.0-dev.3.10-linux64.tar.xz`, SHA-256 `168eba887964125abe17ae97899b38d0b3cfd73c266c78424c194929ddcbc522`; `-o` prints the JSON report to stdout (without it a report file is written beside the input), exit non-zero on any error.
- `tools/README.md` -- Unit tests section and "Your own model in the headset"; add the validator tool there.

## Tasks & Acceptance

**Execution:**
- [x] `lib/PdzDecoding/Model.cs` -- `Model(Name, Parts)`, `ModelPart(Name, Origin, Quads)`, `ModelQuad(A, B, C, D, Texture?, Colour, Mode, FlipH, FlipV)`, `ModelTexture(Width, Height, Rgba)`, `DrawingMode { Opaque, Cutout, Half, Mesh }`; positions `System.Numerics.Vector3` already in glTF metres and axes -- the type the geometry decoder will fill.
- [x] `lib/PdzDecoding/Atlas.cs` -- deterministic shelf packing of distinct textures and colour blocks (first-use order) into atlases, each the smallest power-of-two square from 16 to 2048 that holds its set, with the edge-repeat border; returns pixels and each entry's rectangle.
- [x] `lib/PdzDecoding/GlbWriter.cs` -- `GlbWriter.Write(Model) : byte[]`: validates the model, builds atlases, buffers and JSON, pads chunks (JSON with spaces, BIN with zeros) per the GLB spec.
- [x] `tests/Decoding.Tests/GlbWriterTests.cs` -- one test or more per matrix row, plus atlas border/pixels, UVs, sampler, materials, no `uri`, accessor min/max, determinism (write twice), and recorded output: each sample model's bytes equal `expected/models/<name>.glb`; with `PDZ_RECORD_EXPECTED=1` the test rewrites those files in the source tree instead.
- [x] `tests/Decoding.Tests/expected/models/*.glb` -- recorded samples: one part, parts with all four modes and flips, two atlases.
- [x] `tests/GameLogic.Tests/GlbCheckTests.cs` -- `WrittenModel_IsUsable`: a `GlbWriter` model passes `GlbCheck.Check` with its image count.
- [x] `tools/validate-gltf.sh` -- runs the validator on the given `.glb` files, or on the recorded samples with none; downloads and checks the pinned validator when it is missing; one line per file with counts; `die`/`say`, exit 1 on any error.
- [x] `tools/README.md` -- document the validator tool and the new tests.

**Acceptance Criteria:**
- Given the recorded sample models, when `tools/validate-gltf.sh` runs, then the validator reports 0 errors on each and the script exits 0.
- Given a file with a validator error, when the script runs on it, then it prints `validate-gltf: error: <file>: ...` on stderr and exits 1.
- Given the same model, when it is written twice or on another runtime, then the bytes are identical and equal the recorded file.
- Given `tools/test.sh`, when it runs, then every suite passes.

## Implementation Notes

- Quad corners are relative to their part's origin. A part's origin is its node's `translation`, written only when it is not zero; with one part the root node carries it. With one part the mesh is named after the root; with several, each mesh after its part, and part names must differ from each other and from the root's.
- `ModelColour` is RGB; a colour block is drawn with alpha 255. The flips of an untextured quad change nothing that shows.
- `ArgumentException` messages are one line with no full stop, as `DecodingException`'s are. Non-finite positions and undefined drawing modes are rejected too.
- `two_atlases.glb` is about 16 MB: any second atlas means a full 2048 x 2048 one, and PNGs are stored uncompressed. Too large to commit, so only its SHA-256 is recorded (`expected/models/two_atlases.glb.sha256`); samples over 1 MiB are recorded that way.
- `validate-gltf.sh` with no files runs the recorded-sample test with `PDZ_GLB_SAMPLES` set to a temporary folder, which makes it write the three samples there, and validates those: all three get 0 errors, 0 warnings, 0 infos, 0 hints.
- `asset.generator` is `Manifest.Generator`, so a library version bump changes the recorded samples: record them again with `PDZ_RECORD_EXPECTED=1`.
- Checked on .NET 9 as well as 10: the writer's tests, built for net9.0 in a scratch project, match the recorded files.
- `validate-gltf.sh` treats a validator folder with no `.installed` marker as missing, so the first run downloads the pinned release again.

## Plan Change Log

## Review Triage Log

### Pass 1 (quick) — high 0, medium 0, low 1, false 0, maybe-false 1

| Finding | Verdict | Route | Evidence / action |
|---------|---------|-------|-------------------|
| `GlbWriter.cs`: a node and its mesh share a name (root/mesh `one_part`; part node/mesh `body`), while the contract says node and mesh names are "unique within the file" | maybe-false (medium if true) | defer | Names are unique within nodes and within meshes; whether the contract means across both kinds is the author's reading to give. Raised as an open question. |
| `tools/README.md`: the validator section lists `curl`, `tar`, `xz`, `jq` as download needs; the script also needs `sha256sum` and `find`, and needs `jq` on every run | low | patch | Applied directly (one sentence of documentation): names `jq` as needed every run and the five download tools. |

## Design Notes

The model is in glTF terms (metres, +Y up, front +Z) because the axis conversion and the divisor are not known until geometry is decoded; the decoder that produces a `Model` owns them, and the writer stays format-only. Textures are de-duplicated by content (size and pixels); colour blocks by colour (1 x 1 pixel, so 5 x 5 with the border). UVs: corner A gets the rectangle's top-left, B top-right, C bottom-right, D bottom-left, divided by the atlas size as `float`; a horizontal flip swaps left and right, a vertical one top and bottom. Accessor `min`/`max` are written as the exact `double` value of each `float` (`"R"`, invariant), so a reader comparing in either precision agrees. Indices are `UNSIGNED_SHORT` when a primitive has at most 65535 vertices, else `UNSIGNED_INT`. Materials: `pbrMetallicRoughness` with `baseColorTexture` 0-based on the atlas, `metallicFactor` 0, `extensions.KHR_materials_unlit` `{}`.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all suites pass.
- `tools/validate-gltf.sh` -- expected: each recorded sample reported with 0 errors, exit 0.
- `git status --short` -- expected: no validator binary or report files staged.
