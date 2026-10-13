---
title: 'Decide the standard model format'
type: 'chore'
ticket: '6'
created: '2026-10-13'
status: 'built'
baseline_revision: 'dc00a183e12604fcb643a0fcb49d14e1f1a0a3d0'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/zwei-disc-formats.md'
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The asset contract reserves a `model` kind but does not say what a model file is. The extraction tool cannot write models and the game cannot load them until that is fixed, which blocks story 1.7 and the dragon and enemy epics.

**Approach:** Research what the game can load at runtime on the Quest 3 and what the Saturn models need a format to carry, then add to the asset contract the model format, how a model refers to its textures, the conventions the tool must follow, and the asset ID the flying grey cube stands in for. glTF is the standing proposal. The author decides each open point and agrees the result.

**Decisions (author, 2026-10-13):** all eleven recommended options accepted.
- Settled: binary glTF (`.glb`), one self-contained file per model; textures embedded in it; a model with an undecodable embedded texture is rejected whole and its placeholder shown; the flying grey cube stands in for `e03.flyer`.
- Provisional, to confirm once real models exist: one texture atlas per model; nearest filtering without mipmaps; metres with one fixed divisor for every model; each quad as two triangles with its corner order kept; double-sided materials; Gouraud shading left out with `COLOR_0` reserved; half-transparent and mesh-pattern polygons at 50% blend with distinct material names.
- The extraction tool may also write the textures as loose PNG files for looking at; the game does not read them.

</frozen-after-approval>

## Implementation Notes

One-shot route: the change is one section of a document, with no code. At the author's request the research and drafting are done by a subagent; the author's decisions are taken in the main conversation.

Known from earlier work, for the researcher:
- Game: Godot 4.7.2, C#, Compatibility renderer, standalone Quest 3. Models are loaded at runtime from loose files in the app's external files folder, never imported in the editor.
- Saturn models: quads, not triangles. Each polygon carries its own small texture (16 colours through a lookup table, or 256 colours through a shared palette), so one model uses dozens of tiny textures. Some polygons may be untextured flat colour, some are drawn half-transparent or as a mesh pattern. Per-vertex shading tables (Gouraud) appear to exist per episode. Geometry is not decoded yet, so the format must not assume more than this.
- The dragon has motion data (`.MTB`) that is unexamined. Animation is not needed for the first demo but the format must not rule it out.
- Spec constraints: original assets are used as extracted, with no upscaling or redrawing; all Saturn knowledge stays in the PC tool; the game reads only standard formats.
- The flying grey cube is the built-in placeholder for a model asset. The contract's ID prefixes so far are `e03.` (Episode 3, including its enemies) and `dragon.`.

Research results (2026-10-13), drafted into the contract's Models section with a `**DECISION NEEDED:**` line at each open point. Nothing here has been run on the headset.

- Godot 4.7 documents runtime glTF loading in exported projects: `GLTFDocument.append_from_file` or `append_from_buffer` into a `GLTFState`, then `generate_scene`. `.gltf` and `.glb` both load. FBX loads too (`FBXDocument`); the page does not mention OBJ.
- Read in the 4.7.2-stable source (`modules/gltf/gltf_document.cpp`, `structures/gltf_texture_sampler.h`, `gltf_state.h`):
  - External image and buffer URIs are joined to the base path and read with `FileAccess::get_file_as_bytes`. An external image with an empty base path fails with `ERR_INVALID_PARAMETER`. A missing external buffer fails the load; a missing or undecodable image only warns and leaves a null texture, so the load still succeeds and the game must check for it.
  - `handle_binary_image_mode` defaults to extract, which is editor-only; at runtime it falls through to an uncompressed `ImageTexture` with no mipmaps. Images are decoded as PNG or JPEG only.
  - The material's texture filter comes from the sampler's `minFilter` alone: `9728` gives nearest. Repeat is on only when both wraps are repeat. `set_wrap_t` assigns to `wrap_s` (a bug), harmless when both are clamp.
  - `alphaMode` `BLEND` becomes alpha with depth pre-pass, `MASK` becomes alpha scissor, `doubleSided` disables culling, `KHR_materials_unlit` becomes unshaded.
  - `COLOR_0` turns on vertex colour as albedo automatically, a multiply.
  - Triangles, strips, points and lines load; triangle fans do not. A surface is named after its material. Mesh, node and material `extras` become metadata.
  - Animations: translation, rotation, scale and weights, with step, linear and cubic spline; skins are read.
- glTF 2.0: right-handed, +Y up, front +Z, metres; no quad mode; UV (0, 0) is the top-left of the image; `COLOR_0` is clamped to 0..1; application attributes start with an underscore; with no sampler, repeat and automatic filtering apply.
- Meta's table for Quest 3: under 200 draw calls and under 1.5 million triangles a frame. Godot draws one call per surface, so one material per Saturn texture would not fit; hence the atlas.
- From memory of the VDP1 documentation, not re-read: Gouraud shading is an addition to the texture colour, not a multiply; the mesh pattern is in screen pixels; VDP1 does not filter textures or cull back faces.

Decisions made by the author on 2026-10-13. All eleven recommended options were accepted and the contract's Models section was finalised to match, with the option lists removed.

Settled:
- Format: binary glTF 2.0 (`.glb`).
- Textures: embedded in the `.glb`. The tool may also write loose PNGs for looking at; the game does not read them.
- A model with an undecodable embedded texture is rejected whole and its placeholder is shown.
- The cube's asset ID is `e03.flyer`.

Accepted as provisional, to be confirmed once real models exist, and marked *Provisional* in the contract:
- One texture atlas per model.
- Nearest filtering, no mipmaps.
- Metres, with one fixed divisor for every model; the number is not chosen yet.
- A quad is two triangles with a fixed diagonal, with its four corners kept in order.
- Double-sided materials.
- Per-vertex (Gouraud) shading left out; `COLOR_0` reserved.
- Half-transparent and mesh polygons at 50 % blend, under separate material names `half` and `mesh`.

The contract states that none of the Models section has been run in Godot or on the headset, and that story 1.7 is the first test.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens), 2026-10-13: high 0, medium 2, low 7, false 0, maybe-false 0. All patched; none rejected.

- medium, patched: the contract promised `COLOR_0` could be added later without raising `format`, but Godot applies it as a multiply and Saturn Gouraud adds. Format 1 now says the game ignores `COLOR_0`.
- medium, patched: one row rejected any non-PNG image while another showed any valid `.glb`; an embedded JPEG got both answers. Rejection is now only for an image the game cannot decode.
- low, patched: the sentence on hand-made models understated the table's rules. Reworded to match.
- low, patched: Versioning defined `format` by the manifest alone. It now covers model file contents too.
- low, patched: the counter-clockwise winding claim could not be guaranteed alongside the corner-order rule. Removed; corner order governs. Provisional.
- low, patched: `opaque` and `cutout` were decided per texture, but each Saturn polygon carries its own mode. Now per polygon and provisional.
- low, patched: the atlas border, flat-colour block and three-corner rule rested on undecoded geometry without a provisional marker. Marked.
- low, patched: the spec's open question still asked which model format the extractor writes. Narrowed to the geometry.
- low, patched: `stack.md` told the reader to generate mipmaps for all 3D textures and had no entry for runtime model loading. Updated.

## Verification

**Manual checks (if no CLI):**
- The contract's `model` kind names the format and how textures are referenced from it, the cube's ID is listed, the worked example manifest still parses as JSON, and the author has agreed both.
