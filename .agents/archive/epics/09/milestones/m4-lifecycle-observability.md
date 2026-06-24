# Milestone 4: Lifecycle Observability

Objective: expose read-only lifecycle visibility and explanations without adding new lifecycle behavior.

Add models:

- [x] `DecisionSessionLifecycleProjection`
- [x] `DecisionSessionLifecycleHistory`
- [x] `DecisionSessionInfluenceTrace`
- [x] `DecisionSessionTransferEventProjection`
- [x] `DecisionSessionContinuityArtifactProjection`
- [x] `DecisionSessionSizeProjection`
- [x] `DecisionSessionHealthAssessment`
- [x] `DecisionSessionHealthDimension`

Add service:

- [x] `IDecisionSessionObservabilityService`
- [x] `DecisionSessionObservabilityService`

Projection composition:

- [x] Current session projection.
- [x] Current metrics.
- [x] Current economics.
- [x] Current coherence.
- [x] Current policy evaluation.
- [x] Current transfer eligibility.
- [x] Current continuity artifact, if transfer is pending or completed.
- [x] Recent transfer events.
- [x] Recent recovery events.
- [x] Diagnostics.

History events:

- [x] Created
- [x] Activated
- [x] AnalysisCaptured
- [x] PolicyEvaluated
- [x] TransferEligibilityEvaluated
- [x] ContinuityArtifactCreated
- [x] TransferStarted
- [x] TransferCompleted
- [x] Retired
- [x] ReplacementCreated
- [x] Recovered

Influence trace categories:

- [x] Metrics
- [x] Cache TTL
- [x] Cache miss risk
- [x] Economics
- [x] Coherence
- [x] Policy
- [x] Eligibility
- [x] Continuity artifact
- [x] Transfer
- [x] Recovery

Health dimensions:

- [x] Registry
- [x] Analysis
- [x] Policy
- [x] Eligibility
- [x] Continuity artifact
- [x] Transfer
- [x] Recovery

Health must remain decomposed. Do not hide state in a single opaque score.

Backend endpoints:

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/projection`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/history`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/influence`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/health`

Persistence:

- [x] Observability snapshots are optional and not required for lifecycle authority.
- [x] Observability is derived, disposable, and rebuildable.

Tests:

- [x] Projection composes session, analysis, policy, eligibility, continuity artifact, transfer, and recovery.
- [x] History reconstructs creation, activation, policy, eligibility, continuity artifact creation, transfer, retirement, and recovery.
- [x] Influence trace contains economics, coherence, TTL, cache risk, and eligibility signals.
- [x] Transfer event projection includes source, target, reason, token size, reuse score, transfer score, and eligibility status.
- [x] Continuity artifact projection includes canonical artifact id, fingerprint, source session, target session, and evidence references.
- [x] Size projection exposes token, context, reasoning, and measured-at values.
- [x] Health dimensions report each subsystem independently.
- [x] Observability never mutates registry, transfer, eligibility, or policy state.
- [x] Missing observability snapshot does not block rebuild from authoritative evidence.

Exit criteria:

- [x] The system explains current lifecycle state, why policy chose continue or transfer, whether transfer is eligible, how large the session is, when transfer happened, which artifact carried continuity, why transfer happened, and whether recovery was required.
- [x] No manual lifecycle controls are introduced.




