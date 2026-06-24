# Milestone 4: Lifecycle Observability

Objective: expose read-only lifecycle visibility and explanations without adding new lifecycle behavior.

Add models:

- [ ] `DecisionSessionLifecycleProjection`
- [ ] `DecisionSessionLifecycleHistory`
- [ ] `DecisionSessionInfluenceTrace`
- [ ] `DecisionSessionTransferEventProjection`
- [ ] `DecisionSessionContinuityArtifactProjection`
- [ ] `DecisionSessionSizeProjection`
- [ ] `DecisionSessionHealthAssessment`
- [ ] `DecisionSessionHealthDimension`

Add service:

- [ ] `IDecisionSessionObservabilityService`
- [ ] `DecisionSessionObservabilityService`

Projection composition:

- [ ] Current session projection.
- [ ] Current metrics.
- [ ] Current economics.
- [ ] Current coherence.
- [ ] Current policy evaluation.
- [ ] Current transfer eligibility.
- [ ] Current continuity artifact, if transfer is pending or completed.
- [ ] Recent transfer events.
- [ ] Recent recovery events.
- [ ] Diagnostics.

History events:

- [ ] Created
- [ ] Activated
- [ ] AnalysisCaptured
- [ ] PolicyEvaluated
- [ ] TransferEligibilityEvaluated
- [ ] ContinuityArtifactCreated
- [ ] TransferStarted
- [ ] TransferCompleted
- [ ] Retired
- [ ] ReplacementCreated
- [ ] Recovered

Influence trace categories:

- [ ] Metrics
- [ ] Cache TTL
- [ ] Cache miss risk
- [ ] Economics
- [ ] Coherence
- [ ] Policy
- [ ] Eligibility
- [ ] Continuity artifact
- [ ] Transfer
- [ ] Recovery

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

- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/projection`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/history`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/influence`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/health`

Persistence:

- [ ] Observability snapshots may be stored under `.agents/decision-sessions/observability/`.
- [ ] Observability is derived, disposable, and rebuildable.

Tests:

- [ ] Projection composes session, analysis, policy, eligibility, continuity artifact, transfer, and recovery.
- [ ] History reconstructs creation, activation, policy, eligibility, continuity artifact creation, transfer, retirement, and recovery.
- [ ] Influence trace contains economics, coherence, TTL, cache risk, and eligibility signals.
- [ ] Transfer event projection includes source, target, reason, token size, reuse score, transfer score, and eligibility status.
- [ ] Continuity artifact projection includes canonical artifact id, fingerprint, source session, target session, and evidence references.
- [ ] Size projection exposes token, context, reasoning, and measured-at values.
- [ ] Health dimensions report each subsystem independently.
- [ ] Observability never mutates registry, transfer, eligibility, or policy state.
- [ ] Missing observability snapshot is rebuilt.

Exit criteria:

- [ ] The system explains current lifecycle state, why policy chose continue or transfer, whether transfer is eligible, how large the session is, when transfer happened, which artifact carried continuity, why transfer happened, and whether recovery was required.
- [ ] No manual lifecycle controls are introduced.




