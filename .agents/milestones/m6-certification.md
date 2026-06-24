# Milestone 6: Certification

Objective: prove lifecycle correctness without adding new lifecycle behavior.

Add certification models:

- [x] `DecisionSessionCertificationResult`
- [x] `DecisionSessionCertificationFinding`
- [x] `DecisionSessionCertificationReport`
- [x] `DecisionSessionGovernanceReport`
- [x] `DecisionSessionHealthReport`
- [x] `DecisionSessionLifecycleEndToEndFixture`

Add certification service:

- [x] `IDecisionSessionCertificationService`
- [x] `DecisionSessionCertificationService`

Certification categories:

- [x] Authority
- [x] Single active session
- [x] Analysis determinism
- [x] TTL and cache risk
- [x] Policy determinism
- [x] Transfer eligibility
- [x] Continuity artifact
- [x] Transfer
- [x] Recovery
- [x] Continuity
- [x] Workflow integration
- [x] Diagnostics
- [x] Health

Certification rules:

- [x] Fail if more than one active session exists.
- [x] Fail if analysis is non-deterministic for identical inputs.
- [x] Fail if TTL or cache miss risk is missing from analysis.
- [x] Fail if policy is non-deterministic for identical inputs.
- [x] Fail if transfer executes while eligibility is blocked or deferred.
- [x] Fail if transfer lacks a valid continuity artifact.
- [x] Fail if transfer lacks source session retirement, replacement session activation, or continuity evidence.
- [x] Fail if recovery cannot rebuild missing analysis, policy, or eligibility snapshots.
- [x] Fail if workflow can mutate lifecycle state.
- [x] Fail if lifecycle state lacks diagnostics.
- [x] Fail if health reports healthy while evidence contradicts it.
- [x] Certification may inspect, validate, report, and fail.
- [x] Certification must not repair, transfer, retire, create sessions, or change policy.

Backend endpoints:

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/certification`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/certification/report`
- [x] `POST /api/repositories/{repositoryId:guid}/decision-sessions/certification`

Persistence:

- [x] Certification reports under `.agents/decision-sessions/certification/`.
- [x] Markdown reports remain out of scope unless a concrete human-readable audit workflow requires them.

End-to-end fixture:

1. [x] Create session.
2. [x] Activate session.
3. [x] Build governance session analysis.
4. [x] Evaluate lifecycle policy.
5. [x] Evaluate transfer eligibility.
6. [x] Create continuity artifact.
7. [x] Execute transfer when evaluation says `Transfer` and eligibility is `Eligible`.
8. [x] Recover after simulated restart.
9. [x] Project observability.
10. [x] Project workflow consumption.
11. [x] Run certification.

Tests:

- [x] Authority boundary: workflow cannot transfer.
- [x] Single-active-session certification fails on duplicates.
- [x] Analysis determinism passes for identical inputs.
- [x] TTL and cache risk appear in certification evidence.
- [x] Policy determinism passes for identical inputs.
- [x] Eligibility prevents unsafe transfer.
- [x] Transfer preserves continuity through a valid continuity artifact.
- [x] Recovery reconstructs active session and derived snapshots.
- [x] Diagnostics exist for continue, transfer, eligibility blocked, recovery, and failure states.
- [x] Workflow consumes lifecycle correctly.
- [x] End-to-end lifecycle passes.

Exit criteria:

- [x] Certification service exists.
- [x] Certification reports are persisted.
- [x] End-to-end lifecycle fixture passes.
- [x] The system can prove governance continuity survives long horizons, transfer preserves continuity, recovery reconstructs truth, workflow remains a consumer, and at most one active governance session exists.




