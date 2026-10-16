---
id: 1
type: story
title: "An empty asset list is reported as a problem by the game and the contract"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: false
risk: low
---

# An empty asset list is reported as a problem by the game and the contract

## Description

A manifest whose `assets` list is empty is a problem, named on the launch report board and in the log, as `tools/manifest.py` already treats it, and the asset contract says so.

## Acceptance Criteria

Verify: a pushed manifest with an empty `assets` list shows a board naming the problem, every ID shows its placeholder, and the contract's rule matches what the game and `write_manifest.py --check` do.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md, Decisions and finding S4
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, The manifest

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Decision: an empty `assets` list is an error (author, 2026-10-16, engine spike retrospective).
- Today the game shows no board for it, and the contract allows it.
