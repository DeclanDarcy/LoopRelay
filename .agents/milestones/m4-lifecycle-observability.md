# Milestone 4: Lifecycle Observability

Objective: expose read-only lifecycle visibility and explanations without adding new lifecycle behavior.

Add models:

- [x] `DecisionSessionLifecycleProjection`
- [x] `DecisionSessionLifecycleHistory`
- [x] `DecisionSessionInfluenceTrace`
- [ ] `DecisionSessionTransferEventProjection`
- [ ] `DecisionSessionContinuityArtifactProjection`
- [ ] `DecisionSessionSizeProjection`
- [ ] `DecisionSessionHealthAssessment`
- [ ] `DecisionSessionHealthDimension`

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

- [ ] Registry
- [ ] Analysis
- [ ] Policy
- [ ] Eligibility
- [ ] Continuity artifact
- [ ] Transfer
- [ ] Recovery

Health must remain decomposed. Do not hide state in a single opaque score.

Backend endpoints:

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/projection`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/history`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/influence`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/health`

Persistence:

- [ ] Observability snapshots may be stored under `.agents/decision-sessions/observability/`.
- [ ] Observability is derived, disposable, and rebuildable.

Tests:

- [x] Projection composes session, analysis, policy, eligibility, continuity artifact, transfer, and recovery.
- [x] History reconstructs creation, activation, policy, eligibility, continuity artifact creation, transfer, retirement, and recovery.
- [x] Influence trace contains economics, coherence, TTL, cache risk, and eligibility signals.
- [ ] Transfer event projection includes source, target, reason, token size, reuse score, transfer score, and eligibility status.
- [ ] Continuity artifact projection includes canonical artifact id, fingerprint, source session, target session, and evidence references.
- [ ] Size projection exposes token, context, reasoning, and measured-at values.
- [ ] Health dimensions report each subsystem independently.
- [x] Observability never mutates registry, transfer, eligibility, or policy state.
- [ ] Missing observability snapshot is rebuilt.

Exit criteria:

- [ ] The system explains current lifecycle state, why policy chose continue or transfer, whether transfer is eligible, how large the session is, when transfer happened, which artifact carried continuity, why transfer happened, and whether recovery was required.
- [ ] No manual lifecycle controls are introduced.




