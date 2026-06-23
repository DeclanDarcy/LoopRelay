# Milestone 8: Outcome-Oriented Certification

Goal: certify that reasoning can be reconstructed, not merely that low-level records exist.

## Backend Work

- [x] Add `ReasoningCertificationReport`, `ReasoningCertificationEvidence`, and `ReasoningCertificationResult` models.
- [x] Implement `IReasoningCertificationService`.
- [x] Implement current certification read without persistence.
- [x] Implement certification run with persisted report.
- [x] Implement report listing.
- [x] Certify repository recovery by rebuilding from `.agents/reasoning`.
- [x] Certify restart recovery by creating a fresh service graph and reloading artifacts.
- [x] Certify artifact recovery when markdown projections are missing but structured JSON exists.
- [x] Certify low-level support invariants:
  - [x] Event immutability.
  - [x] Provenance completeness.
  - [x] Relationship integrity.
  - [x] Thread navigability.
  - [x] Query reproducibility.
- [x] Certify outcome scenarios:
  - [x] Explain why a decision was superseded.
  - [x] Explain why an alternative failed.
  - [x] Explain why a contradiction mattered.
  - [x] Explain why an assumption failed.
  - [x] Explain why current strategy exists.
  - [x] Reconstruct a reasoning thread across multiple milestones.
  - [x] Reconstruct project reasoning from repository artifacts after restart.
- [x] Add certification endpoints.

## UI Work

- [x] Add `ReasoningCertificationPanel` showing current certification, persisted reports, pass/fail evidence, outcome scenarios, and recovery diagnostics.
- [x] Link failed outcome evidence to the affected events, threads, relationships, or referenced domain artifacts where possible.

## Tests

- [x] Certification passes for an empty repository with no reasoning artifacts and reports "no reasoning captured" as a valid baseline.
- [x] Certification passes for a repository that can answer the required outcome scenarios.
- [x] Certification fails when an event lacks provenance.
- [x] Certification fails when a persisted relationship points to a missing reasoning node.
- [x] Certification reports unresolved external references as diagnostics unless the target is mandatory for a scenario.
- [x] Certification can rebuild from structured artifacts after deleting generated markdown projections.
- [x] Certification survives service restart.
- [x] Certification endpoint returns current report, persisted run, and history.
- [x] UI characterization covers passed and failed outcome evidence.

## Exit Criteria

- [x] Decision supersession reasoning is certifiable.
- [x] Alternative rejection reasoning is certifiable.
- [x] Contradiction importance is certifiable.
- [x] Assumption failure is certifiable.
- [x] Direction emergence is certifiable.
- [x] Repository recovery is certifiable.
- [x] Restart recovery is certifiable.
- [x] Artifact recovery is certifiable.
