---
id: 7
type: story
title: "The stack notes and the asset contract match what was built"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: low
---

# The stack notes and the asset contract match what was built

## Description

`stack.md` and the asset contract stop saying things the engine spike proved false or settled, and the contract records where it and the code differ, with the author choosing which wins in each case.

## Acceptance Criteria

Verify: every stale statement and every difference listed in the retrospective's S2 and S4 is either corrected or recorded as a decision, and the author has agreed the contract changes.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md, findings S2 and S4; action A5
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool-retrospective.md, findings R8, S2 and S3; action A3
- stack — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/stack.md

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Hitl: the contract is the author's agreement; changes to it need the author's yes.
- The empty asset list is story 1, not this one.
- Added at the in-game extraction epic's close (author, 2026-10-21): also the extraction epic's doc items: add `extracting/` to the contract's fixed folder names; record the texture numbering rule (the record scan and the fits-the-`.CGB` filter) in the contract or the formats notes; `stack.md` saying the library is "not built yet"; `zwei-disc-formats.md` calling mode 5 "Not handled"; the spec's answered palette question; the contract's "Story 1.4 settles that".
- Open question: the contract's "not a Saturn disc" row has no check in the code; keep the row and add a check, or drop it.
