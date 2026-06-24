# Milestone 2: Governance Session Analysis

Objective: measure session facts, evaluate lifecycle economics, and assess reasoning coherence as one capability chain. Analysis answers what is true and what the signals imply. It does not make lifecycle decisions or execute transfer.

Stage 2 is one architectural stage, but it should be implemented through three internal checkpoints:

- [x] Stage 2A: metrics, statistics, TTL, and cache risk.
- [x] Stage 2B: economics.
- [ ] Stage 2C: coherence.

Do not start lifecycle policy until all three checkpoints are complete and the aggregate analysis diagnostics are stable.

### Stage 2A: Metrics, Statistics, TTL, And Cache Risk

Add models:

- [x] `DecisionSessionMetrics`
- [x] `DecisionSessionStatistics`
- [x] `DecisionSessionActivity`
- [x] `DecisionSessionGrowth`
- [x] `DecisionSessionMetricsDiagnostics`
- [x] `DecisionSessionMetricsSnapshot`
- [x] `DecisionSessionCacheMetrics`

Metrics fields:

- [x] `long EstimatedTokenCount`
- [x] `long ContextByteSize`
- [x] `long ReasoningEventCount`
- [x] `long ReasoningThreadCount`
- [x] `long ReasoningRelationshipCount`
- [x] `long DecisionCount`
- [x] `long DecisionCandidateCount`
- [x] `long DecisionProposalCount`
- [x] `long OperationalContextRevisionCount`
- [x] `DateTimeOffset LastActivityAt`
- [x] `DateTimeOffset MeasuredAt`

Statistics and cache fields:

- [x] `TimeSpan SessionAge`
- [x] `TimeSpan SessionElapsedDuration`
- [x] `TimeSpan IdleDuration`
- [x] `decimal GrowthRate`
- [x] `decimal ActivityRate`
- [x] `TimeSpan EstimatedCacheTtl`
- [x] `decimal EstimatedCacheMissRisk`
- [x] `DateTimeOffset? EstimatedCacheExpiresAt`

Add services:

- [x] `ITokenEstimator`
- [x] `IDecisionSessionMetricsService`
- [x] `DecisionSessionMetricsService`
- [x] `DecisionSessionEvidenceReader`

Evidence sources:

- [x] `IDecisionRepository.ListDecisionsAsync`
- [x] `IDecisionRepository.ListCandidatesAsync`
- [x] `IDecisionRepository.ListProposalsAsync`
- [x] `IReasoningRepository.ListEventsAsync`
- [x] `IReasoningRepository.ListThreadsAsync`
- [x] `IReasoningRepository.ListRelationshipsAsync`
- [x] `IOperationalContextProposalStore.ListAsync`
- [x] `IArtifactService` and `IArtifactStore` for current and historical operational context content.

Token and TTL estimation:

- [x] Token estimation must be deterministic and provider-independent.
- [x] Estimate tokens by text length using a stable character-to-token ratio, for example `(characterCount + 3) / 4`.
- [x] Estimate cache TTL and cache miss risk from session elapsed duration, idle duration, and configurable assumptions.
- [x] Include diagnostics describing each source, byte count, character count, TTL assumption, cache risk, and confidence.

Stage 2A checkpoint tests:

- [x] Metrics generated from decisions, reasoning, and operational context evidence.
- [x] Same inputs produce same metrics and statistics.
- [x] Token estimator is deterministic.
- [x] TTL and cache miss risk increase with elapsed and idle duration.
- [x] Activity increases when reasoning, decision, or context evidence increases.
- [x] Growth reflects larger continuity evidence.
- [x] Missing metrics snapshots are rebuilt.
- [x] Diagnostics explain sources, TTL assumptions, cache risk, and missing evidence.

Stage 2A exit criteria:

- [x] The system can answer session size, age, elapsed duration, idle duration, TTL, cache miss risk, activity, and growth.
- [x] Metrics and statistics are rebuildable from authoritative evidence.
- [x] No economics, coherence, policy, eligibility, or transfer behavior exists yet.

### Stage 2B: Economics

Add models:

- [x] `DecisionSessionEconomics`
- [x] `DecisionSessionEconomicsInputs`
- [x] `ReuseValueAssessment`
- [x] `TransferValueAssessment`
- [x] `CacheBenefitAssessment`
- [x] `CacheRiskAssessment`
- [x] `ContinuityBenefitAssessment`
- [x] `DecisionSessionEconomicsDiagnostics`
- [x] `DecisionSessionEconomicsSnapshot`
- [x] `DecisionSessionEconomicsOptions`

Economics fields:

- [x] `decimal EstimatedReuseValue`
- [x] `decimal EstimatedTransferValue`
- [x] `decimal EstimatedContextCost`
- [x] `decimal EstimatedReasoningCost`
- [x] `decimal EstimatedContinuityBenefit`
- [x] `decimal EstimatedCacheBenefit`
- [x] `decimal EstimatedCacheMissRisk`

Add services:

- [x] `IDecisionSessionEconomicsService`
- [x] `DecisionSessionEconomicsService`

Initial deterministic model:

- [x] Normalize values to `0.0m` through `1.0m` where useful.
- [x] Context cost grows with estimated tokens and context bytes.
- [x] Reasoning cost grows with reasoning event count, thread count, and relationship count.
- [x] Continuity benefit grows with decision count, reasoning density, and operational context revisions.
- [x] Cache benefit uses configurable assumptions such as cached-token cost factor `0.10m`.
- [x] Cache risk grows as elapsed duration approaches or exceeds estimated TTL.
- [x] Transfer value grows with growth rate, idle duration, cache miss risk, and large context cost.
- [x] Reuse value grows with continuity benefit, cache benefit, coherence, and recent activity.

Stage 2B checkpoint tests:

- [x] Same inputs produce same economics.
- [x] More continuity increases reuse value.
- [x] Higher cache miss risk increases transfer value.
- [x] Larger reusable corpus increases cache benefit.
- [x] Missing economics snapshots are rebuilt.
- [x] Diagnostics explain inputs, assumptions, missing evidence, TTL, and cache risk.

Stage 2B exit criteria:

- [x] The system can answer reuse value, transfer value, cache benefit, cache risk, context cost, reasoning cost, and continuity benefit.
- [x] Economics is rebuildable from metrics, statistics, TTL/cache inputs, and domain evidence.
- [x] Economics remains analysis only and cannot make lifecycle decisions.

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

- [x] `.agents/decision-sessions/analysis/metrics/`
- [x] `.agents/decision-sessions/analysis/economics/`
- [ ] `.agents/decision-sessions/analysis/coherence/`

Analysis snapshots are derived, recoverable, and disposable. If a snapshot is missing or invalid, rebuild it from Decisions, Reasoning, Continuity, and session records.

### Backend Endpoints

- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/metrics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/statistics`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/economics`
- [ ] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/coherence`
- [x] `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/diagnostics`

Optional compatibility aliases may be added for direct `metrics`, `economics`, and `coherence` paths if useful to match existing endpoint naming style.

### Tests

- [x] Stage 2A checkpoint tests pass.
- [x] Stage 2B checkpoint tests pass.
- [ ] Stage 2C checkpoint tests pass.
- [ ] Same full evidence set produces the same aggregate analysis result.
- [ ] Missing analysis snapshots are rebuilt.
- [ ] Aggregate diagnostics explain sources, assumptions, missing evidence, TTL, cache risk, economics, coherence, and transfer pressure.

### Exit Criteria

- [ ] Stage 2A, Stage 2B, and Stage 2C exit criteria are all satisfied.
- [ ] The system can answer session size, age, elapsed duration, idle duration, TTL, cache miss risk, activity, growth, reuse value, transfer value, coherence, fragmentation, density, continuity quality, and transfer pressure.
- [ ] No lifecycle decision or transfer execution exists yet.




