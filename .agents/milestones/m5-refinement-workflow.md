# Milestone 5: Decision Refinement Workflow

## Goal

support controlled proposal evolution with revision history.

## Backend Work

- [x] Add `DecisionProposalRevision`, `DecisionRefinementRequest`, `DecisionConstraint`, `DecisionAssumptionRevision`, `DecisionOptionRevision`, and `DecisionTradeoffRevision`.
- [x] Implement `IDecisionRefinementService`.
- [ ] Support refinement requests for:
  - [x] adding constraints
  - [ ] changing priorities
  - [x] challenging assumptions
  - [x] adding or removing options
  - [x] expanding tradeoffs
  - [x] changing recommendation rationale
- [x] Preserve removed options and retired assumptions in revision history.
- [x] Persist revision JSON and markdown comparison artifacts.
- [x] Track who or what requested refinement, reason, changed fields, accepted changes, rejected changes, and diagnostics.
- [x] Return refined proposals to a non-authoritative proposal state.

## UI Work

- [ ] Add refinement request form.
- [ ] Add revision history.
- [ ] Add revision comparison view.
- [ ] Distinguish current proposal content from historical revisions.

## Tests

- [x] Refinement request tests.
- [x] Revision history tests.
- [x] Removed option preservation tests.
- [x] Assumption evolution tests.
- [x] Revision comparison tests.
- [x] Attribution tests.

## Exit Criteria

- [x] Proposal evolution is explicit and traceable.
- [x] Refinement does not resolve decisions.
- [x] Every change has a reason and history entry.
- [x] Removed alternatives remain inspectable.
