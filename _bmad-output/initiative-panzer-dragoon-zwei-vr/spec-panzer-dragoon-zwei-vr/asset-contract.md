# Asset contract

The extraction tool and the game share nothing except a folder of files and the manifest inside it. This document is that contract. The tool writes to it; the game reads from it. Neither may assume anything the contract does not state.

## Placeholders and replacement

The game always runs, with or without the folder.

- For every asset ID it uses, the game has a built-in placeholder of the author's own making: plain images, grey cubes. Placeholders contain nothing from the disc.
- When the folder supplies a valid asset for an ID, the game uses it in place of the placeholder.
- When the folder is empty or absent, the game runs entirely on placeholders and says so.

So a player who has not run the extraction tool still sees a scene, and running the tool replaces the stand-ins with the real thing, one ID at a time.

## The folder

On the headset, the asset folder is the app's own external files folder:

```
/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/
```

SideQuest and `adb push` can both copy into it. On the PC, the tool writes an output folder of the user's choosing with exactly the layout below. The user copies what is inside that folder, not the folder itself, into `files/`, so that `manifest.json` ends up directly inside `files/`.

```
files/                 the asset folder (the output folder's contents on the PC)
  manifest.json        required whenever the folder holds assets; at the top level
  images/              suggested, not required
  models/              suggested, not required
```

- There is one `manifest.json`, at the top level.
- Everything else is the tool's choice. The game finds files only through the manifest and must not assume any file name or subfolder.
- Files in the folder that the manifest does not list are ignored.

## The manifest

`manifest.json` is one JSON object, UTF-8 without a byte-order mark.

```json
{
  "format": 1,
  "generator": "pdz-extract 0.1",
  "source": { "product": "GS-9049", "version": "V1.001" },
  "assets": [
    {
      "id": "e03.floor",
      "kind": "image",
      "path": "images/e03_floor.png",
      "bytes": 181234,
      "sha256": "3a7bd3e2360a3d29eea436fcfb7e44c735d117c42d1c1835420b6b9942dd4f1b"
    },
    {
      "id": "e03.canopy",
      "kind": "image",
      "path": "images/e03_canopy.png",
      "bytes": 204817
    },
    {
      "id": "e03.flyer",
      "kind": "model",
      "path": "models/e03_flyer.glb",
      "bytes": 48392
    }
  ]
}
```

### Top-level fields

| Field | Type | Required | Meaning |
|-------|------|----------|---------|
| `format` | integer | yes | Version of this contract. This document describes `1`. |
| `generator` | string | yes | What wrote the manifest: a tool's name and version, or `hand-written`. For people; the game does not act on it. |
| `source` | object or null | yes | Which disc the assets came from, as `{"product": "GS-9049", "version": "V1.001"}`. `null` when they are not from a disc. For people; the game does not act on it. |
| `assets` | array | yes | One entry per file. May be empty. |

### Asset entries

| Field | Type | Required | Meaning |
|-------|------|----------|---------|
| `id` | string | yes | The name the game asks for. Unique within the manifest. |
| `kind` | string | yes | What the file is. See Kinds. |
| `path` | string | yes | Where the file is, relative to the asset folder. |
| `bytes` | integer | yes | The file's exact size in bytes. |
| `sha256` | string | no | SHA-256 of the file, 64 lowercase hex digits. For checking a folder on the PC; the game does not read it. |
| `meta` | object | no | Extra data specific to the kind. None is defined in format 1. |

### IDs

- One or more levels of lowercase letters, digits and underscores, separated by single dots: `[a-z0-9_]+(\.[a-z0-9_]+)*`. No empty levels, so no leading, trailing or doubled dots.
- Levels run broadest first. The first level says where the asset belongs:

| Prefix | Used for |
|--------|----------|
| `e03.` | Episode 3, such as `e03.floor` and `e03.canopy`, and its enemies, such as `e03.flyer`. |
| `dragon.` | The dragon. |

- The table lists the prefixes in use. A new one is added here when it is first needed.
- The engine spike's ground and sky are the placeholders for `e03.floor` and `e03.canopy`. There are no separate test IDs: the author proves external loading by supplying their own images under the real IDs.
- The engine spike's flying grey cube is the placeholder for `e03.flyer`, a model: the Episode 3 flying enemy that the grey cubes later become. As with the ground and sky, there is no separate test ID.
- An ID is a promise. Once the game asks for an ID, the tool keeps writing it. The file behind it may move or be renamed freely.

### Paths

- Relative to the asset folder, with forward slashes.
- No leading slash, no `.` or `..` parts, no backslashes.
- Lowercase ASCII letters, digits, underscores, hyphens and dots only, so the same path works on every file system.

### Kinds

| Kind | File format |
|------|-------------|
| `image` | PNG, 8 bits per channel, RGB or RGBA. |
| `model` | Binary glTF 2.0 (`.glb`), one self-contained file with its textures inside it. See Models. |

## Models

A `model` asset is one glTF 2.0 file in the binary form, `.glb`. glTF is a published Khronos standard, opens in ordinary viewers and in Blender, and Godot loads it at runtime in an exported build with `GLTFDocument` and `GLTFState`, with no editor import.

None of this section has yet been run in Godot or on the headset. Story 1.7 is where it is first tested.

Some rules below are marked *Provisional*. They depend on model geometry that is not decoded yet and on Saturn drawing behaviour that has not been re-checked. The tool follows a provisional rule now, and it is to be confirmed once real models exist. Changing one later is expected, and does not by itself raise `format` unless a game built for the old rule would misread the new files.

### One file, textures inside

- The file is self-contained. Its one buffer is the GLB binary chunk, and every image the tool writes is a PNG stored in that buffer. PNG rules are those of the `image` kind: 8 bits per channel, RGB or RGBA.
- No `uri` appears anywhere in the file: no external buffers, no external images, no `data:` URIs.
- So one model is one manifest entry, and the `bytes` check covers its geometry and its textures together.
- A model never refers to an `image` asset. An image the game asks for by ID and an image inside a model are separate things, even when the tool made both from the same disc data.

### Textures

- All of a model's textures are packed into one image, an atlas, of at most 2048 x 2048 pixels. A model too large for one atlas uses more, and gains one set of materials per atlas. *Provisional.*
- Pixels are copied into the atlas unchanged: no scaling, no filtering, no redrawing. Colours are already resolved, so the lookup table or palette is applied by the tool and the PNG is plain RGBA.
- Each texture sits in the atlas with a 2-pixel border that repeats its own edge pixels. UV coordinates address only the original rectangle, never the border. *Provisional.*
- A texture drawn flipped on the Saturn is stored unflipped; the flip is in the UV coordinates.
- UV coordinates are `TEXCOORD_0`, with (0, 0) at the top-left corner of the atlas, as glTF defines.
- An untextured, flat-coloured polygon is given a small block of its colour in the atlas and is otherwise an ordinary textured polygon. *Provisional.*
- Every texture uses one sampler: `magFilter` and `minFilter` both `9728` (nearest), `wrapS` and `wrapT` both `33071` (clamp to edge). No mipmaps are stored. *Provisional.*
- The tool may also write the same textures as loose PNG files for looking at. The game does not read them and they need no manifest entry.

### Geometry

- Axes are glTF's: right-handed, +Y up, the model's front facing +Z. The tool converts from the Saturn's axes; the game never sees them. Godot loads glTF axes as they are.
- Positions are in metres, as glTF requires. The tool divides Saturn coordinates by one fixed number, the same for every model, so models keep their sizes relative to each other. That number is recorded here once geometry is decoded. *Provisional.*
- Triangles only (primitive mode 4), indexed. glTF has no quads.
- Each Saturn quad is written as four vertices of its own, in the Saturn's corner order, followed by the two triangles (0, 1, 2) and (0, 2, 3) of those four. Vertices are not shared between quads, because each quad has its own texture. So within a primitive, quad `k` is vertices `4k` to `4k+3`, and a reader that wants the quads back can have them. *Provisional.*
- A Saturn polygon with only three distinct corners is written the same way, with the repeated corner repeated. *Provisional.*
- Every material sets `doubleSided` to `true`, because it is not yet known which way the Saturn's polygons face. The corner-order rule above governs: the tool does not reorder corners to make front faces wind counter-clockwise, so winding is not guaranteed while materials are double-sided. *Provisional.*
- No normals. Every material uses the `KHR_materials_unlit` extension, listed in `extensionsUsed` and not in `extensionsRequired`: the Saturn's shading is in its textures and its colour tables, not in lights.
- No vertex colours. `COLOR_0` is reserved for the Saturn's per-vertex shading, which is not decoded yet. In format 1 the game ignores `COLOR_0` if a file has it, as it ignores animations, so adding it later does not change `format`. *Provisional.*
- The file passes the Khronos glTF Validator with no errors.

### Transparency

A model has at most four materials per atlas. The material's name says how its polygons are drawn, and the game may rely on the names.

| Material name | Used for | glTF settings |
|---------------|----------|---------------|
| `opaque` | Polygons the Saturn draws with no transparent pixels, as the tool decodes each polygon's own drawing mode. | `alphaMode` `OPAQUE`. *Provisional.* |
| `cutout` | Polygons the Saturn draws with some pixels fully transparent, decided the same way. | `alphaMode` `MASK`, `alphaCutoff` `0.5`. *Provisional.* |
| `half` | Polygons the Saturn draws half-transparent. | `alphaMode` `BLEND`, `baseColorFactor` `[1, 1, 1, 0.5]`. *Provisional.* |
| `mesh` | Polygons the Saturn draws as a mesh pattern, every other pixel. | As `half`. The separate name lets the game draw the pattern itself later. *Provisional.* |

- With a second atlas the names are `opaque_2`, `cutout_2` and so on.
- A pixel the Saturn would not draw for that polygon has alpha 0 in the atlas. Every other pixel has alpha 255. Half-transparency is in the material, not in the pixels.
- A material no polygon uses is left out.

### Structure and naming

- One scene, with one root node.
- A model with parts that move separately has one node per part under the root, each with its own mesh and with its origin at the point the part turns about. A model with no such parts is the root node and one mesh.
- Each mesh has one primitive per material.
- Node and mesh names are lowercase letters, digits and underscores, and unique within the file. The root node is named after the last level of the asset ID: `flyer` for `e03.flyer`.
- Material names are those in the Transparency table.
- No animations, skins, cameras or lights in format 1. glTF can carry animation and the game's loader can read it, so the dragon's motion can be added later, in the model or as a kind of its own, without changing `format`. The game ignores any it finds until then.
- No extensions other than `KHR_materials_unlit`, and nothing in `extensionsRequired`.

### When a model is invalid

These add to the table under What the game does at launch. A model is used whole or not at all: the game never shows half a model.

| Situation | What the player is told | What is shown |
|-----------|-------------------------|---------------|
| The file is not a readable `.glb` of glTF version 2.0 | Unreadable model, with its ID and path. | The placeholder for that ID. |
| The file refers to anything outside itself: an external buffer, an external image or a `data:` URI | Not self-contained, with its ID and path. The game never opens a file a model names. | The placeholder for that ID. |
| The file lists an extension in `extensionsRequired` | Unsupported model, with its ID and the extension's name. | The placeholder for that ID. |
| An image inside the file cannot be decoded by the game, whatever its format | Unreadable texture in model, with its ID. | The placeholder for that ID. |
| The file loads but contains no mesh | Empty model, with its ID. | The placeholder for that ID. |
| The file is valid glTF but breaks a convention above, such as lit materials, other material names, linear filtering or an image that is not PNG but that the game can decode | Nothing. | The model, as the file describes it. |

The last row means the game does not police the conventions. They are the tool's obligations; the author's own hand-made models, as in story 1.7, need only pass the rows above it: a readable, self-contained `.glb` with at least one mesh, no required extensions and images the game can decode.

## What the game does at launch

The game checks the folder before showing the scene. It never refuses to run: an asset with a problem is reported and its placeholder is used. The report is readable in the headset and lists every problem, not only the first.

| Situation | What the player is told | What is shown |
|-----------|-------------------------|---------------|
| No `manifest.json` | A short notice: running on placeholders, with the folder path. Not an error. | Placeholders for everything. |
| `manifest.json` is not valid JSON, or a required top-level field is absent or of the wrong type | The manifest cannot be read, with the reason. | Placeholders for everything. |
| `format` is not one this build of the game supports | The assets were made by a newer or older tool than this build understands; run the matching tool again. | Placeholders for everything. |
| Two entries share an `id` | Duplicate ID, with the ID. | The placeholder for that ID. |
| A `path` breaks the path rules, or `bytes` is negative | Invalid entry, with its ID. The game never opens a path that leaves the asset folder. | The placeholder for that ID. |
| A listed file is absent, or the path is not a file | Missing, with its ID and path. | The placeholder for that ID. |
| A listed file's size differs from `bytes` | Incomplete or changed, with its ID and path. | The placeholder for that ID. |
| The game needs an ID whose entry has a kind it does not know or does not expect | Wrong kind, with the ID and the kind found. | The placeholder for that ID. |
| The game needs an ID the manifest does not list | Nothing. This is the normal state before the tool supports that asset. | The placeholder for that ID. |
| The manifest lists an ID the game does not use | Nothing. | Nothing. |

Checking size catches a copy that was cut short, without hashing every file on every launch.

## Versioning

- `format` rises only when a game built for the old number could misread the new manifest, or the files it lists: for example a change to what a model file contains that an older game would draw wrongly.
- Adding an optional field, a new kind or new IDs does not change `format`. The game ignores fields it does not know.
- Each build of the game supports a stated set of `format` numbers, at first only `1`, and reports any other.

## Not in the contract

- **Tuning values.** Floor depth, canopy height and scroll speed belong to the game and are set in the headset.
- **Copyrighted files in the repository or the app.** Nothing the tool writes is committed or packed into the APK. The built-in placeholders are the author's own work.
- **How the game reaches the folder from Godot.** Story 1.4 settles that; the contract fixes only the location.
