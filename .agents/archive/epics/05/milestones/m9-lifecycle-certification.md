# Milestone 9: Lifecycle Certification

## Goal

prove the decision lifecycle works end to end, recovers from repository artifacts, scales to long histories, and preserves authority boundaries.

## Backend Work

- [x] Add `DecisionLifecycleCertificationResult`, `DecisionCertificationReport`, `DecisionCertificationEvidence`, and long-horizon fixtures.
- [x] Implement `IDecisionCertificationService`.
- [x] Validate:
  - [x] context resolution
  - [x] discovery
  - [x] candidate lifecycle
  - [x] proposal generation
  - [x] proposal lifecycle
  - [x] review
  - [x] refinement
  - [x] resolution
  - [x] governance
  - [x] execution consumption
  - [x] operational-context assimilation boundary
  - [x] authority boundaries
  - [x] recovery after reload
  - [x] artifact reconstruction
  - [x] multi-repository isolation
  - [x] long-horizon decision histories
- [x] Generate certification reports under `.agents/decisions/certification`.

## UI Work

- [x] Add certification panel.
- [x] Show pass/fail status, evidence, findings, and coverage without turning metrics into workflow authority.

## Tests

- [x] End-to-end lifecycle tests.
- [x] Restart recovery tests.
- [x] Artifact recovery tests.
- [x] Multi-repository isolation tests.
- [x] Long-horizon scale tests with 50, 100, and 200 decision histories.
- [x] Authority boundary tests proving execution and governance cannot resolve decisions.
- [x] Assimilation boundary tests proving decision resolution does not mutate operational context.
- [x] Certification report reproducibility tests.

## Exit Criteria

- [x] Complete lifecycle is certified.
- [x] Repository authority survives restart and artifact projection regeneration.
- [x] Multi-repository histories remain independent.
- [x] Governed resolved decisions influence execution.
- [x] Governance findings remain advisory.
- [x] Operational-context assimilation remains review-mediated.
