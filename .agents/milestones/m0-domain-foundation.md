# Milestone 0: Decision Domain and Artifact Foundation

## Goal

establish first-class decision lifecycle objects and repository-owned structured artifacts.

This is one architectural milestone with multiple implementation slices. Complete and certify each slice independently before treating M0 as done:

- [x] M0A: domain primitives, state models, transition matrices, relationship rules, and in-memory tests.
- [x] M0B: repository-backed persistence, ID allocation, repository ownership, JSON schema versioning, and filesystem safety.
- [x] M0C: markdown projection generation, `decisions.md` index compatibility, artifact discovery compatibility, and rotation compatibility.
- [ ] M0D: recovery from structured artifacts, projection regeneration, repository restart recovery, and full M0 regression tests.

## Backend Work

- [x] Add `CommandCenter.Decisions` project and solution entry.
- [x] Add primitives and models for decisions, outcomes, classifications, relationships, metadata, candidates, proposals, proposal states, candidate states, and history entries.
- [x] Implement transition validation for decisions, candidates, and proposals.
- [x] Implement relationship validation for duplicate relationships, self-reference rules, and unsupported relationship types.
- [x] Implement `IDecisionRepository` with an in-memory test double and a file-system implementation.
- [x] Implement ID allocation by scanning existing lifecycle artifacts.
- [x] Implement repository ownership on every lifecycle record.
- [x] Finalize `.agents/decisions` structured layout.
- [x] Implement `DecisionArtifactProjectionService`.
- [x] Render `decision.md`, `candidate.md`, `proposal.md`, and the current `decisions.md` index from structured records.
- [x] Preserve existing `ArtifactService` discovery for `decisions.md` and `decisions.NNNN.md`.
- [x] Extend artifact discovery only where markdown lifecycle projections should appear in the artifact browser.
- [x] Keep JSON lifecycle files out of the generic artifact editor unless a typed editor exists.
- [x] Reuse `ArtifactRotationService.RotateCurrentDecisionsAsync` for current decisions index snapshots.
- [ ] Add safe recovery when markdown projections are missing but structured records exist.
- [x] Add DI extension `AddDecisions()` and register it from backend startup.

## Tests

- [x] Decision state transition tests.
- [x] Candidate state transition tests.
- [x] Proposal state transition tests.
- [x] Outcome/state distinction tests.
- [x] ID allocation tests.
- [x] Relationship validation tests.
- [x] File-system round trip tests.
- [x] Repository isolation tests.
- [x] Projection generation tests.
- [x] Decision index rendering tests.
- [x] Existing decision artifact discovery compatibility tests.
- [ ] Recovery tests from structured artifacts after deleting generated markdown projections.

## Exit Criteria

- [ ] Decision aggregate is first-class and independent from markdown.
- [ ] Candidate and proposal lifecycles are explicit.
- [ ] Stable IDs survive reload.
- [x] Structured lifecycle artifacts are repository-owned and reloadable.
- [x] Human-readable projections exist.
- [x] Existing decision markdown discovery remains compatible.
- [ ] Invalid transitions and relationships are rejected.
