# Asset contract

Extraction and the game's loader share nothing except a folder of files and the manifest inside it. This document is that contract. Extraction writes to it, whether the game runs it on the headset or the PC command runs it; the loader reads from it. "The tool" below means either. Neither may assume anything the contract does not state.

## Placeholders and replacement

The game always runs, with or without the folder.

- For every asset ID it uses, the game has a built-in placeholder of the author's own making: plain images, grey cubes. Placeholders contain nothing from the disc.
- When the folder supplies a valid asset for an ID, the game uses it in place of the placeholder.
- When the folder is empty or absent, the game runs entirely on placeholders and says so.

So a player who has not supplied a disc image still sees a scene, and extraction replaces the stand-ins with the real thing, one ID at a time.

## The folder

On the headset, the asset folder is the app's own external files folder:

```
/sdcard/Android/data/io.github.youngwt.panzadragoonvr/files/
```

The game fills it itself: at launch, as Extraction says, it extracts the assets from the player's disc image and writes them and the manifest here. The player's own files can be copied in with SideQuest or `adb push` instead. On the PC, the same decoding library writes an output folder of the user's choosing with exactly the layout below. The user copies what is inside that folder, not the folder itself, into `files/`, so that `manifest.json` ends up directly inside `files/`.

```
files/                 the asset folder (the output folder's contents on the PC)
  manifest.json        required whenever the folder holds assets; at the top level
  images/              suggested, not required
  models/              suggested, not required
  disc/                the player's disc image, read by extraction on the headset; see Extraction
  development          an empty marker file: development mode; see Extraction
  tuning.json          optional: the scene's tuning; see Tuning
```

- There is one `manifest.json`, at the top level.
- Everything else is the tool's choice. The game finds assets only through the manifest and must not assume any file name or subfolder for them. The only fixed names besides `manifest.json` are `disc/` and `development`, which Extraction defines, and `tuning.json`, which Tuning defines. The manifest does not list `tuning.json`.
- Files in the folder that the manifest does not list are ignored.

## The manifest

`manifest.json` is one JSON object, UTF-8 without a byte-order mark.

```json
{
  "format": 1,
  "generator": "pdz-extract 0.2",
  "source": { "product": "GS-9049", "version": "V1.001" },
  "assets": [
    {
      "id": "e03.floor",
      "kind": "image",
      "path": "images/e03/floor.png",
      "bytes": 181234,
      "sha256": "3a7bd3e2360a3d29eea436fcfb7e44c735d117c42d1c1835420b6b9942dd4f1b"
    },
    {
      "id": "e03.canopy",
      "kind": "image",
      "path": "images/e03/canopy.png",
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
| `generator` | string | yes | What wrote the manifest: a tool's name and version, or `hand-written`. The game acts on it only to decide whether to extract again in release mode (see Extraction). |
| `source` | object or null | yes | Which disc the assets came from, as `{"product": "GS-9049", "version": "V1.001"}`. `null` when they are not from a disc. The game acts on it only to decide whether to extract again in release mode (see Extraction). |
| `assets` | array | yes | One entry per file, at least one. An empty list is a problem: see What the game does at launch. |

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

#### Extracted textures

Every texture the extractor decodes from the disc's Episode 3 files is listed, as an `image`, under an ID of the form

```
e03.tex.<file>.<n>
```

- `<file>` is the disc file's name without its extension, in lowercase: `e03cmn`, `e03b0n0` to `e03b0n2`, `e03b1n0` to `e03b1n2`, `e03boss`, `e03end`.
- `<n>` is the texture's place in that file, as four decimal digits from `0000`. Textures are numbered in the order their first drawing record appears in the file's `.MDB`. A record whose four words (CMDPMOD, CMDCOLR, CMDSRCA and CMDSIZE) all equal those of an earlier record in the same file is the same texture and takes no new number; a record differing in any of them is a new texture, even when its image turns out the same.
- The number depends on how the extractor finds and de-duplicates records. A change to either renumbers the textures, which breaks the promise for every ID the game already asks for. Such a change is made only with the author's agreement, and with the game updated to match.
- The file behind the ID is at `images/e03/<file>/<n>.png`, for example `images/e03/e03b0n0/0007.png`.

#### Extracted floor and canopy

The extractor also writes Episode 3's floor and canopy, the two planes of VDP2's RBG0 layer that `EPISODE3.PRG` holds packed, each as one whole 512x512 page, as `image` assets under the IDs the game already asks for:

| ID | File |
|----|------|
| `e03.floor` | `images/e03/floor.png` |
| `e03.canopy` | `images/e03/canopy.png` |

- They are an addition to the textures: a disc whose planes cannot be found or unpacked still has its textures extracted, and the extractor gives a one-line notice, `no floor and canopy: <reason>`, in place of the line naming where they came from. The manifest then lacks the two IDs, so the game shows its placeholders for them; an earlier run's `floor.png` and `canopy.png` stay in the folder, unlisted, as for any file extraction did not write.

### Paths

- Relative to the asset folder, with forward slashes.
- No leading slash, no `.` or `..` parts, no backslashes.
- Lowercase ASCII letters, digits, underscores, hyphens and dots only, so the same path works on every file system.

### Kinds

| Kind | File format |
|------|-------------|
| `image` | PNG, 8 bits per channel, RGB or RGBA. |
| `model` | Binary glTF 2.0 (`.glb`), one self-contained file with its textures inside it. See Models. |

## Extraction

The game extracts the assets on the headset at launch, and the PC command does the same on the PC. Both use the one decoding library, so given the same disc image they write the same files, byte for byte.

### The disc image

- The player copies their own disc image into `disc/` inside the asset folder: one `.cue` sheet with the `.bin` file or files it names, or one `.iso` of 2048-byte sectors. Any file names.
- A `.cue` sheet that names an `.iso` in `disc/` (its name matched without regard to case) is one disc image with it: the sheet is the image and the ISO is its file, as a `.bin` would be. A sheet that cannot be read names nothing, so an `.iso` beside it is an image of its own.
- More than one disc image in `disc/` is a problem: it is reported and nothing is extracted.
- A disc other than the Japanese GS-9049 V1.001 is attempted, not refused. What it yields is whatever the decoders find.

### Development and release modes

- **Development mode** is on when an empty file named `development` sits at the top of the asset folder. The game then keeps the disc image and extracts again on every launch.
- **Release mode** is the mode without that file, whatever the build. The game extracts when `disc/` holds an image, unless the folder's `manifest.json` was written by the current extractor (its whole `generator` is the game's own, `pdz-extract` and the decoding library's version) from a disc with the same `source` product and version. A newer extractor therefore extracts again when the player copies the image back; nothing is extracted again without the image. After a successful extraction that wrote something (a texture or a plane), and whenever extraction is skipped for that reason, the game deletes the disc image. An extraction that succeeds but writes nothing keeps the image, with a log line saying so, as a failed one does.

### What extraction writes

- Extraction writes into a staging folder, `extracting/` inside the asset folder, which it empties first. Only when every file is written without error does it move the new files into place, the new `manifest.json` last, and then remove the staging folder. A failure before that leaves the previous files and manifest in place; a leftover `extracting/` folder is emptied by the next extraction.
- It writes its own `manifest.json`, replacing any manifest in the folder. Files it did not write are left in place but are no longer listed.
- `generator` is `pdz-extract` followed by the decoding library's version, the same on the PC and the headset. `source` is the product and version from the disc's Saturn header.
- For identical output: entries are sorted by `id` in ordinal order; every entry has `sha256`; the JSON puts every object and array member on a line of its own, indented two spaces per level, with `": "` after each key, `\n` line ends and a final newline, and keys in the order of the example below; PNGs come from one deterministic encoder.

```json
{
  "format": 1,
  "generator": "pdz-extract 0.2",
  "source": {
    "product": "GS-9049",
    "version": "V1.001"
  },
  "assets": [
    {
      "id": "e03.tex.e03b0n0.0000",
      "kind": "image",
      "path": "images/e03/e03b0n0/0000.png",
      "bytes": 1283,
      "sha256": "9f2c5d8a3b1e4f6a7c0d2e9b8a1f3c5e7d9b0a2c4e6f8a1b3c5d7e9f0a2b4c6d"
    },
    {
      "id": "e03.tex.e03b0n0.0001",
      "kind": "image",
      "path": "images/e03/e03b0n0/0001.png",
      "bytes": 977,
      "sha256": "1b3d5f7a9c2e4a6b8d0f1a3c5e7b9d2f4a6c8e0b1d3f5a7c9e2b4d6f8a0c1e3b"
    }
  ]
}
```

### Progress and failure

- While extraction runs, the scene is shown on what the folder already holds, and a board in the world shows progress by disc file. When it finishes, the game reads the folder again.
- A failed extraction is reported on the launch report board with the reason, and the game carries on with what the folder held, or placeholders.

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
| `manifest.json` lists no assets | The manifest cannot be read: it lists no assets. | Placeholders for everything. |
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

Extraction, when it runs, adds these:

| Situation | What the player is told | What is shown |
|-----------|-------------------------|---------------|
| More than one disc image in `disc/` | More than one disc image, with their names. Nothing is extracted. | What the folder already holds. |
| The disc image cannot be read, or is not a Saturn disc | The disc image cannot be read, with the reason. | What the folder already holds. |
| Extraction fails part way | Extraction failed, with the disc file and the reason. The previous contents are kept. | What the folder already holds. |

Tuning adds these:

| Situation | What the player is told | What is shown |
|-----------|-------------------------|---------------|
| No `tuning.json` | Nothing. | The built-in values. |
| `tuning.json` cannot be read, is not valid JSON or is not one JSON object | The tuning file cannot be read, with the reason. | The built-in values. |
| A setting is not a number or is out of its range | The setting's name and its allowed range. | The built-in value for that setting; the others as the file gives them. |
| A field is not a setting, such as a misspelt name | The field's name. | Nothing changes; the field is ignored. |

A tuning problem alone puts the board up, as an asset problem does; with other problems it is listed ahead of them, so the board always names it.

## Tuning

The floor's depth, the canopy's height, how fast they scroll and how much of each one copy of its image covers belong to the game, not to the extracted assets. Each has a built-in value. An optional `tuning.json` at the top of the asset folder replaces any of them, so the author can try a new look by editing the file on the PC and pushing it (`tools/push-tuning.sh`) without a rebuild. The game reads it once, at launch; a change is seen when the app is started again. It is never changed in the headset.

`tuning.json` is one JSON object. Every field is optional and every value a number; the example holds the built-in values:

```json
{
  "floor_depth": 6,
  "canopy_height": 10,
  "flight_speed": 6,
  "canopy_speed_scale": 1,
  "floor_tile": 24,
  "canopy_tile": 48
}
```

| Field | Range | Built-in | Meaning |
|-------|-------|----------|---------|
| `floor_depth` | 3 to 50 | 6 | Metres the ground plane is below the play area's floor. The player rides the dragon at the play area's floor, and the dragon is at least 3 m off the ground. The built-ins were set by the author in the headset on 2026-10-26 to look like Episode 3. |
| `canopy_height` | 4 to 100 | 10 | Metres the canopy plane is above the play area's floor, so it is always above a standing head and the flyer. |
| `flight_speed` | 0 to 30 | 6 | Metres a second the floor's image moves past. |
| `canopy_speed_scale` | 0 to 2 | 1 | How the canopy's image moves, as a multiple of the speed at which it crosses the same angle a second overhead as the floor does below, seen from an eye 1.6 m above the play area's floor. 1 is a flat backdrop at the floor's on-screen speed; 0 holds it still. |
| `floor_tile` | 4 to 500 | 24 | Metres of floor one copy of its image covers. |
| `canopy_tile` | 8 to 500 | 48 | Metres of canopy one copy of its image covers. |

Ranges include their ends, and are narrow enough that each allowed value gives a sane picture at 90 Hz on the game's 2000 m planes with the other settings at their built-ins, as do all settings at their low ends together and all at their high ends together (backlog 17, 2026-10-27). Mixing ends can still break the picture: a high canopy, a shallow floor, the top speed, the top canopy speed scale and a small canopy image move the canopy's image more than a tile a frame. At the top `canopy_height` the canopy's edge shows about 6 degrees above the horizon. A setting that is out of range or not a number keeps its built-in value and is reported; the others are still used. A field that is not a setting is reported and ignored. The game never refuses to run because of the file.

## Versioning

- `format` rises only when a game built for the old number could misread the new manifest, or the files it lists: for example a change to what a model file contains that an older game would draw wrongly.
- Adding an optional field, a new kind or new IDs does not change `format`. The game ignores fields it does not know.
- Each build of the game supports a stated set of `format` numbers, at first only `1`, and reports any other.

## Not in the contract

- **Tuning values in the manifest.** Floor depth, canopy height and scroll speed belong to the game. They are set in `tuning.json` (see Tuning), which the manifest does not list and `format` does not cover; they are never changed in the headset.
- **Copyrighted files in the repository or the app.** Nothing the tool writes is committed or packed into the APK. The built-in placeholders are the author's own work.
- **How the game reaches the folder from Godot.** Story 1.4 settles that; the contract fixes only the location.
