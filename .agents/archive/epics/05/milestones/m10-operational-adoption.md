# Milestone 10: Operational Adoption and Long-Horizon Validation

## Goal

validate whether the certified decision lifecycle produces useful outcomes in real project work.

## Backend Work

- [ ] Add `DecisionOperationalAdoptionReport`, adoption evidence models, and long-horizon usage metrics.
- [ ] Implement `IDecisionOperationalAdoptionService`.
- [ ] Generate adoption reports under `.agents/decisions/adoption`.
- [ ] Track:
  - [ ] discovered candidates
  - [ ] dismissed candidates
  - [ ] expired candidates
  - [ ] duplicate candidates
  - [ ] generated proposals
  - [ ] reviewed proposals
  - [ ] refined proposals
  - [ ] resolved proposals
  - [ ] ignored proposals
  - [ ] accepted recommendations
  - [ ] overridden recommendations
  - [ ] deferred decisions
  - [ ] supersession frequency
  - [ ] constraint reuse
  - [ ] execution influence
  - [ ] decision coverage findings
  - [ ] governance usefulness
  - [ ] governance noise
- [ ] Produce operational reports without mutating decisions, operational context, execution state, or governance findings.

## UI Work

- [ ] Add operational adoption report viewer.
- [ ] Show adoption trends, lifecycle throughput, governance usefulness, and execution influence as observational data.
- [ ] Keep all adoption metrics non-authoritative.

## Tests

- [ ] Adoption report generation tests.
- [ ] Adoption metric calculation tests.
- [ ] Multi-repository adoption isolation tests.
- [ ] Governance usefulness/noise report tests.
- [ ] Tests proving adoption reporting is read-only.

## Exit Criteria

- [ ] Real repositories can exercise the full lifecycle.
- [ ] Long-horizon decision behavior is observable.
- [ ] Adoption evidence is collected without becoming workflow authority.
- [ ] Operational reports show whether the lifecycle is worth using.
