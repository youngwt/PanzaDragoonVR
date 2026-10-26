---
id: 14
type: story
title: "The disc image and release-mode rules follow the retrospective's decisions"
parent: none
covers: []
after: []
assignee: ""
refined: false
hitl: true
risk: medium
---

# The disc image and release-mode rules follow the retrospective's decisions

## Description

Three rule changes the author decided on 2026-10-21, in the asset contract and the code: a cue sheet that names an `.iso` counts as one disc image with it, so that dump layout extracts; in release mode an extraction that wrote nothing keeps the disc image; and the release-mode skip also compares the extractor's version, so an app update can extract again once the player copies the disc image back; the image is not kept on the headset.

## Acceptance Criteria

Verify: the contract states the three rules and the author has agreed it; tests cover each; on the headset a cue naming an `.iso` extracts, and a release-mode run with a newer extractor version extracts again.

## References

- parent — none
- retrospective — _bmad-output/initiative-panzer-dragoon-zwei-vr/epic-extraction-tool/epic-extraction-tool-retrospective.md, findings R5, R6, R7 and the Decisions section
- contract — _bmad-output/initiative-panzer-dragoon-zwei-vr/spec-panzer-dragoon-zwei-vr/asset-contract.md, Extraction

## Notes

- Source: carried out of the in-game extraction epic at its close (author, 2026-10-21); kept in the backlog.
- Decision: a cue naming an `.iso` is one image; release mode keeps the image when nothing was written; the skip compares the extractor version (author, 2026-10-21).
- Decision: release mode still deletes the disc image after a successful extraction; to pick up a newer extractor, the player copies the image to the headset again, and the version check then lets it extract (author, 2026-10-21).
- Hitl: contract changes need the author's yes; headset check.
- Priority: build next, before the next epic (author, 2026-10-26). Ticket 3.4 raised the extractor to 0.2 so the floor and canopy reach a release-mode folder extracted by 0.1 only through this version check (forest floor and canopy retrospective, F12 and R1).
- The headset check also covers what the forest epic never ran on the headset: release mode with the planes, and the European ISO (forest retrospective, R4).
