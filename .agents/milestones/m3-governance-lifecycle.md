# Milestone 3: Governance Lifecycle

Objective: decide whether the active governance session should continue or transfer, verify transfer readiness, execute transfer safely, and recover lifecycle state.

### Lifecycle Policy

Add primitives and models:

- [ ] `DecisionSessionLifecycleDecision`: `Continue`, `Transfer`
- [ ] `DecisionSessionLifecycleEvaluation`
- [ ] `ReuseScoreAssessment`
- [ ] `TransferScoreAssessment`
- [ ] `DecisionSessionLifecycleDiagnostics`
- [ ] `DecisionSessionLifecycleSnapshot`
- [ ] `DecisionSessionLifecyclePolicyOptions`

Lifecycle evaluation fields:

- [ ] `DecisionSessionLifecycleDecision Decision`
- [ ] `decimal ReuseScore`
- [ ] `decimal TransferScore`
- [ ] `string Reason`
- [ ] `IReadOnlyList<string> ContributingFactors`
- [ ] `DateTimeOffset EvaluatedAt`

Add service:

- [ ] `IDecisionSessionLifecyclePolicy`
- [ ] `DecisionSessionLifecyclePolicy`

Inputs:

- [ ] `DecisionSession`
- [ ] `DecisionSessionMetrics`
- [ ] `DecisionSessionStatistics`
- [ ] `DecisionSessionCacheMetrics`
- [ ] `DecisionSessionEconomics`
- [ ] `DecisionSessionCoherence`

Deterministic policy:

- [ ] Reuse score grows with estimated reuse value, cache benefit, continuity benefit, and coherence score.
- [ ] Transfer score grows with estimated transfer value, transfer pressure, fragmentation, growth, and cache miss risk.
- [ ] If `ReuseScore > TransferScore`, decide `Continue`.
- [ ] If `TransferScore > ReuseScore`, decide `Transfer`.
- [ ] If scores are equal, decide `Continue` to avoid churn.
- [ ] Same inputs must always produce the same evaluation.

Persist lifecycle snapshots under `.agents/decision-sessions/lifecycle/policy/`.

### Transfer Eligibility

Transfer eligibility is the operational gate between policy and transfer execution. It prevents policy from knowing operational details while preventing transfer from starting when required continuity or repository evidence is unavailable.

Add models:

- [ ] `DecisionSessionTransferEligibility`
- [ ] `DecisionSessionTransferEligibilityStatus`: `NotApplicable`, `Eligible`, `Blocked`, `Deferred`
- [ ] `DecisionSessionTransferEligibilityFinding`
- [ ] `DecisionSessionTransferEligibilityDiagnostics`

Add service:

- [ ] `IDecisionSessionTransferEligibilityService`
- [ ] `DecisionSessionTransferEligibilityService`

Eligibility inputs:

- [ ] Active session.
- [ ] Lifecycle policy evaluation.
- [ ] Registry validation.
- [ ] Repository availability.
- [ ] Transfer-pending state.
- [ ] Continuity evidence availability.
- [ ] Operational context availability.
- [ ] Ability to create a continuity artifact.
- [ ] Recovery findings.

Eligibility rules:

- [ ] If policy decision is `Continue`, eligibility is `NotApplicable`.
- [ ] If no active session exists, eligibility is `Blocked`.
- [ ] If registry has duplicate active sessions, eligibility is `Blocked`.
- [ ] If source session is already `TransferPending`, eligibility is `Deferred` unless recovery can prove the prior transfer failed safely.
- [ ] If operational context evidence is unavailable, eligibility is `Blocked`.
- [ ] If continuity artifact generation cannot produce a valid artifact, eligibility is `Blocked`.
- [ ] If repository state is unavailable or locked, eligibility is `Deferred` or `Blocked` with diagnostics.
- [ ] If unresolved recovery findings threaten continuity, eligibility is `Blocked`.
- [ ] If all preconditions pass and policy decision is `Transfer`, eligibility is `Eligible`.

Persist eligibility checks under `.agents/decision-sessions/lifecycle/eligibility/`.

### Continuity Artifact

Add a first-class canonical transfer payload:

- [ ] `DecisionSessionContinuityArtifact`

This artifact is the durable governance-continuity payload transferred between source and replacement decision sessions. It is not merely a diagnostic snapshot.

Fields:

- [ ] `string ArtifactId`
- [ ] `Guid RepositoryId`
- [ ] `DecisionSessionId SourceSessionId`
- [ ] `DecisionSessionId? TargetSessionId`
- [ ] `DateTimeOffset CreatedAt`
- [ ] `DecisionSessionLifecycleEvaluation PolicyEvaluation`
- [ ] `DecisionSessionMetrics Metrics`
- [ ] `DecisionSessionEconomics Economics`
- [ ] `DecisionSessionCoherence Coherence`
- [ ] `DecisionSessionCacheMetrics Cache`
- [ ] `IReadOnlyList<DecisionSessionContinuityReference> DecisionReferences`
- [ ] `IReadOnlyList<DecisionSessionContinuityReference> ReasoningReferences`
- [ ] `IReadOnlyList<DecisionSessionContinuityReference> OperationalContextReferences`
- [ ] `string ContinuityFingerprint`
- [ ] `IReadOnlyList<string> Diagnostics`

Add:

- [ ] `DecisionSessionContinuityReference`
- [ ] `DecisionSessionContinuityArtifactValidation`
- [ ] `IDecisionSessionContinuityArtifactService`
- [ ] `DecisionSessionContinuityArtifactService`

Persistence:

- [ ] Store artifacts under `.agents/decision-sessions/continuity-artifacts/`.
- [ ] Use deterministic artifact ids such as `continuity.YYYYMMDDTHHMMSS.fffffffZ.<source-session-id>.json`.
- [ ] Store a markdown projection only if useful for human diagnostics.
- [ ] Validate repository id, source session id, fingerprint, evidence references, and schema version on read.

Continuity artifact rules:

- [ ] The artifact is the canonical payload for transfer.
- [ ] It is durable, recoverable, and auditable.
- [ ] It records what continuity is being carried forward, not who owns operational context.
- [ ] Decision Sessions must never own Operational Context. They contribute transfer artifacts that continuity services may consume.

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

- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy/diagnostics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility/diagnostics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts/{artifactId}`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/history`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/diagnostics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/history`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/diagnostics`

Do not add a manual transfer endpoint.

### Tests

- [ ] Same inputs produce same policy decision.
- [ ] Reuse score greater than transfer score decides `Continue`.
- [ ] Transfer score greater than reuse score decides `Transfer`.
- [ ] Equal scores decide `Continue`.
- [ ] Higher cache miss risk raises transfer score.
- [ ] Eligibility is `NotApplicable` when policy is `Continue`.
- [ ] Eligibility is `Blocked` when no active session exists.
- [ ] Eligibility is `Blocked` for duplicate active sessions.
- [ ] Eligibility is `Blocked` when continuity artifact generation fails.
- [ ] Eligibility is `Blocked` when operational context evidence is unavailable.
- [ ] Eligibility is `Deferred` or `Blocked` when repository state is unavailable or locked.
- [ ] Transfer decision plus eligible status results in transfer execution.
- [ ] Transfer decision plus blocked eligibility does not mutate registry state.
- [ ] Continuity artifact is created before source retirement.
- [ ] Continuity artifact is the canonical transfer payload and validates required references.
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

- [ ] Policy can decide continue or transfer.
- [ ] Eligibility can block or defer transfer without changing policy.
- [ ] Transfer creates a first-class continuity artifact.
- [ ] Transfer preserves continuity and never creates parallel active sessions.
- [ ] Recovery survives restart, missing snapshots, duplicate-active corruption, and interrupted transfer states with diagnostics.




