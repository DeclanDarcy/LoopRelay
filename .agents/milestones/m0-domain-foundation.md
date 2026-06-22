# Milestone 0: Decision Domain and Artifact Foundation

## Goal

establish first-class decision lifecycle objects and repository-owned structured artifacts.

This is one architectural milestone with multiple implementation slices. Complete and certify each slice independently before treating M0 as done:

- [ ] M0A: domain primitives, state models, transition matrices, relationship rules, and in-memory tests.
- [ ] M0B: repository-backed persistence, ID allocation, repository ownership, JSON schema versioning, and filesystem safety.
- [ ] M0C: markdown projection generation, `decisions.md` index compatibility, artifact discovery compatibility, and rotation compatibility.
- [ ] M0D: recovery from structured artifacts, projection regeneration, repository restart recovery, and full M0 regression tests.

## Backend Work

- [ ] Add `CommandCenter.Decisions` project and solution entry.
- [ ] Add primitives and models for decisions, outcomes, classifications, relationships, metadata, candidates, proposals, proposal states, candidate states, and history entries.
- [ ] Implement transition validation for decisions, candidates, and proposals.
- [ ] Implement relationship validation for duplicate relationships, self-reference rules, and unsupported relationship types.
- [ ] Implement `IDecisionRepository` with an in-memory test double and a file-system implementation.
- [ ] Implement ID allocation by scanning existing lifecycle artifacts.
- [ ] Implement repository ownership on every lifecycle record.
- [ ] Finalize `.agents/decisions` structured layout.
- [ ] Implement `DecisionArtifactProjectionService`.
- [ ] Render `decision.md`, `candidate.md`, `proposal.md`, and the current `decisions.md` index from structured records.
- [ ] Preserve existing `ArtifactService` discovery for `decisions.md` and `decisions.NNNN.md`.
- [ ] Extend artifact discovery only where markdown lifecycle projections should appear in the artifact browser.
- [ ] Keep JSON lifecycle files out of the generic artifact editor unless a typed editor exists.
- [ ] Reuse `ArtifactRotationService.RotateCurrentDecisionsAsync` for current decisions index snapshots.
- [ ] Add safe recovery when markdown projections are missing but structured records exist.
- [ ] Add DI extension `AddDecisions()` and register it from backend startup.

## Tests

- [ ] Decision state transition tests.
- [ ] Candidate state transition tests.
- [ ] Proposal state transition tests.
- [ ] Outcome/state distinction tests.
- [ ] ID allocation tests.
- [ ] Relationship validation tests.
- [ ] File-system round trip tests.
- [ ] Repository isolation tests.
- [ ] Projection generation tests.
- [ ] Decision index rendering tests.
- [ ] Existing decision artifact discovery compatibility tests.
- [ ] Recovery tests from structured artifacts after deleting generated markdown projections.

## Exit Criteria

- [ ] Decision aggregate is first-class and independent from markdown.
- [ ] Candidate and proposal lifecycles are explicit.
- [ ] Stable IDs survive reload.
- [ ] Structured lifecycle artifacts are repository-owned and reloadable.
- [ ] Human-readable projections exist.
- [ ] Existing decision markdown discovery remains compatible.
- [ ] Invalid transitions and relationships are rejected.
