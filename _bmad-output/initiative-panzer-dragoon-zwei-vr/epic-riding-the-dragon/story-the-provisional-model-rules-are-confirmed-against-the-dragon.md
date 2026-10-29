---
id: 6
type: story
title: "The provisional model rules are confirmed against the dragon"
parent: epic-riding-the-dragon
covers: [CAP-3]
after: [5]
assignee: ""
refined: false
hitl: true
risk: medium
---

# The provisional model rules are confirmed against the dragon

## Description

The rules marked Provisional in the asset contract's Models section are checked against the extracted dragon and confirmed or changed, including the Saturn-to-metres divisor, with the `.glb` writer and its tests changed to match.

## Acceptance Criteria

Verify: no rule in the Models section is still marked Provisional; each change has the author's agreement; the tests and the dragon `.glb` files follow the changed rules.

## References

- parent — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-riding-the-dragon/epic-riding-the-dragon.md
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Models
- deferred work — _bmad-output/initiative-panzer-dragoon-zwei-vr/deferred-work.md, entry on the Models section

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Needs decoded model geometry, which comes with the dragon epic; it may move into that epic when it is planned.
- Moved into the riding the dragon epic as story 6, was backlog 8 (author, 2026-10-29). The backlog ticket's "Saturn colour divisor" was the Saturn-to-metres divisor; the contract has no colour divisor.
