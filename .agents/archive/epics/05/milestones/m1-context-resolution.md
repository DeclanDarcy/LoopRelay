# Milestone 1: Decision Context Resolution

## Goal

build deterministic decision context from repository state without creating decisions or recommendations.

## Backend Work

- [x] Add `DecisionContext`, `DecisionContextSnapshot`, `DecisionContextValidationResult`, `DecisionContextDiagnostics`, and source attribution models.
- [x] Implement `IDecisionContextService`.
- [ ] Load and attribute:
  - [x] `.agents/plan.md`
  - [x] selected or active `.agents/milestones/*.md`
  - [x] `.agents/operational_context.md`
  - [x] current and historical structured decisions
  - [x] current decision markdown where structured records are absent
  - [x] recent handoffs
  - [x] continuity diagnostics when available
- [x] Classify required inputs and optional inputs.
- [x] Preserve source references for every context item.
- [x] Persist immutable context snapshots under `.agents/decisions/contexts`.
- [x] Add context inspection endpoints.

## Tests

- [x] Context assembly tests.
- [x] Missing required input tests.
- [x] Optional source omission tests.
- [x] Source attribution tests.
- [x] Snapshot repeatability tests.
- [x] Repository restart recovery tests.

## Exit Criteria

- [x] Same repository state produces deterministic decision context.
- [x] Context diagnostics explain loaded, missing, and warning sources.
- [x] Later decision services consume `DecisionContext` without reading repository files directly.
