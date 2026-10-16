---
id: 2
type: story
title: "The PC tools and the game agree on every manifest and model rule, proven by shared test cases"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: false
risk: medium
---

# The PC tools and the game agree on every manifest and model rule, proven by shared test cases

## Description

One set of test files (manifests, PNGs, `.glb` files, each with its expected result) runs in both the Python and the C# test suites, and `tools/manifest.py` is brought into line with the game: it applies the contract's invalid-model table, rejects `NaN`, `Infinity` and deep nesting, and uses the game's wording for each problem. The test model from `make_test_model.py` is checked by the game's model check too.

## Acceptance Criteria

Verify: `tools/test.sh` runs the shared cases in both suites and every case gives the same result on both sides; a model the game rejects fails `write_manifest.py --check`.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-engine-spike/epic-engine-spike-retrospective.md, findings R1, R2, V1, A-dup; action A1
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, The manifest and Models

## Notes

- Source: carried out of the engine spike epic at its close (author, 2026-10-16); kept in the backlog for a possible backlog epic later.
- Best done before the extraction tool epic writes real manifests (retrospective A1).
