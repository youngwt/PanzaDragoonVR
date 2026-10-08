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
| `e03.` | Episode 3, such as `e03.floor` and `e03.canopy`, and its enemies. |
| `dragon.` | The dragon. |

- The table lists the prefixes in use. A new one is added here when it is first needed.
- The engine spike's ground and sky are the placeholders for `e03.floor` and `e03.canopy`. There are no separate test IDs: the author proves external loading by supplying their own images under the real IDs.
- An ID is a promise. Once the game asks for an ID, the tool keeps writing it. The file behind it may move or be renamed freely.

### Paths

- Relative to the asset folder, with forward slashes.
- No leading slash, no `.` or `..` parts, no backslashes.
- Lowercase ASCII letters, digits, underscores, hyphens and dots only, so the same path works on every file system.

### Kinds

| Kind | File format |
|------|-------------|
| `image` | PNG, 8 bits per channel, RGB or RGBA. |
| `model` | Reserved. The format is decided in story 1.6 and added here. Until then no manifest should contain it, and the game treats it as a kind it does not know. |

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

- `format` rises only when a game built for the old number could misread the new manifest.
- Adding an optional field, a new kind or new IDs does not change `format`. The game ignores fields it does not know.
- Each build of the game supports a stated set of `format` numbers, at first only `1`, and reports any other.

## Not in the contract

- **Tuning values.** Floor depth, canopy height and scroll speed belong to the game and are set in the headset.
- **Copyrighted files in the repository or the app.** Nothing the tool writes is committed or packed into the APK. The built-in placeholders are the author's own work.
- **How the game reaches the folder from Godot.** Story 1.4 settles that; the contract fixes only the location.
