---
title: 'A lone surrogate in a manifest entry is an invalid entry'
type: 'bugfix'
created: '2026-10-16'
status: 'built'
baseline_revision: '98e27a48c6f52c437c6668a8b51cc7131445df8f'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A manifest entry whose `id`, `kind` or `path` holds a lone surrogate escape (such as `"\ud800"`) parses as JSON, but `JsonElement.GetString()` then throws `InvalidOperationException` in `game/Logic/AssetManifest.cs`. `Parse` catches only `JsonException`, so the exception leaves `AssetFolder.Open()`, and the launch report says the asset folder was not found although it was (engine spike retrospective, finding R3, action A2; the author asked for the fix on 2026-10-16).

**Approach:** Read those three strings in a way that cannot throw, and report an unreadable one as an invalid entry like any other bad field, as `GlbCheck` already does for the same case in models. The other entries keep loading. Unit tests cover each of the three fields and show the remaining entries are unaffected.

</frozen-after-approval>

## Implementation Notes

Oneshot: one engine-free file and its tests, well under 100 lines.

- `AssetManifest.Text(JsonElement)` reads a JSON string or gives null, catching the `InvalidOperationException` a lone surrogate throws. `ReadEntries` reads `id`, `path` and `kind` through it.
- `EntryProblem` adds `field '<name>' is not readable text: it holds a lone surrogate escape` for any of the three that is a string but cannot be read. With an unreadable `id` the entry is labelled `entry N`, as for an absent ID, so no other ID is hidden.
- Three tests: one for `id`, and a theory for `kind` and `path`. Run against the old code, the `kind` and `path` cases fail with the `InvalidOperationException`. `tools/test.sh`: 73 Python and 149 C# tests pass.
- Review patch: an unreadable `kind` is no longer an invalid entry. It reads as its JSON text, a kind the game does not know, so it becomes "wrong kind" only when that ID is asked for. That matches the contract and `tools/manifest.py --check`, which gives a notice. The tests are now one each for `id`, `path` and `kind`; 73 Python and 149 C# tests pass.

## Review Triage Log

Quick lens, 2026-10-16: high 0, medium 0, low 1, false 0, maybe-false 0. One patched.

- low, patched: an unreadable `kind` was an invalid entry in the game, but `tools/manifest.py --check` gives only a notice for an unknown kind, against the "must agree" rule at `AssetManifest.cs:48-51`. It is now treated as an unknown kind. The `id` and `path` cases already agreed in outcome with `--check`, though the wording differs; the wording falls under the retrospective's action A1.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all Python and C# tests pass, including the new ones.
