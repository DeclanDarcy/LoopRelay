# Milestone 3: Governance Lifecycle

Objective: decide whether the active governance session should continue or transfer, verify transfer readiness, execute transfer safely, and recover lifecycle state.

### Lifecycle Policy

Add primitives and models:

- [x] `DecisionSessionLifecycleDecision`: `Continue`, `Transfer`
- [x] `DecisionSessionLifecycleEvaluation`
- [x] `ReuseScoreAssessment`
- [x] `TransferScoreAssessment`
- [x] `DecisionSessionLifecycleDiagnostics`
- [x] `DecisionSessionLifecycleSnapshot`
- [x] `DecisionSessionLifecyclePolicyOptions`

Lifecycle evaluation fields:

- [x] `DecisionSessionLifecycleDecision Decision`
- [x] `decimal ReuseScore`
- [x] `decimal TransferScore`
- [x] `string Reason`
- [x] `IReadOnlyList<string> ContributingFactors`
- [x] `DateTimeOffset EvaluatedAt`

Add service:

- [x] `IDecisionSessionLifecyclePolicy`
- [x] `DecisionSessionLifecyclePolicy`

Inputs:

- [x] `DecisionSession`
- [x] `DecisionSessionMetrics`
- [x] `DecisionSessionStatistics`
- [x] `DecisionSessionCacheMetrics`
- [x] `DecisionSessionEconomics`
- [x] `DecisionSessionCoherence`

Deterministic policy:

- [x] Reuse score grows with estimated reuse value, cache benefit, continuity benefit, and coherence score.
- [x] Transfer score grows with estimated transfer value, transfer pressure, fragmentation, growth, and cache miss risk.
- [x] If `ReuseScore > TransferScore`, decide `Continue`.
- [x] If `TransferScore > ReuseScore`, decide `Transfer`.
- [x] If scores are equal, decide `Continue` to avoid churn.
- [x] Same inputs must always produce the same evaluation.

[x] Persist lifecycle snapshots under `.agents/decision-sessions/lifecycle/policy/`.

### Transfer Eligibility

Transfer eligibility is the operational gate between policy and transfer execution. It prevents policy from knowing operational details while preventing transfer from starting when required continuity or repository evidence is unavailable.

Add models:

- [x] `DecisionSessionTransferEligibility`
- [x] `DecisionSessionTransferEligibilityStatus`: `NotApplicable`, `Eligible`, `Blocked`, `Deferred`
- [x] `DecisionSessionTransferEligibilityFinding`
- [x] `DecisionSessionTransferEligibilityDiagnostics`

Add service:

- [x] `IDecisionSessionTransferEligibilityService`
- [x] `DecisionSessionTransferEligibilityService`

Eligibility inputs:

- [x] Active session.
- [x] Lifecycle policy evaluation.
- [x] Registry validation.
- [x] Repository availability.
- [x] Transfer-pending state.
- [x] Continuity evidence availability.
- [x] Operational context availability.
- [x] Ability to create a continuity artifact.
- [x] Recovery findings.

Eligibility rules:

- [x] If policy decision is `Continue`, eligibility is `NotApplicable`.
- [x] If no active session exists, eligibility is `Blocked`.
- [x] If registry has duplicate active sessions, eligibility is `Blocked`.
- [x] If source session is already `TransferPending`, eligibility is `Deferred` unless recovery can prove the prior transfer failed safely.
- [x] If operational context evidence is unavailable, eligibility is `Blocked`.
- [x] If continuity artifact generation cannot produce a valid artifact, eligibility is `Blocked`.
- [x] If repository state is unavailable or locked, eligibility is `Deferred` or `Blocked` with diagnostics.
- [x] If unresolved recovery findings threaten continuity, eligibility is `Blocked`.
- [x] If all preconditions pass and policy decision is `Transfer`, eligibility is `Eligible`.

[x] Persist eligibility checks under `.agents/decision-sessions/lifecycle/eligibility/`.

### Continuity Artifact

Add a first-class canonical transfer payload:

- [x] `DecisionSessionContinuityArtifact`

This artifact is the durable governance-continuity payload transferred between source and replacement decision sessions. It is not merely a diagnostic snapshot.

Fields:

- [x] `string ArtifactId`
- [x] `Guid RepositoryId`
- [x] `DecisionSessionId SourceSessionId`
- [x] `DecisionSessionId? TargetSessionId`
- [x] `DateTimeOffset CreatedAt`
- [x] `DecisionSessionLifecycleEvaluation PolicyEvaluation`
- [x] `DecisionSessionMetrics Metrics`
- [x] `DecisionSessionEconomics Economics`
- [x] `DecisionSessionCoherence Coherence`
- [x] `DecisionSessionCacheMetrics Cache`
- [x] `IReadOnlyList<DecisionSessionContinuityReference> DecisionReferences`
- [x] `IReadOnlyList<DecisionSessionContinuityReference> ReasoningReferences`
- [x] `IReadOnlyList<DecisionSessionContinuityReference> OperationalContextReferences`
- [x] `string ContinuityFingerprint`
- [x] `IReadOnlyList<string> Diagnostics`

Add:

- [x] `DecisionSessionContinuityReference`
- [x] `DecisionSessionContinuityArtifactValidation`
- [x] `IDecisionSessionContinuityArtifactService`
- [x] `DecisionSessionContinuityArtifactService`

Persistence:

- [x] Store artifacts under `.agents/decision-sessions/continuity-artifacts/`.
- [x] Use deterministic artifact ids such as `continuity.YYYYMMDDTHHMMSS.fffffffZ.<source-session-id>.json`.
- [x] Store a markdown projection only if useful for human diagnostics.
- [x] Validate repository id, source session id, fingerprint, evidence references, and schema version on read.

Continuity artifact rules:

- [x] The artifact is the canonical payload for transfer.
- [x] It is durable, recoverable, and auditable.
- [x] It records what continuity is being carried forward, not who owns operational context.
- [x] Decision Sessions must never own Operational Context. They contribute transfer artifacts that continuity services may consume.

### Transfer Execution

Add models:

- [ ] `DecisionSessionTransfer`
- [ ] `DecisionSessionTransferEvent`
- [ ] `DecisionSessionTransferDiagnostics`
- [ ] `DecisionSessionTransferResult`

Add services:

- [ ] `IDecisionSessionTransferService`
- [ ] `DecisionSessionTransferService`
- [ ] `IDecisionSessionContinuityCaptureService`
- [ ] `DecisionSessionContinuityCaptureService`
- [ ] `IDecisionSessionContinuityIntegrationService`
- [ ] `DecisionSessionContinuityIntegrationService`

Transfer flow:

1. [ ] Load active session.
2. [ ] Require policy evaluation decision `Transfer`.
3. [ ] Require transfer eligibility status `Eligible`.
4. [ ] Mark source session `TransferPending`.
5. [ ] Create and persist `DecisionSessionContinuityArtifact`.
6. [ ] Persist transfer started event.
7. [ ] Integrate the continuity artifact into existing continuity infrastructure without making decision sessions the owner of operational context.
8. [ ] Retire source session.
9. [ ] Create replacement session with new identity and inherited repository ownership.
10. [ ] Activate replacement session.
11. [ ] Update the continuity artifact with target session id if not known at creation time.
12. [ ] Mark transfer completed and persist diagnostics.

Invariant rules:

- [ ] Source must be active before transfer starts.
- [ ] `TransferPending` is allowed during transfer.
- [ ] Do not create or activate replacement before source is no longer active.
- [ ] Do not allow two active sessions at any point.
- [ ] Failed transfer must leave diagnostics, eligibility findings, and enough state for recovery.

Persist transfer events under `.agents/decision-sessions/transfers/`.

### Recovery And Resilience

Add models:

- [ ] `DecisionSessionRecoveryResult`
- [ ] `DecisionSessionRecoveryFinding`
- [ ] `TransferRecoveryAssessment`
- [ ] `DecisionSessionRecoveryDiagnostics`
- [ ] `DecisionSessionRecoveryHistory`
- [ ] `DecisionSessionRecoveryEvent`

Extend:

- [ ] `DecisionSessionRecoveryService`

Add hosted service:

- [ ] `DecisionSessionRecoveryHostedService`

Recovery responsibilities:

- [ ] Load registry.
- [ ] Validate active-session count.
- [ ] Validate duplicate ids.
- [ ] Reconstruct active session from registry, transfer events, continuity artifacts, and continuity evidence.
- [ ] Reconstruct transfer history from transfer events, continuity artifacts, and session records.
- [ ] Assess interrupted `TransferPending` sessions.
- [ ] Rebuild missing metrics, economics, coherence, policy, and eligibility snapshots.
- [ ] Persist recovery events, findings, and diagnostics.

Recovery philosophy:

- [ ] Decisions, Reasoning, and Continuity evidence outrank derived snapshots.
- [ ] Continuity artifacts outrank transfer diagnostics.
- [ ] Continuity evidence outranks stale session-state hints when they conflict.
- [ ] Do not silently choose one active session when duplicate active sessions exist.
- [ ] Repository recovery failures must be isolated to that repository.

Hosted startup behavior:

1. [ ] List repositories through `IRepositoryService`.
2. [ ] Recover each repository independently.
3. [ ] Publish diagnostics.
4. [ ] Continue recovering other repositories if one fails.

### Backend Endpoints

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy/diagnostics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility/diagnostics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts/{artifactId}`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/history`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/diagnostics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/history`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/diagnostics`

Do not add a manual transfer endpoint.

### Tests

- [x] Same inputs produce same policy decision.
- [x] Reuse score greater than transfer score decides `Continue`.
- [x] Transfer score greater than reuse score decides `Transfer`.
- [x] Equal scores decide `Continue`.
- [x] Higher cache miss risk raises transfer score.
- [x] Eligibility is `NotApplicable` when policy is `Continue`.
- [x] Eligibility is `Blocked` when no active session exists.
- [x] Eligibility is `Blocked` for duplicate active sessions.
- [x] Eligibility is `Blocked` when continuity artifact generation fails.
- [x] Eligibility is `Blocked` when operational context evidence is unavailable.
- [x] Eligibility is `Deferred` or `Blocked` when repository state is unavailable or locked.
- [ ] Transfer decision plus eligible status results in transfer execution.
- [ ] Transfer decision plus blocked eligibility does not mutate registry state.
- [ ] Continuity artifact is created before source retirement.
- [x] Continuity artifact is the canonical transfer payload and validates required references.
- [ ] Source session is retired.
- [ ] Replacement session is created and active.
- [ ] Two active sessions never exist.
- [ ] Transfer events are durable and auditable.
- [ ] Active session recovers after restart.
- [ ] Completed transfer recovers replacement as active.
- [ ] `TransferPending` after restart emits diagnostics.
- [ ] Missing analysis, policy, and eligibility snapshots are rebuilt.
- [ ] Duplicate active sessions produce a recovery finding.
- [ ] Hosted recovery isolates repository failures.

### Exit Criteria

- [x] Policy can decide continue or transfer.
- [x] Eligibility can block or defer transfer without changing policy.
- [ ] Transfer creates a first-class continuity artifact.
- [ ] Transfer preserves continuity and never creates parallel active sessions.
- [ ] Recovery survives restart, missing snapshots, duplicate-active corruption, and interrupted transfer states with diagnostics.




