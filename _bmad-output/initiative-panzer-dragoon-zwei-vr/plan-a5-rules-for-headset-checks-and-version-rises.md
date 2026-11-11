---
title: 'Rules for headset checks, frozen plans and version rises'
type: 'chore'
ticket: ''
created: '2026-11-09'
status: 'done'
baseline_revision: '8f7fb4e2fac563e3082754daab4913bc8a646e88'
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

**Problem:** Action item A5 of the riding the dragon retrospective (`epic-riding-the-dragon/epic-riding-the-dragon-retrospective.md`, F10, F12, R11) found three lessons that no rule records:
- A ticket whose criterion is a look in the headset was marked done with no word on each item, in 4.7 and 4.8.
- A frozen plan's Never was overridden inside a feature commit, in backlog 21.
- `Extraction.Version` was raised in 4.7 while the library's `.csproj` version was left behind.

**Approach:** Record the first two as rules in `AGENTS.md`. Hold the third with a test, not a rule: the library's `.csproj` `<Version>` must be `Extraction.Version` followed by `.0`.

</frozen-after-approval>

## Implementation Notes

Oneshot: two bullets in `AGENTS.md` and one test in `tests/GameLogic.Tests/`, which already references the library and has `Repository.Root()`.

Checks: `tools/test.sh` all pass (105 Python, 352 GameLogic, 253 Decoding). With the `.csproj` changed to 0.4.1 in a scratch edit, `LibraryVersion_TheProjectsIsTheExtractors` failed; the edit was reverted.

## Review Triage Log

Quick lens, one pass: low 4, all patched.

- low, patch: `AGENTS.md` said the version is "in two" places, though the README's manifest paragraph and the asset contract's two examples carry it too, with no test. The paragraph now names every place a rise changes.
- low, patch: "`Extraction.Version`, which release mode acts on" skipped that release mode compares the generator, `Manifest.Generator`. Reworded, in `AGENTS.md` and the test's comment.
- low, patch: the test searched the whole `.csproj` text, so a `<Version>` in a comment or a second one would pass. It now reads the single `Version` element.
- low, patch: the headset rule did not ask the ticket to name the items to look at, as the retrospective's F10 does. Added.

## Verification

**Commands:**
- `tools/test.sh` -- expected: all pass
