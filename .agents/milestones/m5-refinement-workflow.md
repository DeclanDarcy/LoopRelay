# Milestone 5: Decision Refinement Workflow

## Goal

support controlled proposal evolution with revision history.

## Backend Work

- [ ] Add `DecisionProposalRevision`, `DecisionRefinementRequest`, `DecisionConstraint`, `DecisionAssumptionRevision`, `DecisionOptionRevision`, and `DecisionTradeoffRevision`.
- [ ] Implement `IDecisionRefinementService`.
- [ ] Support refinement requests for:
  - [ ] adding constraints
  - [ ] changing priorities
  - [ ] challenging assumptions
  - [ ] adding or removing options
  - [ ] expanding tradeoffs
  - [ ] changing recommendation rationale
- [ ] Preserve removed options and retired assumptions in revision history.
- [ ] Persist revision JSON and markdown comparison artifacts.
- [ ] Track who or what requested refinement, reason, changed fields, accepted changes, rejected changes, and diagnostics.
- [ ] Return refined proposals to a non-authoritative proposal state.

## UI Work

- [ ] Add refinement request form.
- [ ] Add revision history.
- [ ] Add revision comparison view.
- [ ] Distinguish current proposal content from historical revisions.

## Tests

- [ ] Refinement request tests.
- [ ] Revision history tests.
- [ ] Removed option preservation tests.
- [ ] Assumption evolution tests.
- [ ] Revision comparison tests.
- [ ] Attribution tests.

## Exit Criteria

- [ ] Proposal evolution is explicit and traceable.
- [ ] Refinement does not resolve decisions.
- [ ] Every change has a reason and history entry.
- [ ] Removed alternatives remain inspectable.
