# Milestone 2: Governance Session Analysis

Objective: measure session facts, evaluate lifecycle economics, and assess reasoning coherence as one capability chain. Analysis answers what is true and what the signals imply. It does not make lifecycle decisions or execute transfer.

Stage 2 is one architectural stage, but it should be implemented through three internal checkpoints:

- [ ] Stage 2A: metrics, statistics, TTL, and cache risk.
- [ ] Stage 2B: economics.
- [ ] Stage 2C: coherence.

Do not start lifecycle policy until all three checkpoints are complete and the aggregate analysis diagnostics are stable.

### Stage 2A: Metrics, Statistics, TTL, And Cache Risk

Add models:

- [ ] `DecisionSessionMetrics`
- [ ] `DecisionSessionStatistics`
- [ ] `DecisionSessionActivity`
- [ ] `DecisionSessionGrowth`
- [ ] `DecisionSessionMetricsDiagnostics`
- [ ] `DecisionSessionMetricsSnapshot`
- [ ] `DecisionSessionCacheMetrics`

Metrics fields:

- [ ] `long EstimatedTokenCount`
- [ ] `long ContextByteSize`
- [ ] `long ReasoningEventCount`
- [ ] `long ReasoningThreadCount`
- [ ] `long ReasoningRelationshipCount`
- [ ] `long DecisionCount`
- [ ] `long DecisionCandidateCount`
- [ ] `long DecisionProposalCount`
- [ ] `long OperationalContextRevisionCount`
- [ ] `DateTimeOffset LastActivityAt`
- [ ] `DateTimeOffset MeasuredAt`

Statistics and cache fields:

- [ ] `TimeSpan SessionAge`
- [ ] `TimeSpan SessionElapsedDuration`
- [ ] `TimeSpan IdleDuration`
- [ ] `decimal GrowthRate`
- [ ] `decimal ActivityRate`
- [ ] `TimeSpan EstimatedCacheTtl`
- [ ] `decimal EstimatedCacheMissRisk`
- [ ] `DateTimeOffset? EstimatedCacheExpiresAt`

Add services:

- [ ] `ITokenEstimator`
- [ ] `IDecisionSessionMetricsService`
- [ ] `DecisionSessionMetricsService`
- [ ] `DecisionSessionEvidenceReader`

Evidence sources:

- [ ] `IDecisionRepository.ListDecisionsAsync`
- [ ] `IDecisionRepository.ListCandidatesAsync`
- [ ] `IDecisionRepository.ListProposalsAsync`
- [ ] `IReasoningRepository.ListEventsAsync`
- [ ] `IReasoningRepository.ListThreadsAsync`
- [ ] `IReasoningRepository.ListRelationshipsAsync`
- [ ] `IOperationalContextProposalStore.ListAsync`
- [ ] `IArtifactService` and `IArtifactStore` for current and historical operational context content.

Token and TTL estimation:

- [ ] Token estimation must be deterministic and provider-independent.
- [ ] Estimate tokens by text length using a stable character-to-token ratio, for example `(characterCount + 3) / 4`.
- [ ] Estimate cache TTL and cache miss risk from session elapsed duration, idle duration, and configurable assumptions.
- [ ] Include diagnostics describing each source, byte count, character count, TTL assumption, cache risk, and confidence.

Stage 2A checkpoint tests:

- [ ] Metrics generated from decisions, reasoning, and operational context evidence.
- [ ] Same inputs produce same metrics and statistics.
- [ ] Token estimator is deterministic.
- [ ] TTL and cache miss risk increase with elapsed and idle duration.
- [ ] Activity increases when reasoning, decision, or context evidence increases.
- [ ] Growth reflects larger continuity evidence.
- [ ] Missing metrics snapshots are rebuilt.
- [ ] Diagnostics explain sources, TTL assumptions, cache risk, and missing evidence.

Stage 2A exit criteria:

- [ ] The system can answer session size, age, elapsed duration, idle duration, TTL, cache miss risk, activity, and growth.
- [ ] Metrics and statistics are rebuildable from authoritative evidence.
- [ ] No economics, coherence, policy, eligibility, or transfer behavior exists yet.

### Stage 2B: Economics

Add models:

- [ ] `DecisionSessionEconomics`
- [ ] `DecisionSessionEconomicsInputs`
- [ ] `ReuseValueAssessment`
- [ ] `TransferValueAssessment`
- [ ] `CacheBenefitAssessment`
- [ ] `CacheRiskAssessment`
- [ ] `ContinuityBenefitAssessment`
- [ ] `DecisionSessionEconomicsDiagnostics`
- [ ] `DecisionSessionEconomicsSnapshot`
- [ ] `DecisionSessionEconomicsOptions`

Economics fields:

- [ ] `decimal EstimatedReuseValue`
- [ ] `decimal EstimatedTransferValue`
- [ ] `decimal EstimatedContextCost`
- [ ] `decimal EstimatedReasoningCost`
- [ ] `decimal EstimatedContinuityBenefit`
- [ ] `decimal EstimatedCacheBenefit`
- [ ] `decimal EstimatedCacheMissRisk`

Add services:

- [ ] `IDecisionSessionEconomicsService`
- [ ] `DecisionSessionEconomicsService`

Initial deterministic model:

- [ ] Normalize values to `0.0m` through `1.0m` where useful.
- [ ] Context cost grows with estimated tokens and context bytes.
- [ ] Reasoning cost grows with reasoning event count, thread count, and relationship count.
- [ ] Continuity benefit grows with decision count, reasoning density, and operational context revisions.
- [ ] Cache benefit uses configurable assumptions such as cached-token cost factor `0.10m`.
- [ ] Cache risk grows as elapsed duration approaches or exceeds estimated TTL.
- [ ] Transfer value grows with growth rate, idle duration, cache miss risk, and large context cost.
- [ ] Reuse value grows with continuity benefit, cache benefit, coherence, and recent activity.

Stage 2B checkpoint tests:

- [ ] Same inputs produce same economics.
- [ ] More continuity increases reuse value.
- [ ] Higher cache miss risk increases transfer value.
- [ ] Larger reusable corpus increases cache benefit.
- [ ] Missing economics snapshots are rebuilt.
- [ ] Diagnostics explain inputs, assumptions, missing evidence, TTL, and cache risk.

Stage 2B exit criteria:

- [ ] The system can answer reuse value, transfer value, cache benefit, cache risk, context cost, reasoning cost, and continuity benefit.
- [ ] Economics is rebuildable from metrics, statistics, TTL/cache inputs, and domain evidence.
- [ ] Economics remains analysis only and cannot make lifecycle decisions.

### Stage 2C: Coherence

Add models:

- [ ] `DecisionSessionCoherence`
- [ ] `DecisionSessionCoherenceInputs`
- [ ] `FragmentationAssessment`
- [ ] `DensityAssessment`
- [ ] `ContinuityQualityAssessment`
- [ ] `TransferPressureAssessment`
- [ ] `DecisionSessionCoherenceDiagnostics`
- [ ] `DecisionSessionCoherenceSnapshot`

Coherence fields:

- [ ] `decimal CoherenceScore`
- [ ] `decimal FragmentationScore`
- [ ] `decimal DensityScore`
- [ ] `decimal ContinuityScore`
- [ ] `decimal TransferPressure`

Add services:

- [ ] `IDecisionSessionCoherenceService`
- [ ] `DecisionSessionCoherenceService`

Evidence sources:

- [ ] `IReasoningRepository` for events, threads, relationships.
- [ ] `IReasoningGraphService.GetGraphAsync` for nodes and graph relationships.
- [ ] `IDecisionRepository` for decision and proposal counts.
- [ ] Continuity evidence for operational context revision counts.

Initial deterministic model:

- [ ] Density score increases with relationship count relative to node count.
- [ ] Fragmentation score increases with many isolated nodes, disconnected thread groups, or low relationship density.
- [ ] Continuity score increases with resolved decisions, cross-referenced reasoning, and operational context revisions.
- [ ] Transfer pressure increases with fragmentation, growth, low coherence, cache miss risk, and high context cost.
- [ ] Transfer pressure remains a signal, not a decision.

Stage 2C checkpoint tests:

- [ ] Same inputs produce same coherence.
- [ ] Disconnected reasoning increases fragmentation.
- [ ] More relationships increase density.
- [ ] More governance evidence increases continuity score.
- [ ] Higher fragmentation plus growth increases transfer pressure.
- [ ] Missing coherence snapshots are rebuilt.
- [ ] Diagnostics explain reasoning topology, missing evidence, cache risk contribution, and transfer pressure.

Stage 2C exit criteria:

- [ ] The system can answer coherence, fragmentation, density, continuity quality, and transfer pressure.
- [ ] Coherence is rebuildable from reasoning, decisions, continuity, metrics, and economics evidence.
- [ ] Coherence remains analysis only and cannot make lifecycle decisions.

### Persistence And Recovery

Persist analysis snapshots under:

- [ ] `.agents/decision-sessions/analysis/metrics/`
- [ ] `.agents/decision-sessions/analysis/economics/`
- [ ] `.agents/decision-sessions/analysis/coherence/`

Analysis snapshots are derived, recoverable, and disposable. If a snapshot is missing or invalid, rebuild it from Decisions, Reasoning, Continuity, and session records.

### Backend Endpoints

- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/metrics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/statistics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/economics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/coherence`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/diagnostics`

Optional compatibility aliases may be added for direct `metrics`, `economics`, and `coherence` paths if useful to match existing endpoint naming style.

### Tests

- [ ] Stage 2A checkpoint tests pass.
- [ ] Stage 2B checkpoint tests pass.
- [ ] Stage 2C checkpoint tests pass.
- [ ] Same full evidence set produces the same aggregate analysis result.
- [ ] Missing analysis snapshots are rebuilt.
- [ ] Aggregate diagnostics explain sources, assumptions, missing evidence, TTL, cache risk, economics, coherence, and transfer pressure.

### Exit Criteria

- [ ] Stage 2A, Stage 2B, and Stage 2C exit criteria are all satisfied.
- [ ] The system can answer session size, age, elapsed duration, idle duration, TTL, cache miss risk, activity, growth, reuse value, transfer value, coherence, fragmentation, density, continuity quality, and transfer pressure.
- [ ] No lifecycle decision or transfer execution exists yet.




