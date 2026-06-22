# Milestone 7: Decision Governance

## Goal

detect decision ecosystem health issues before decisions are projected into execution.

## Backend Work

- [ ] Add governance findings, categories, severities, reports, diagnostics, and health assessment models.
- [ ] Implement `IDecisionGovernanceService`.
- [ ] Add analyzers for:
  - [ ] consistency
  - [ ] supersession lineage
  - [ ] dependency integrity
  - [ ] authority metadata
  - [ ] decision coverage
  - [ ] proposal quality
  - [ ] execution projection readiness
- [ ] Decision coverage analysis detects repeated ambiguity, repeated blockers, repeated governance findings, repeated forks, stale candidates, and repeated unresolved questions.
- [ ] Detect contradictory resolved decisions, conflicting directives, circular supersession, broken dependencies, missing resolver metadata, unresolved stale proposals, decisions not ready for projection, and projection failures.
- [ ] Persist governance reports under `.agents/decisions/governance`.
- [ ] Mark blocking governance findings so execution projection can exclude affected decisions.

## UI Work

- [ ] Add governance panel to the Decisions tab.
- [ ] Show findings grouped by severity and category.
- [ ] Link each finding to related decisions, candidates, proposals, relationships, artifacts, and source signals.
- [ ] Keep findings advisory and non-mutating.

## Tests

- [ ] Consistency analyzer tests.
- [ ] Lineage analyzer tests.
- [ ] Dependency analyzer tests.
- [ ] Authority analyzer tests.
- [ ] Decision coverage tests.
- [ ] Execution projection readiness tests.
- [ ] Governance report persistence tests.
- [ ] UI finding display tests.

## Exit Criteria

- [ ] Decision health is observable before execution consumption.
- [ ] Findings are explainable and traceable.
- [ ] Coverage gaps are visible.
- [ ] Governance does not modify decisions, proposals, operational context, or execution state.
