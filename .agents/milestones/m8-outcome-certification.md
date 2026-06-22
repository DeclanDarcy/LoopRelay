# Milestone 8: Outcome-Oriented Certification

Goal: certify that reasoning can be reconstructed, not merely that low-level records exist.

## Backend Work

- [ ] Add `ReasoningCertificationReport`, `ReasoningCertificationEvidence`, and `ReasoningCertificationResult` models.
- [ ] Implement `IReasoningCertificationService`.
- [ ] Implement current certification read without persistence.
- [ ] Implement certification run with persisted report.
- [ ] Implement report listing.
- [ ] Certify repository recovery by rebuilding from `.agents/reasoning`.
- [ ] Certify restart recovery by creating a fresh service graph and reloading artifacts.
- [ ] Certify artifact recovery when markdown projections are missing but structured JSON exists.
- [ ] Certify low-level support invariants:
  - [ ] Event immutability.
  - [ ] Provenance completeness.
  - [ ] Relationship integrity.
  - [ ] Thread navigability.
  - [ ] Query reproducibility.
- [ ] Certify outcome scenarios:
  - [ ] Explain why a decision was superseded.
  - [ ] Explain why an alternative failed.
  - [ ] Explain why a contradiction mattered.
  - [ ] Explain why an assumption failed.
  - [ ] Explain why current strategy exists.
  - [ ] Reconstruct a reasoning thread across multiple milestones.
  - [ ] Reconstruct project reasoning from repository artifacts after restart.
- [ ] Add certification endpoints.

## UI Work

- [ ] Add `ReasoningCertificationPanel` showing current certification, persisted reports, pass/fail evidence, outcome scenarios, and recovery diagnostics.
- [ ] Link failed outcome evidence to the affected events, threads, relationships, or referenced domain artifacts where possible.

## Tests

- [ ] Certification passes for an empty repository with no reasoning artifacts and reports "no reasoning captured" as a valid baseline.
- [ ] Certification passes for a repository that can answer the required outcome scenarios.
- [ ] Certification fails when an event lacks provenance.
- [ ] Certification fails when a persisted relationship points to a missing reasoning node.
- [ ] Certification reports unresolved external references as diagnostics unless the target is mandatory for a scenario.
- [ ] Certification can rebuild from structured artifacts after deleting generated markdown projections.
- [ ] Certification survives service restart.
- [ ] Certification endpoint returns current report, persisted run, and history.
- [ ] UI characterization covers passed and failed outcome evidence.

## Exit Criteria

- [ ] Decision supersession reasoning is certifiable.
- [ ] Alternative rejection reasoning is certifiable.
- [ ] Contradiction importance is certifiable.
- [ ] Assumption failure is certifiable.
- [ ] Direction emergence is certifiable.
- [ ] Repository recovery is certifiable.
- [ ] Restart recovery is certifiable.
- [ ] Artifact recovery is certifiable.
