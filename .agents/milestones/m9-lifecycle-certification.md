# Milestone 9: Lifecycle Certification

## Goal

prove the decision lifecycle works end to end, recovers from repository artifacts, scales to long histories, and preserves authority boundaries.

## Backend Work

- [ ] Add `DecisionLifecycleCertificationResult`, `DecisionCertificationReport`, `DecisionCertificationEvidence`, and long-horizon fixtures.
- [ ] Implement `IDecisionCertificationService`.
- [ ] Validate:
  - [ ] context resolution
  - [ ] discovery
  - [ ] candidate lifecycle
  - [ ] proposal generation
  - [ ] proposal lifecycle
  - [ ] review
  - [ ] refinement
  - [ ] resolution
  - [ ] governance
  - [ ] execution consumption
  - [ ] operational-context assimilation boundary
  - [ ] authority boundaries
  - [ ] recovery after reload
  - [ ] artifact reconstruction
  - [ ] multi-repository isolation
  - [ ] long-horizon decision histories
- [ ] Generate certification reports under `.agents/decisions/certification`.

## UI Work

- [ ] Add certification panel.
- [ ] Show pass/fail status, evidence, findings, and coverage without turning metrics into workflow authority.

## Tests

- [ ] End-to-end lifecycle tests.
- [ ] Restart recovery tests.
- [ ] Artifact recovery tests.
- [ ] Multi-repository isolation tests.
- [ ] Long-horizon scale tests with 50, 100, and 200 decision histories.
- [ ] Authority boundary tests proving execution and governance cannot resolve decisions.
- [ ] Assimilation boundary tests proving decision resolution does not mutate operational context.
- [ ] Certification report reproducibility tests.

## Exit Criteria

- [ ] Complete lifecycle is certified.
- [ ] Repository authority survives restart and artifact projection regeneration.
- [ ] Multi-repository histories remain independent.
- [ ] Governed resolved decisions influence execution.
- [ ] Governance findings remain advisory.
- [ ] Operational-context assimilation remains review-mediated.
