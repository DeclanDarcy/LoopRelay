# Milestone 1: Decision Context Resolution

## Goal

build deterministic decision context from repository state without creating decisions or recommendations.

## Backend Work

- [ ] Add `DecisionContext`, `DecisionContextSnapshot`, `DecisionContextValidationResult`, `DecisionContextDiagnostics`, and source attribution models.
- [ ] Implement `IDecisionContextService`.
- [ ] Load and attribute:
  - [ ] `.agents/plan.md`
  - [ ] selected or active `.agents/milestones/*.md`
  - [ ] `.agents/operational_context.md`
  - [ ] current and historical structured decisions
  - [ ] current decision markdown where structured records are absent
  - [ ] recent handoffs
  - [ ] continuity diagnostics when available
- [ ] Classify required inputs and optional inputs.
- [ ] Preserve source references for every context item.
- [ ] Persist immutable context snapshots under `.agents/decisions/contexts`.
- [ ] Add context inspection endpoints.

## Tests

- [ ] Context assembly tests.
- [ ] Missing required input tests.
- [ ] Optional source omission tests.
- [ ] Source attribution tests.
- [ ] Snapshot repeatability tests.
- [ ] Repository restart recovery tests.

## Exit Criteria

- [ ] Same repository state produces deterministic decision context.
- [ ] Context diagnostics explain loaded, missing, and warning sources.
- [ ] Later decision services consume `DecisionContext` without reading repository files directly.
