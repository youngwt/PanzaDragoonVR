---
title: 'Decide the manifest format and folder layout'
type: 'chore'
ticket: '2'
created: '2026-10-08'
status: done
baseline_revision: '63fae81e12715613c8b0d3f8a767be61bc9a81dc'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The extraction tool and the game meet only at a folder of files and its manifest, and neither the manifest's format nor the folder's layout is defined. The extraction tool epic and stories 1.4 to 1.7 cannot start without it.

**Approach:** Write one asset contract document in the spec folder that fixes the folder location and layout, the manifest format with a worked example, and what the game checks at launch. The author decides the open points and agrees the document.

**Decisions (author, 2026-10-08):**
- The game finds an asset by its ID in the manifest, not by a fixed file path.
- At launch the game checks that every listed file is present and has the size the manifest records.
- The checksum per file is optional, and the game does not check it (author, revising the earlier choice after review).
- The game has its own built-in placeholder assets and always runs. Assets from the folder replace placeholders ID by ID, so a player who has not run the tool still sees a scene, even if it is grey cubes.
- One manifest, written by the tool. No separate test IDs or packs.
- Agreed as drafted: JSON; ID, kind, path and byte size on every entry; dot-separated lowercase IDs with `e03.` and `dragon.` prefixes; file names and subfolders left to the tool; `model` kind reserved for story 1.6; the launch report lists every problem and never opens a path outside the folder; each game build states the manifest versions it supports.

</frozen-after-approval>

## Implementation Notes

One-shot route: the change is a single short document and two small edits to the spec, with no code.

- Wrote `spec-panzer-dragoon-zwei-vr/asset-contract.md`: folder location, manifest fields, ID and path rules, kinds, the launch check and versioning.
- Choices first drafted beyond the author's two decisions (since agreed or revised; see Decisions): JSON as the format; `bytes` required and `sha256` optional so a manifest can be written by hand; ID prefixes `test.`, `e03.`, `dragon.`; the `model` kind reserved for story 1.6; subfolders left to the tool; unknown kinds are an error only when asked for.
- Spec: added the contract to `companions`, pointed the two-tracks constraint at it, and removed the open question it answers.

## Plan Change Log

## Review Triage Log

Pass 1 (quick lens): high 0, medium 2, low 8, false 0, maybe-false 0. One rejected low.

- medium, halted for the author: agreement was not recorded. The author has since agreed choices 1 to 7.
- medium, halted for the author: optional `sha256` contradicted the frozen decision that the manifest carries a checksum. First patched to required; the author then chose optional, recorded in Decisions.
- medium, halted for the author: one manifest could not hold both hand-supplied test assets and tool output. The author chose built-in placeholders replaced ID by ID, which removes the separate test assets.
- low, patched: the launch table lacked duplicate IDs, invalid paths, negative sizes and wrong or unknown kinds. Rows added.
- low, patched: behaviour of the reserved `model` kind before story 1.6 was undefined. Now treated as an unknown kind.
- low, patched: the ID pattern allowed empty levels. Pattern tightened; the prefix table is stated as not exhaustive.
- low, patched: "accepts any format up to" committed the game to reading old incompatible formats. Now a stated supported set.
- low, patched: the folder tree did not say the output folder's contents, not the folder, are copied.
- low, patched: an unsourced claim about how SideQuest transfers fail. Removed.
- low, patched: the spec's memlog still carried the manifest question. Decision entries appended.
- low, rejected: the spec and `stack.md` still give the folder as `Android/data/<package>/files`. That is consistent with the contract's full path, so nothing is wrong.

## Verification

**Manual checks (if no CLI):**
- `spec-panzer-dragoon-zwei-vr/asset-contract.md` exists, holds a worked example manifest that parses as JSON, and the author has agreed it.
