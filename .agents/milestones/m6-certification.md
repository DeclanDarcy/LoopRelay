# Milestone 6: Certification

Objective: prove lifecycle correctness without adding new lifecycle behavior.

Add certification models:

- [ ] `DecisionSessionCertificationResult`
- [ ] `DecisionSessionCertificationFinding`
- [ ] `DecisionSessionCertificationReport`
- [ ] `DecisionSessionGovernanceReport`
- [ ] `DecisionSessionHealthReport`
- [ ] `DecisionSessionLifecycleEndToEndFixture`

Add certification service:

- [ ] `IDecisionSessionCertificationService`
- [ ] `DecisionSessionCertificationService`

Certification categories:

- [ ] Authority
- [ ] Single active session
- [ ] Analysis determinism
- [ ] TTL and cache risk
- [ ] Policy determinism
- [ ] Transfer eligibility
- [ ] Continuity artifact
- [ ] Transfer
- [ ] Recovery
- [ ] Continuity
- [ ] Workflow integration
- [ ] Diagnostics
- [ ] Health

Certification rules:

- [ ] Fail if more than one active session exists.
- [ ] Fail if analysis is non-deterministic for identical inputs.
- [ ] Fail if TTL or cache miss risk is missing from analysis.
- [ ] Fail if policy is non-deterministic for identical inputs.
- [ ] Fail if transfer executes while eligibility is blocked or deferred.
- [ ] Fail if transfer lacks a valid continuity artifact.
- [ ] Fail if transfer lacks source session retirement, replacement session activation, or continuity evidence.
- [ ] Fail if recovery cannot rebuild missing analysis, policy, or eligibility snapshots.
- [ ] Fail if workflow can mutate lifecycle state.
- [ ] Fail if lifecycle state lacks diagnostics.
- [ ] Fail if health reports healthy while evidence contradicts it.
- [ ] Certification may inspect, validate, report, and fail.
- [ ] Certification must not repair, transfer, retire, create sessions, or change policy.

Backend endpoints:

- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/certification`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/certification/report`
- [ ] `POST /api/repositories/{repositoryId:guid}/decision-sessions/certification`

Persistence:

- [ ] Certification reports under `.agents/decision-sessions/certification/`.
- [ ] Optional markdown reports under `.agents/decision-sessions/reports/`.

End-to-end fixture:

1. [ ] Create session.
2. [ ] Activate session.
3. [ ] Build governance session analysis.
4. [ ] Evaluate lifecycle policy.
5. [ ] Evaluate transfer eligibility.
6. [ ] Create continuity artifact.
7. [ ] Execute transfer when evaluation says `Transfer` and eligibility is `Eligible`.
8. [ ] Recover after simulated restart.
9. [ ] Project observability.
10. [ ] Project workflow consumption.
11. [ ] Run certification.

Tests:

- [ ] Authority boundary: workflow cannot transfer.
- [ ] Single-active-session certification fails on duplicates.
- [ ] Analysis determinism passes for identical inputs.
- [ ] TTL and cache risk appear in certification evidence.
- [ ] Policy determinism passes for identical inputs.
- [ ] Eligibility prevents unsafe transfer.
- [ ] Transfer preserves continuity through a valid continuity artifact.
- [ ] Recovery reconstructs active session and derived snapshots.
- [ ] Diagnostics exist for continue, transfer, eligibility blocked, recovery, and failure states.
- [ ] Workflow consumes lifecycle correctly.
- [ ] End-to-end lifecycle passes.

Exit criteria:

- [ ] Certification service exists.
- [ ] Certification reports are persisted.
- [ ] End-to-end lifecycle fixture passes.
- [ ] The system can prove governance continuity survives long horizons, transfer preserves continuity, recovery reconstructs truth, workflow remains a consumer, and at most one active governance session exists.




