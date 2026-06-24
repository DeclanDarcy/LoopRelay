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

- [x] `DecisionSessionTransfer`
- [x] `DecisionSessionTransferEvent`
- [x] `DecisionSessionTransferDiagnostics`
- [x] `DecisionSessionTransferResult`

Add services:

- [x] `IDecisionSessionTransferService`
- [x] `DecisionSessionTransferService`
- [x] `IDecisionSessionContinuityCaptureService`
- [x] `DecisionSessionContinuityCaptureService`
- [x] `IDecisionSessionContinuityIntegrationService`
- [x] `DecisionSessionContinuityIntegrationService`

Transfer flow:

1. [x] Load active session.
2. [x] Require policy evaluation decision `Transfer`.
3. [x] Require transfer eligibility status `Eligible`.
4. [x] Mark source session `TransferPending`.
5. [x] Create and persist `DecisionSessionContinuityArtifact`.
6. [x] Persist transfer started event.
7. [x] Integrate the continuity artifact into existing continuity infrastructure without making decision sessions the owner of operational context.
8. [x] Retire source session.
9. [x] Create replacement session with new identity and inherited repository ownership.
10. [x] Activate replacement session.
11. [x] Update the continuity artifact with target session id if not known at creation time.
12. [x] Mark transfer completed and persist diagnostics.

Invariant rules:

- [x] Source must be active before transfer starts.
- [x] `TransferPending` is allowed during transfer.
- [x] Do not create or activate replacement before source is no longer active.
- [x] Do not allow two active sessions at any point.
- [x] Failed transfer must leave diagnostics, eligibility findings, and enough state for recovery.

[x] Persist transfer events under `.agents/decision-sessions/transfers/`.

### Recovery And Resilience

Add models:

- [x] `DecisionSessionRecoveryResult`
- [x] `DecisionSessionRecoveryFinding`
- [x] `TransferRecoveryAssessment`
- [x] `DecisionSessionRecoveryDiagnostics`
- [x] `DecisionSessionRecoveryHistory`
- [x] `DecisionSessionRecoveryEvent`

Extend:

- [x] `DecisionSessionRecoveryService`

Add hosted service:

- [x] `DecisionSessionRecoveryHostedService`

Recovery responsibilities:

- [x] Load registry.
- [x] Validate active-session count.
- [x] Validate duplicate ids.
- [x] Reconstruct active session from registry, transfer events, continuity artifacts, and continuity evidence.
- [x] Reconstruct transfer history from transfer events, continuity artifacts, and session records.
- [x] Assess interrupted `TransferPending` sessions.
- [x] Rebuild missing metrics, economics, coherence, policy, and eligibility snapshots.
- [x] Persist recovery events, findings, and diagnostics.

Recovery philosophy:

- [x] Decisions, Reasoning, and Continuity evidence outrank derived snapshots.
- [x] Continuity artifacts outrank transfer diagnostics.
- [x] Continuity evidence outranks stale session-state hints when they conflict.
- [x] Do not silently choose one active session when duplicate active sessions exist.
- [x] Repository recovery failures must be isolated to that repository.

Hosted startup behavior:

1. [x] List repositories through `IRepositoryService`.
2. [x] Recover each repository independently.
3. [x] Publish diagnostics.
4. [x] Continue recovering other repositories if one fails.

### Backend Endpoints

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy/diagnostics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility/diagnostics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts/{artifactId}`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/history`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/diagnostics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/history`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/diagnostics`

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
- [x] Transfer decision plus eligible status results in transfer execution.
- [x] Transfer decision plus blocked eligibility does not mutate registry state.
- [x] Continuity artifact is created before source retirement.
- [x] Continuity artifact is the canonical transfer payload and validates required references.
- [x] Source session is retired.
- [x] Replacement session is created and active.
- [x] Two active sessions never exist.
- [x] Transfer events are durable and auditable.
- [x] Active session recovers after restart.
- [x] Completed transfer recovers replacement as active.
- [x] `TransferPending` after restart emits diagnostics.
- [x] Missing analysis, policy, and eligibility snapshots are rebuilt.
- [x] Duplicate active sessions produce a recovery finding.
- [x] Hosted recovery isolates repository failures.

### Exit Criteria

- [x] Policy can decide continue or transfer.
- [x] Eligibility can block or defer transfer without changing policy.
- [x] Transfer creates a first-class continuity artifact.
- [x] Transfer preserves continuity and never creates parallel active sessions.
- [x] Recovery survives restart, missing snapshots, duplicate-active corruption, and interrupted transfer states with diagnostics.




