# Milestone 7: Decision Governance

## Goal

detect decision ecosystem health issues before decisions are projected into execution.

## Backend Work

- [x] Add governance findings, categories, severities, reports, diagnostics, and health assessment models.
- [x] Implement `IDecisionGovernanceService`.
- [ ] Add analyzers for:
  - [x] consistency
  - [x] supersession lineage
  - [x] dependency integrity
  - [x] authority metadata
  - [x] authority boundary integrity
  - [x] resolved decision snapshot integrity
  - [ ] decision coverage
  - [x] proposal quality
  - [x] execution projection readiness
- [ ] Decision coverage analysis detects repeated ambiguity, repeated blockers, repeated governance findings, repeated forks, stale candidates, and repeated unresolved questions.
- [ ] Detect contradictory resolved decisions, conflicting directives, circular supersession, broken dependencies, missing resolver metadata, unresolved stale proposals, decisions not ready for projection, and projection failures.
  - [x] Contradictory resolved decisions.
  - [x] Circular supersession.
  - [x] Missing relationship targets.
  - [x] Missing or incomplete resolver metadata.
  - [x] Missing, incomplete, or invalid resolved proposal snapshots.
  - [x] Superseded decisions with missing or multiple replacement parents.
  - [x] DependsOn/Supports/Constrains relationships pointing at archived or superseded authority.
  - [x] Multiple accepted resolved decisions for one candidate.
  - [x] Conflicting execution directives.
  - [ ] Unresolved stale proposals.
  - [ ] Projection failures.
- [x] Persist governance reports under `.agents/decisions/governance`.
- [x] Mark blocking governance findings so execution projection can exclude affected decisions.

## UI Work

- [x] Add governance panel to the Decisions tab.
- [x] Show findings grouped by severity and category.
- [x] Link each finding to related decisions, candidates, proposals, relationships, artifacts, and source signals.
- [x] Keep findings advisory and non-mutating.

## Tests

- [x] Consistency analyzer tests.
- [x] Lineage analyzer tests.
- [x] Dependency analyzer tests.
- [x] Authority analyzer tests.
- [x] Decision coverage tests.
- [x] Execution projection readiness tests.
- [x] Snapshot integrity tests.
- [x] Governance report persistence tests.
- [x] UI finding display tests.

## Exit Criteria

- [x] Decision health is observable before execution consumption.
- [x] Findings are explainable and traceable.
- [x] Coverage gaps are visible.
- [x] Governance does not modify decisions, proposals, operational context, or execution state.
