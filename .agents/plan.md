# Decision Session Lifecycle Implementation Plan

## Goal

Implement a first-class Decision Session Lifecycle capability for governance continuity. A decision session is the long-lived governance trunk for a repository. It tracks accumulated decision, reasoning, and continuity evidence; evaluates whether the current governance session should continue or transfer continuity; checks whether transfer is operationally eligible; executes transfer when eligible; recovers after failure; exposes read-only observability; integrates into workflow as a consumer; and certifies lifecycle correctness.

The lifecycle optimizes:

- Governance continuity
- Decision throughput
- Session economics
- Reasoning coherence
- Cache and continuity economics
- Continuity transfer
- Lifecycle recovery

It must not become execution lifecycle, workflow execution, task execution, commit lifecycle, push lifecycle, or execution-session routing.

## Core Invariants

- A repository may have zero active decision sessions before initialization or during diagnostic failure states, but it must never have more than one active decision session.
- In normal operation after initialization or transfer, one active decision session should exist per repository.
- Decision sessions are separate from execution sessions. `CommandCenter.DecisionSessions` must not reference `CommandCenter.Execution`.
- The decision-session lifecycle remains the proto-governance trunk. The execution session remains the proto-operational trunk.
- The registry owns session identity, activation, state transitions, and the single-active-session invariant.
- Analysis produces signals. Policy makes lifecycle decisions. Transfer eligibility checks operational readiness. Transfer execution mutates lifecycle state.
- Metrics, economics, coherence, observability, workflow projections, and certification are not authoritative for session state.
- Lifecycle policy may decide `Continue` or `Transfer`, but it must not execute transfer or mutate registry state.
- Transfer eligibility may block or defer transfer execution, but it must not change the policy decision.
- Transfer execution is the only lifecycle component allowed to retire a session and activate a replacement because of policy.
- Transfer means create a continuity artifact, integrate continuity, retire the source session, create a replacement session, and activate that replacement.
- Users do not choose reuse, transfer, replacement, or retirement. They may observe current session state, size, economics, transfer history, recovery history, and diagnostics.
- All derived snapshots must be disposable and rebuildable from authoritative evidence in Decisions, Reasoning, Continuity, and session records.
- Workflow consumes decision-session state. Workflow must not trigger transfer, retire sessions, create sessions, or override lifecycle policy.

## Current Codebase Fit

The solution currently has these relevant projects:

- `src/CommandCenter.Core`: repository identity, artifact storage, repository service, planning, artifact paths.
- `src/CommandCenter.Decisions`: decision records, governance, quality, certification, repository-backed artifacts.
- `src/CommandCenter.Reasoning`: reasoning events, threads, relationships, graph, reconstruction, certification.
- `src/CommandCenter.Continuity`: operational context proposals, lifecycle, diagnostics, reports.
- `src/CommandCenter.Workflow`: workflow projections, health, recovery, certification, hosted recovery.
- `src/CommandCenter.Middle`: repository dashboard and workspace projections.
- `src/CommandCenter.Backend`: ASP.NET minimal APIs and service composition.
- `src/CommandCenter.Shell` and `src/CommandCenter.UI`: Tauri/React client surfaces.
- `tests/CommandCenter.Backend.Tests`: xUnit service, persistence, endpoint, and projection tests.

The new lifecycle should follow existing patterns:

- Use `Guid RepositoryId`, matching existing domain models.
- Use `IRepositoryService` to resolve `Repository` by id in services.
- Use `IArtifactStore` plus repository-relative artifact paths through `ArtifactPath.ResolveRepositoryPath`.
- Use schema-wrapped JSON documents with deterministic `JsonSerializerOptions` and enum string conversion.
- Add a domain `ServiceCollectionExtensions.AddDecisionSessions()` method.
- Add backend endpoint mapping through a dedicated `MapDecisionSessionEndpoints()` extension.
- Put tests in `tests/CommandCenter.Backend.Tests` unless a separate test project is later introduced.

## Project And Reference Plan

Create:

```text
src/CommandCenter.DecisionSessions/
  Abstractions/
  Certification/
  Extensions/
  Models/
  Persistence/
  Primitives/
  Services/
  CommandCenter.DecisionSessions.csproj
```

The new project references:

- `CommandCenter.Core`
- `CommandCenter.Decisions`
- `CommandCenter.Reasoning`
- `CommandCenter.Continuity`

The new project must not reference:

- `CommandCenter.Execution`
- `CommandCenter.Workflow`
- `CommandCenter.Middle`
- `CommandCenter.Backend`
- `CommandCenter.UI`
- `CommandCenter.Shell`

Update:

- `CommandCenter.slnx`: add `src/CommandCenter.DecisionSessions/CommandCenter.DecisionSessions.csproj`.
- `src/CommandCenter.Backend/CommandCenter.Backend.csproj`: reference `CommandCenter.DecisionSessions`.
- `tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj`: reference `CommandCenter.DecisionSessions`.
- `src/CommandCenter.Workflow/CommandCenter.Workflow.csproj`: add a reference to `CommandCenter.DecisionSessions` only when workflow integration starts.
- `src/CommandCenter.Middle/CommandCenter.Middle.csproj`: add a reference to `CommandCenter.DecisionSessions` only when repository summary integration starts.

## Persistence Layout

Use repository-local artifacts under:

```text
.agents/decision-sessions/
  registry.json
  analysis/
    metrics/
    economics/
    coherence/
  lifecycle/
    policy/
    eligibility/
  continuity-artifacts/
  transfers/
  recovery/
  observability/
  reports/
  certification/
```

Create:

- `DecisionSessionArtifactPaths`
- `DecisionSessionArtifactDocument<T>`
- `DecisionSessionJson`

Use schema version `decision-sessions.v1`. Every document must include schema version, repository id, timestamps, and payload. Reads must reject unsupported schema versions and cross-repository payloads.

`registry.json` contains session records and is the authority for session state. Other files contain derived analysis snapshots, policy evaluations, eligibility checks, continuity artifacts, transfer events, histories, diagnostics, reports, and certification results.

## Lifecycle Shape

The lifecycle stack is:

```text
Decision Session
  -> Registry
  -> Governance Session Analysis
  -> Lifecycle Policy
  -> Transfer Eligibility
  -> Continuity Transfer
  -> Recovery
  -> Observability
  -> Workflow Consumption
  -> Certification
```

The work is organized into six implementation stages:

1. Foundation and registry.
2. Governance session analysis.
3. Governance lifecycle.
4. Lifecycle observability.
5. Workflow and repository consumption.
6. Certification.

The implementation order remains fine-grained inside those stages so each capability can be tested independently.

## Stage 1: Foundation And Registry

Objective: establish the decision-session domain, persistence, registry authority, and recovery validation.

### Domain

Add primitives:

- `DecisionSessionId`: immutable strongly typed identity around `Guid`.
- `DecisionSessionState`: `Created`, `Active`, `TransferPending`, `Transferred`, `Retired`.
- `DecisionSessionOwnership`: repository id, created by, created at.

Add models:

- `DecisionSession`
- `DecisionSessionMetadata`
- `DecisionSessionDiagnostics`
- `DecisionSessionRecord`
- `DecisionSessionProjection`

`DecisionSession` fields:

- `DecisionSessionId Id`
- `Guid RepositoryId`
- `DecisionSessionState State`
- `DateTimeOffset CreatedAt`
- `DateTimeOffset? ActivatedAt`
- `DateTimeOffset? RetiredAt`
- `DecisionSessionOwnership Ownership`
- `DecisionSessionMetadata Metadata`

Do not add metrics, economics, coherence, reuse score, transfer score, token count, or transfer metadata to the aggregate.

### Contracts

Repository operations:

- `Task<DecisionSession> CreateAsync(Repository repository, DecisionSession session)`
- `Task<DecisionSession> UpdateAsync(Repository repository, DecisionSession session)`
- `Task<DecisionSession?> GetAsync(Repository repository, DecisionSessionId sessionId)`
- `Task<DecisionSession?> GetActiveAsync(Repository repository)`
- `Task<IReadOnlyList<DecisionSession>> ListAsync(Repository repository)`

Registry operations:

- `Task<DecisionSession> CreateSessionAsync(Guid repositoryId, string createdBy)`
- `Task<DecisionSession> ActivateSessionAsync(Guid repositoryId, DecisionSessionId sessionId)`
- `Task<DecisionSession> MarkTransferPendingAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)`
- `Task<DecisionSession> MarkTransferredAsync(Guid repositoryId, DecisionSessionId sourceSessionId, DecisionSessionId targetSessionId, string reason)`
- `Task<DecisionSession> RetireSessionAsync(Guid repositoryId, DecisionSessionId sessionId, string reason)`
- `Task<DecisionSession?> GetActiveSessionAsync(Guid repositoryId)`

Recovery operations:

- Load persisted sessions.
- Validate duplicate ids and active-session count.
- Validate timestamp consistency.
- Produce diagnostics.
- Do not repair state in the initial implementation.

### Registry Rules

- `CreateSessionAsync` creates a `Created` session.
- `ActivateSessionAsync` allows `Created -> Active`.
- Activating a session fails if another session is already active in the same repository.
- Activating an already active session fails.
- Activating `Transferred` or `Retired` fails.
- `MarkTransferPendingAsync` allows `Active -> TransferPending`.
- `RetireSessionAsync` allows `Active -> Retired` and `TransferPending -> Retired`.
- `MarkTransferredAsync` is only used by transfer execution after replacement session creation.
- `GetActiveSessionAsync` returns null for zero active sessions, returns the active session for one active session, and fails with diagnostics for more than one active session.

### Persistence

Implement:

- `FileSystemDecisionSessionRepository`
- `DecisionSessionValidationResult`
- `DecisionSessionRegistryDiagnostics`
- `DecisionSessionRecoveryService`

Persistence rules:

- Store all sessions in `.agents/decision-sessions/registry.json`.
- Keep records deterministic and ordered by creation time, then session id.
- Reject duplicate ids.
- Reject records whose `RepositoryId` does not match the repository being read.
- Reject invalid timestamp relationships such as `ActivatedAt > RetiredAt`.

### Backend Endpoints

- `GET /api/repositories/{repositoryId:guid}/decision-sessions`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/active`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/diagnostics`

Use a route group and shared `HandleAsync` error mapping similar to `ReasoningEndpoints`.

### Tests

- Session id stability.
- State enum round-trip through JSON.
- Aggregate creation.
- Repository ownership validation.
- Repository save/load/list.
- Create, activate, and retire sessions.
- Zero active sessions allowed.
- One active session allowed.
- Two active sessions rejected.
- Duplicate ids rejected.
- Invalid timestamp state produces validation diagnostics.
- Unsupported schema version rejected.
- Cross-repository payload rejected.
- Endpoints return list, active session, and diagnostics.

### Exit Criteria

- Domain compiles.
- Service registration exists.
- Registry is operational.
- Persistence is operational.
- Recovery validates persisted state.
- The single-active-session invariant is enforced.
- The system can answer which governance session is active, or that none is active.

## Stage 2: Governance Session Analysis

Objective: measure session facts, evaluate lifecycle economics, and assess reasoning coherence as one capability chain. Analysis answers what is true and what the signals imply. It does not make lifecycle decisions or execute transfer.

Stage 2 is one architectural stage, but it should be implemented through three internal checkpoints:

- Stage 2A: metrics, statistics, TTL, and cache risk.
- Stage 2B: economics.
- Stage 2C: coherence.

Do not start lifecycle policy until all three checkpoints are complete and the aggregate analysis diagnostics are stable.

### Stage 2A: Metrics, Statistics, TTL, And Cache Risk

Add models:

- `DecisionSessionMetrics`
- `DecisionSessionStatistics`
- `DecisionSessionActivity`
- `DecisionSessionGrowth`
- `DecisionSessionMetricsDiagnostics`
- `DecisionSessionMetricsSnapshot`
- `DecisionSessionCacheMetrics`

Metrics fields:

- `long EstimatedTokenCount`
- `long ContextByteSize`
- `long ReasoningEventCount`
- `long ReasoningThreadCount`
- `long ReasoningRelationshipCount`
- `long DecisionCount`
- `long DecisionCandidateCount`
- `long DecisionProposalCount`
- `long OperationalContextRevisionCount`
- `DateTimeOffset LastActivityAt`
- `DateTimeOffset MeasuredAt`

Statistics and cache fields:

- `TimeSpan SessionAge`
- `TimeSpan SessionElapsedDuration`
- `TimeSpan IdleDuration`
- `decimal GrowthRate`
- `decimal ActivityRate`
- `TimeSpan EstimatedCacheTtl`
- `decimal EstimatedCacheMissRisk`
- `DateTimeOffset? EstimatedCacheExpiresAt`

Add services:

- `ITokenEstimator`
- `IDecisionSessionMetricsService`
- `DecisionSessionMetricsService`
- `DecisionSessionEvidenceReader`

Evidence sources:

- `IDecisionRepository.ListDecisionsAsync`
- `IDecisionRepository.ListCandidatesAsync`
- `IDecisionRepository.ListProposalsAsync`
- `IReasoningRepository.ListEventsAsync`
- `IReasoningRepository.ListThreadsAsync`
- `IReasoningRepository.ListRelationshipsAsync`
- `IOperationalContextProposalStore.ListAsync`
- `IArtifactService` and `IArtifactStore` for current and historical operational context content.

Token and TTL estimation:

- Token estimation must be deterministic and provider-independent.
- Estimate tokens by text length using a stable character-to-token ratio, for example `(characterCount + 3) / 4`.
- Estimate cache TTL and cache miss risk from session elapsed duration, idle duration, and configurable assumptions.
- Include diagnostics describing each source, byte count, character count, TTL assumption, cache risk, and confidence.

Stage 2A checkpoint tests:

- Metrics generated from decisions, reasoning, and operational context evidence.
- Same inputs produce same metrics and statistics.
- Token estimator is deterministic.
- TTL and cache miss risk increase with elapsed and idle duration.
- Activity increases when reasoning, decision, or context evidence increases.
- Growth reflects larger continuity evidence.
- Missing metrics snapshots are rebuilt.
- Diagnostics explain sources, TTL assumptions, cache risk, and missing evidence.

Stage 2A exit criteria:

- The system can answer session size, age, elapsed duration, idle duration, TTL, cache miss risk, activity, and growth.
- Metrics and statistics are rebuildable from authoritative evidence.
- No economics, coherence, policy, eligibility, or transfer behavior exists yet.

### Stage 2B: Economics

Add models:

- `DecisionSessionEconomics`
- `DecisionSessionEconomicsInputs`
- `ReuseValueAssessment`
- `TransferValueAssessment`
- `CacheBenefitAssessment`
- `CacheRiskAssessment`
- `ContinuityBenefitAssessment`
- `DecisionSessionEconomicsDiagnostics`
- `DecisionSessionEconomicsSnapshot`
- `DecisionSessionEconomicsOptions`

Economics fields:

- `decimal EstimatedReuseValue`
- `decimal EstimatedTransferValue`
- `decimal EstimatedContextCost`
- `decimal EstimatedReasoningCost`
- `decimal EstimatedContinuityBenefit`
- `decimal EstimatedCacheBenefit`
- `decimal EstimatedCacheMissRisk`

Add services:

- `IDecisionSessionEconomicsService`
- `DecisionSessionEconomicsService`

Initial deterministic model:

- Normalize values to `0.0m` through `1.0m` where useful.
- Context cost grows with estimated tokens and context bytes.
- Reasoning cost grows with reasoning event count, thread count, and relationship count.
- Continuity benefit grows with decision count, reasoning density, and operational context revisions.
- Cache benefit uses configurable assumptions such as cached-token cost factor `0.10m`.
- Cache risk grows as elapsed duration approaches or exceeds estimated TTL.
- Transfer value grows with growth rate, idle duration, cache miss risk, and large context cost.
- Reuse value grows with continuity benefit, cache benefit, coherence, and recent activity.

Stage 2B checkpoint tests:

- Same inputs produce same economics.
- More continuity increases reuse value.
- Higher cache miss risk increases transfer value.
- Larger reusable corpus increases cache benefit.
- Missing economics snapshots are rebuilt.
- Diagnostics explain inputs, assumptions, missing evidence, TTL, and cache risk.

Stage 2B exit criteria:

- The system can answer reuse value, transfer value, cache benefit, cache risk, context cost, reasoning cost, and continuity benefit.
- Economics is rebuildable from metrics, statistics, TTL/cache inputs, and domain evidence.
- Economics remains analysis only and cannot make lifecycle decisions.

### Stage 2C: Coherence

Add models:

- `DecisionSessionCoherence`
- `DecisionSessionCoherenceInputs`
- `FragmentationAssessment`
- `DensityAssessment`
- `ContinuityQualityAssessment`
- `TransferPressureAssessment`
- `DecisionSessionCoherenceDiagnostics`
- `DecisionSessionCoherenceSnapshot`

Coherence fields:

- `decimal CoherenceScore`
- `decimal FragmentationScore`
- `decimal DensityScore`
- `decimal ContinuityScore`
- `decimal TransferPressure`

Add services:

- `IDecisionSessionCoherenceService`
- `DecisionSessionCoherenceService`

Evidence sources:

- `IReasoningRepository` for events, threads, relationships.
- `IReasoningGraphService.GetGraphAsync` for nodes and graph relationships.
- `IDecisionRepository` for decision and proposal counts.
- Continuity evidence for operational context revision counts.

Initial deterministic model:

- Density score increases with relationship count relative to node count.
- Fragmentation score increases with many isolated nodes, disconnected thread groups, or low relationship density.
- Continuity score increases with resolved decisions, cross-referenced reasoning, and operational context revisions.
- Transfer pressure increases with fragmentation, growth, low coherence, cache miss risk, and high context cost.
- Transfer pressure remains a signal, not a decision.

Stage 2C checkpoint tests:

- Same inputs produce same coherence.
- Disconnected reasoning increases fragmentation.
- More relationships increase density.
- More governance evidence increases continuity score.
- Higher fragmentation plus growth increases transfer pressure.
- Missing coherence snapshots are rebuilt.
- Diagnostics explain reasoning topology, missing evidence, cache risk contribution, and transfer pressure.

Stage 2C exit criteria:

- The system can answer coherence, fragmentation, density, continuity quality, and transfer pressure.
- Coherence is rebuildable from reasoning, decisions, continuity, metrics, and economics evidence.
- Coherence remains analysis only and cannot make lifecycle decisions.

### Persistence And Recovery

Persist analysis snapshots under:

- `.agents/decision-sessions/analysis/metrics/`
- `.agents/decision-sessions/analysis/economics/`
- `.agents/decision-sessions/analysis/coherence/`

Analysis snapshots are derived, recoverable, and disposable. If a snapshot is missing or invalid, rebuild it from Decisions, Reasoning, Continuity, and session records.

### Backend Endpoints

- `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/metrics`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/statistics`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/economics`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/coherence`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/analysis/diagnostics`

Optional compatibility aliases may be added for direct `metrics`, `economics`, and `coherence` paths if useful to match existing endpoint naming style.

### Tests

- Stage 2A checkpoint tests pass.
- Stage 2B checkpoint tests pass.
- Stage 2C checkpoint tests pass.
- Same full evidence set produces the same aggregate analysis result.
- Missing analysis snapshots are rebuilt.
- Aggregate diagnostics explain sources, assumptions, missing evidence, TTL, cache risk, economics, coherence, and transfer pressure.

### Exit Criteria

- Stage 2A, Stage 2B, and Stage 2C exit criteria are all satisfied.
- The system can answer session size, age, elapsed duration, idle duration, TTL, cache miss risk, activity, growth, reuse value, transfer value, coherence, fragmentation, density, continuity quality, and transfer pressure.
- No lifecycle decision or transfer execution exists yet.

## Stage 3: Governance Lifecycle

Objective: decide whether the active governance session should continue or transfer, verify transfer readiness, execute transfer safely, and recover lifecycle state.

### Lifecycle Policy

Add primitives and models:

- `DecisionSessionLifecycleDecision`: `Continue`, `Transfer`
- `DecisionSessionLifecycleEvaluation`
- `ReuseScoreAssessment`
- `TransferScoreAssessment`
- `DecisionSessionLifecycleDiagnostics`
- `DecisionSessionLifecycleSnapshot`
- `DecisionSessionLifecyclePolicyOptions`

Lifecycle evaluation fields:

- `DecisionSessionLifecycleDecision Decision`
- `decimal ReuseScore`
- `decimal TransferScore`
- `string Reason`
- `IReadOnlyList<string> ContributingFactors`
- `DateTimeOffset EvaluatedAt`

Add service:

- `IDecisionSessionLifecyclePolicy`
- `DecisionSessionLifecyclePolicy`

Inputs:

- `DecisionSession`
- `DecisionSessionMetrics`
- `DecisionSessionStatistics`
- `DecisionSessionCacheMetrics`
- `DecisionSessionEconomics`
- `DecisionSessionCoherence`

Deterministic policy:

- Reuse score grows with estimated reuse value, cache benefit, continuity benefit, and coherence score.
- Transfer score grows with estimated transfer value, transfer pressure, fragmentation, growth, and cache miss risk.
- If `ReuseScore > TransferScore`, decide `Continue`.
- If `TransferScore > ReuseScore`, decide `Transfer`.
- If scores are equal, decide `Continue` to avoid churn.
- Same inputs must always produce the same evaluation.

Persist lifecycle snapshots under `.agents/decision-sessions/lifecycle/policy/`.

### Transfer Eligibility

Transfer eligibility is the operational gate between policy and transfer execution. It prevents policy from knowing operational details while preventing transfer from starting when required continuity or repository evidence is unavailable.

Add models:

- `DecisionSessionTransferEligibility`
- `DecisionSessionTransferEligibilityStatus`: `NotApplicable`, `Eligible`, `Blocked`, `Deferred`
- `DecisionSessionTransferEligibilityFinding`
- `DecisionSessionTransferEligibilityDiagnostics`

Add service:

- `IDecisionSessionTransferEligibilityService`
- `DecisionSessionTransferEligibilityService`

Eligibility inputs:

- Active session.
- Lifecycle policy evaluation.
- Registry validation.
- Repository availability.
- Transfer-pending state.
- Continuity evidence availability.
- Operational context availability.
- Ability to create a continuity artifact.
- Recovery findings.

Eligibility rules:

- If policy decision is `Continue`, eligibility is `NotApplicable`.
- If no active session exists, eligibility is `Blocked`.
- If registry has duplicate active sessions, eligibility is `Blocked`.
- If source session is already `TransferPending`, eligibility is `Deferred` unless recovery can prove the prior transfer failed safely.
- If operational context evidence is unavailable, eligibility is `Blocked`.
- If continuity artifact generation cannot produce a valid artifact, eligibility is `Blocked`.
- If repository state is unavailable or locked, eligibility is `Deferred` or `Blocked` with diagnostics.
- If unresolved recovery findings threaten continuity, eligibility is `Blocked`.
- If all preconditions pass and policy decision is `Transfer`, eligibility is `Eligible`.

Persist eligibility checks under `.agents/decision-sessions/lifecycle/eligibility/`.

### Continuity Artifact

Add a first-class canonical transfer payload:

- `DecisionSessionContinuityArtifact`

This artifact is the durable governance-continuity payload transferred between source and replacement decision sessions. It is not merely a diagnostic snapshot.

Fields:

- `string ArtifactId`
- `Guid RepositoryId`
- `DecisionSessionId SourceSessionId`
- `DecisionSessionId? TargetSessionId`
- `DateTimeOffset CreatedAt`
- `DecisionSessionLifecycleEvaluation PolicyEvaluation`
- `DecisionSessionMetrics Metrics`
- `DecisionSessionEconomics Economics`
- `DecisionSessionCoherence Coherence`
- `DecisionSessionCacheMetrics Cache`
- `IReadOnlyList<DecisionSessionContinuityReference> DecisionReferences`
- `IReadOnlyList<DecisionSessionContinuityReference> ReasoningReferences`
- `IReadOnlyList<DecisionSessionContinuityReference> OperationalContextReferences`
- `string ContinuityFingerprint`
- `IReadOnlyList<string> Diagnostics`

Add:

- `DecisionSessionContinuityReference`
- `DecisionSessionContinuityArtifactValidation`
- `IDecisionSessionContinuityArtifactService`
- `DecisionSessionContinuityArtifactService`

Persistence:

- Store artifacts under `.agents/decision-sessions/continuity-artifacts/`.
- Use deterministic artifact ids such as `continuity.YYYYMMDDTHHMMSS.fffffffZ.<source-session-id>.json`.
- Store a markdown projection only if useful for human diagnostics.
- Validate repository id, source session id, fingerprint, evidence references, and schema version on read.

Continuity artifact rules:

- The artifact is the canonical payload for transfer.
- It is durable, recoverable, and auditable.
- It records what continuity is being carried forward, not who owns operational context.
- Decision Sessions must never own Operational Context. They contribute transfer artifacts that continuity services may consume.

### Transfer Execution

Add models:

- `DecisionSessionTransfer`
- `DecisionSessionTransferEvent`
- `DecisionSessionTransferDiagnostics`
- `DecisionSessionTransferResult`

Add services:

- `IDecisionSessionTransferService`
- `DecisionSessionTransferService`
- `IDecisionSessionContinuityCaptureService`
- `DecisionSessionContinuityCaptureService`
- `IDecisionSessionContinuityIntegrationService`
- `DecisionSessionContinuityIntegrationService`

Transfer flow:

1. Load active session.
2. Require policy evaluation decision `Transfer`.
3. Require transfer eligibility status `Eligible`.
4. Mark source session `TransferPending`.
5. Create and persist `DecisionSessionContinuityArtifact`.
6. Persist transfer started event.
7. Integrate the continuity artifact into existing continuity infrastructure without making decision sessions the owner of operational context.
8. Retire source session.
9. Create replacement session with new identity and inherited repository ownership.
10. Activate replacement session.
11. Update the continuity artifact with target session id if not known at creation time.
12. Mark transfer completed and persist diagnostics.

Invariant rules:

- Source must be active before transfer starts.
- `TransferPending` is allowed during transfer.
- Do not create or activate replacement before source is no longer active.
- Do not allow two active sessions at any point.
- Failed transfer must leave diagnostics, eligibility findings, and enough state for recovery.

Persist transfer events under `.agents/decision-sessions/transfers/`.

### Recovery And Resilience

Add models:

- `DecisionSessionRecoveryResult`
- `DecisionSessionRecoveryFinding`
- `TransferRecoveryAssessment`
- `DecisionSessionRecoveryDiagnostics`
- `DecisionSessionRecoveryHistory`
- `DecisionSessionRecoveryEvent`

Extend:

- `DecisionSessionRecoveryService`

Add hosted service:

- `DecisionSessionRecoveryHostedService`

Recovery responsibilities:

- Load registry.
- Validate active-session count.
- Validate duplicate ids.
- Reconstruct active session from registry, transfer events, continuity artifacts, and continuity evidence.
- Reconstruct transfer history from transfer events, continuity artifacts, and session records.
- Assess interrupted `TransferPending` sessions.
- Rebuild missing metrics, economics, coherence, policy, and eligibility snapshots.
- Persist recovery events, findings, and diagnostics.

Recovery philosophy:

- Decisions, Reasoning, and Continuity evidence outrank derived snapshots.
- Continuity artifacts outrank transfer diagnostics.
- Continuity evidence outranks stale session-state hints when they conflict.
- Do not silently choose one active session when duplicate active sessions exist.
- Repository recovery failures must be isolated to that repository.

Hosted startup behavior:

1. List repositories through `IRepositoryService`.
2. Recover each repository independently.
3. Publish diagnostics.
4. Continue recovering other repositories if one fails.

### Backend Endpoints

- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/policy/diagnostics`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/eligibility/diagnostics`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/continuity-artifacts/{artifactId}`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/history`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/transfers/diagnostics`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/history`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/recovery/diagnostics`

Do not add a manual transfer endpoint.

### Tests

- Same inputs produce same policy decision.
- Reuse score greater than transfer score decides `Continue`.
- Transfer score greater than reuse score decides `Transfer`.
- Equal scores decide `Continue`.
- Higher cache miss risk raises transfer score.
- Eligibility is `NotApplicable` when policy is `Continue`.
- Eligibility is `Blocked` when no active session exists.
- Eligibility is `Blocked` for duplicate active sessions.
- Eligibility is `Blocked` when continuity artifact generation fails.
- Eligibility is `Blocked` when operational context evidence is unavailable.
- Eligibility is `Deferred` or `Blocked` when repository state is unavailable or locked.
- Transfer decision plus eligible status results in transfer execution.
- Transfer decision plus blocked eligibility does not mutate registry state.
- Continuity artifact is created before source retirement.
- Continuity artifact is the canonical transfer payload and validates required references.
- Source session is retired.
- Replacement session is created and active.
- Two active sessions never exist.
- Transfer events are durable and auditable.
- Active session recovers after restart.
- Completed transfer recovers replacement as active.
- `TransferPending` after restart emits diagnostics.
- Missing analysis, policy, and eligibility snapshots are rebuilt.
- Duplicate active sessions produce a recovery finding.
- Hosted recovery isolates repository failures.

### Exit Criteria

- Policy can decide continue or transfer.
- Eligibility can block or defer transfer without changing policy.
- Transfer creates a first-class continuity artifact.
- Transfer preserves continuity and never creates parallel active sessions.
- Recovery survives restart, missing snapshots, duplicate-active corruption, and interrupted transfer states with diagnostics.

## Stage 4: Lifecycle Observability

Objective: expose read-only lifecycle visibility and explanations without adding new lifecycle behavior.

Add models:

- `DecisionSessionLifecycleProjection`
- `DecisionSessionLifecycleHistory`
- `DecisionSessionInfluenceTrace`
- `DecisionSessionTransferEventProjection`
- `DecisionSessionContinuityArtifactProjection`
- `DecisionSessionSizeProjection`
- `DecisionSessionHealthAssessment`
- `DecisionSessionHealthDimension`

Add service:

- `IDecisionSessionObservabilityService`
- `DecisionSessionObservabilityService`

Projection composition:

- Current session projection.
- Current metrics.
- Current economics.
- Current coherence.
- Current policy evaluation.
- Current transfer eligibility.
- Current continuity artifact, if transfer is pending or completed.
- Recent transfer events.
- Recent recovery events.
- Diagnostics.

History events:

- Created
- Activated
- AnalysisCaptured
- PolicyEvaluated
- TransferEligibilityEvaluated
- ContinuityArtifactCreated
- TransferStarted
- TransferCompleted
- Retired
- ReplacementCreated
- Recovered

Influence trace categories:

- Metrics
- Cache TTL
- Cache miss risk
- Economics
- Coherence
- Policy
- Eligibility
- Continuity artifact
- Transfer
- Recovery

Health dimensions:

- Registry
- Analysis
- Policy
- Eligibility
- Continuity artifact
- Transfer
- Recovery

Health must remain decomposed. Do not hide state in a single opaque score.

Backend endpoints:

- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/projection`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/history`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/influence`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/lifecycle/health`

Persistence:

- Observability snapshots may be stored under `.agents/decision-sessions/observability/`.
- Observability is derived, disposable, and rebuildable.

Tests:

- Projection composes session, analysis, policy, eligibility, continuity artifact, transfer, and recovery.
- History reconstructs creation, activation, policy, eligibility, continuity artifact creation, transfer, retirement, and recovery.
- Influence trace contains economics, coherence, TTL, cache risk, and eligibility signals.
- Transfer event projection includes source, target, reason, token size, reuse score, transfer score, and eligibility status.
- Continuity artifact projection includes canonical artifact id, fingerprint, source session, target session, and evidence references.
- Size projection exposes token, context, reasoning, and measured-at values.
- Health dimensions report each subsystem independently.
- Observability never mutates registry, transfer, eligibility, or policy state.
- Missing observability snapshot is rebuilt.

Exit criteria:

- The system explains current lifecycle state, why policy chose continue or transfer, whether transfer is eligible, how large the session is, when transfer happened, which artifact carried continuity, why transfer happened, and whether recovery was required.
- No manual lifecycle controls are introduced.

## Stage 5: Workflow And Repository Consumption

Objective: let workflow and repository summaries consume governance-session state without owning it.

Add workflow models in `src/CommandCenter.Workflow/Models`:

- `WorkflowDecisionSessionProjection`
- `WorkflowGovernanceSummary`
- `WorkflowTransferProjection`
- `WorkflowGovernanceHealthProjection`
- `WorkflowGovernanceInfluenceProjection`
- `WorkflowGovernanceReadiness`
- `DecisionSessionWorkflowDiagnostics`

Add workflow service abstraction and implementation:

- `IWorkflowDecisionSessionService`
- `WorkflowDecisionSessionService`

Integrate into:

- `WorkflowProjectionService`
- `WorkflowHealthService`
- `WorkflowReportService`
- `WorkflowCertificationService`

Workflow projection fields:

- Decision session id.
- Decision session state.
- Estimated token count.
- Estimated cache TTL.
- Estimated cache miss risk.
- Reuse score.
- Transfer score.
- Coherence score.
- Transfer pressure.
- Current lifecycle decision.
- Transfer eligibility status.
- Continuity artifact id and fingerprint when relevant.
- Transfer lineage.
- Governance health dimensions.

Repository summary integration:

- Add `RepositoryDecisionSessionSummary` in `src/CommandCenter.Middle/Projections`.
- Extend `RepositoryDashboardProjection` and `RepositoryWorkspaceProjection`.
- Extend `RepositoryProjectionService` through an optional decision-session observability dependency, matching the existing optional reasoning dependency pattern.

Backend endpoints:

- `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/health`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/influence`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/workflow/summary`

Authority rules:

- Workflow may display, report, explain, and certify consumption.
- Workflow may not change lifecycle decisions.
- Workflow may not evaluate transfer eligibility as authority.
- Workflow may not execute transfer.
- Workflow may not retire, create, or activate sessions.

Tests:

- Lifecycle state appears in workflow projection.
- Continue and transfer decisions are visible.
- Eligibility status is visible.
- Continuity artifact lineage is projected.
- Transfer lineage is projected.
- Lifecycle health appears in workflow health.
- Influence trace appears in workflow influence projection.
- Repository summary includes decision-session state, TTL, cache risk, and health.
- Workflow cannot call mutating lifecycle APIs.
- Deleted workflow projection is rebuilt.

Exit criteria:

- Workflow can answer active governance session, current lifecycle recommendation, transfer eligibility, health, recent transfer lineage, continuity artifact lineage, and increasing transfer pressure.
- The decision-session lifecycle remains authoritative.

## Stage 6: Certification

Objective: prove lifecycle correctness without adding new lifecycle behavior.

Add certification models:

- `DecisionSessionCertificationResult`
- `DecisionSessionCertificationFinding`
- `DecisionSessionCertificationReport`
- `DecisionSessionGovernanceReport`
- `DecisionSessionHealthReport`
- `DecisionSessionLifecycleEndToEndFixture`

Add certification service:

- `IDecisionSessionCertificationService`
- `DecisionSessionCertificationService`

Certification categories:

- Authority
- Single active session
- Analysis determinism
- TTL and cache risk
- Policy determinism
- Transfer eligibility
- Continuity artifact
- Transfer
- Recovery
- Continuity
- Workflow integration
- Diagnostics
- Health

Certification rules:

- Fail if more than one active session exists.
- Fail if analysis is non-deterministic for identical inputs.
- Fail if TTL or cache miss risk is missing from analysis.
- Fail if policy is non-deterministic for identical inputs.
- Fail if transfer executes while eligibility is blocked or deferred.
- Fail if transfer lacks a valid continuity artifact.
- Fail if transfer lacks source session retirement, replacement session activation, or continuity evidence.
- Fail if recovery cannot rebuild missing analysis, policy, or eligibility snapshots.
- Fail if workflow can mutate lifecycle state.
- Fail if lifecycle state lacks diagnostics.
- Fail if health reports healthy while evidence contradicts it.
- Certification may inspect, validate, report, and fail.
- Certification must not repair, transfer, retire, create sessions, or change policy.

Backend endpoints:

- `GET /api/repositories/{repositoryId:guid}/decision-sessions/certification`
- `GET /api/repositories/{repositoryId:guid}/decision-sessions/certification/report`
- `POST /api/repositories/{repositoryId:guid}/decision-sessions/certification`

Persistence:

- Certification reports under `.agents/decision-sessions/certification/`.
- Optional markdown reports under `.agents/decision-sessions/reports/`.

End-to-end fixture:

1. Create session.
2. Activate session.
3. Build governance session analysis.
4. Evaluate lifecycle policy.
5. Evaluate transfer eligibility.
6. Create continuity artifact.
7. Execute transfer when evaluation says `Transfer` and eligibility is `Eligible`.
8. Recover after simulated restart.
9. Project observability.
10. Project workflow consumption.
11. Run certification.

Tests:

- Authority boundary: workflow cannot transfer.
- Single-active-session certification fails on duplicates.
- Analysis determinism passes for identical inputs.
- TTL and cache risk appear in certification evidence.
- Policy determinism passes for identical inputs.
- Eligibility prevents unsafe transfer.
- Transfer preserves continuity through a valid continuity artifact.
- Recovery reconstructs active session and derived snapshots.
- Diagnostics exist for continue, transfer, eligibility blocked, recovery, and failure states.
- Workflow consumes lifecycle correctly.
- End-to-end lifecycle passes.

Exit criteria:

- Certification service exists.
- Certification reports are persisted.
- End-to-end lifecycle fixture passes.
- The system can prove governance continuity survives long horizons, transfer preserves continuity, recovery reconstructs truth, workflow remains a consumer, and at most one active governance session exists.

## Endpoint Summary

All endpoints are repository scoped unless noted. Most endpoints are read-only.

```text
GET  /api/repositories/{repositoryId}/decision-sessions
GET  /api/repositories/{repositoryId}/decision-sessions/active
GET  /api/repositories/{repositoryId}/decision-sessions/diagnostics

GET  /api/repositories/{repositoryId}/decision-sessions/analysis/metrics
GET  /api/repositories/{repositoryId}/decision-sessions/analysis/statistics
GET  /api/repositories/{repositoryId}/decision-sessions/analysis/economics
GET  /api/repositories/{repositoryId}/decision-sessions/analysis/coherence
GET  /api/repositories/{repositoryId}/decision-sessions/analysis/diagnostics

GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/policy
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/policy/diagnostics
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/eligibility
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/eligibility/diagnostics
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/projection
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/history
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/influence
GET  /api/repositories/{repositoryId}/decision-sessions/lifecycle/health

GET  /api/repositories/{repositoryId}/decision-sessions/continuity-artifacts
GET  /api/repositories/{repositoryId}/decision-sessions/continuity-artifacts/{artifactId}

GET  /api/repositories/{repositoryId}/decision-sessions/transfers
GET  /api/repositories/{repositoryId}/decision-sessions/transfers/history
GET  /api/repositories/{repositoryId}/decision-sessions/transfers/diagnostics

GET  /api/repositories/{repositoryId}/decision-sessions/recovery
GET  /api/repositories/{repositoryId}/decision-sessions/recovery/history
GET  /api/repositories/{repositoryId}/decision-sessions/recovery/diagnostics

GET  /api/repositories/{repositoryId}/decision-sessions/workflow
GET  /api/repositories/{repositoryId}/decision-sessions/workflow/health
GET  /api/repositories/{repositoryId}/decision-sessions/workflow/influence
GET  /api/repositories/{repositoryId}/decision-sessions/workflow/summary

GET  /api/repositories/{repositoryId}/decision-sessions/certification
GET  /api/repositories/{repositoryId}/decision-sessions/certification/report
POST /api/repositories/{repositoryId}/decision-sessions/certification
```

Do not add endpoints that manually force transfer, session creation, activation, retirement, eligibility override, or policy override.

## Backend Composition

Update `src/CommandCenter.Backend/Program.cs`:

- Add `using CommandCenter.DecisionSessions.Extensions;`
- Call `builder.Services.AddDecisionSessions();` after `AddReasoning()` and before `AddWorkflow()`.
- Call `app.MapDecisionSessionEndpoints();` near other repository-scoped domain endpoints.

`AddDecisionSessions()` registers:

- File-system repository.
- Registry.
- Recovery.
- Metrics.
- Token estimator.
- Economics.
- Coherence.
- Lifecycle policy.
- Transfer eligibility.
- Continuity artifact service.
- Transfer.
- Continuity capture and integration.
- Observability.
- Certification.
- Hosted recovery after Stage 3 recovery is implemented.

## Tauri And UI Scope

Backend endpoints are the required integration surface. Do not add manual UI controls for lifecycle management.

If read-only desktop visibility is later required, add Tauri commands in `src/CommandCenter.Shell/src/main.rs`, TypeScript API functions under `src/CommandCenter.UI/src/api`, and TypeScript types under `src/CommandCenter.UI/src/types`. Those additions must remain read-only except for certification run commands.

## Test Plan

Add focused xUnit files:

- `DecisionSessionFoundationTests.cs`
- `DecisionSessionRepositoryTests.cs`
- `DecisionSessionRegistryTests.cs`
- `DecisionSessionAnalysisTests.cs`
- `DecisionSessionMetricsTests.cs`
- `DecisionSessionEconomicsTests.cs`
- `DecisionSessionCoherenceTests.cs`
- `DecisionSessionLifecyclePolicyTests.cs`
- `DecisionSessionTransferEligibilityTests.cs`
- `DecisionSessionContinuityArtifactTests.cs`
- `DecisionSessionTransferTests.cs`
- `DecisionSessionRecoveryTests.cs`
- `DecisionSessionObservabilityTests.cs`
- `DecisionSessionWorkflowIntegrationTests.cs`
- `DecisionSessionCertificationTests.cs`
- `DecisionSessionEndpointTests.cs`

Use `MemoryArtifactStore` for deterministic service tests where possible. Use temporary directories for file-system persistence tests that need actual directory behavior.

Test categories:

- Domain and state transitions.
- Repository-scoped persistence.
- Unsupported schema version rejection.
- Cross-repository payload rejection.
- Unsafe id/path rejection.
- Active-session invariant.
- Derived snapshot rebuild.
- Deterministic metrics/economics/coherence/policy.
- TTL and cache miss risk.
- Transfer eligibility.
- Continuity artifact validation.
- Transfer ordering and invariants.
- Recovery from restart, missing snapshots, corruption, and interrupted transfer.
- Read-only observability and workflow integration.
- Certification pass/fail behavior.
- Endpoint status mapping for not found, bad request, and conflict.

## Validation Commands

Run after each implementation stage:

```powershell
dotnet test .\CommandCenter.slnx
```

Run when backend endpoints change:

```powershell
dotnet test .\tests\CommandCenter.Backend.Tests\CommandCenter.Backend.Tests.csproj
```

Run when UI or Tauri read-only surfaces are added:

```powershell
cd .\src\CommandCenter.UI
npm run build
npm run test
```

## Implementation Order

1. Add project, solution reference, primitives, models, JSON/path helpers, DI extension, and foundation tests.
2. Implement repository, registry, validation, diagnostics, and basic read-only endpoints.
3. Complete Stage 2A: metrics, statistics, TTL, cache miss risk, diagnostics, and rebuildable metrics snapshots.
4. Complete Stage 2B: economics, cache benefit/risk economics, diagnostics, and rebuildable economics snapshots.
5. Complete Stage 2C: coherence, fragmentation, density, transfer pressure, diagnostics, and rebuildable coherence snapshots.
6. Add lifecycle policy and rebuildable policy snapshots.
7. Add transfer eligibility and eligibility diagnostics.
8. Add first-class continuity artifacts and validation.
9. Add transfer execution with continuity artifact integration and transfer history.
10. Harden recovery, startup hosted recovery, recovery history, and derived snapshot rebuilds.
11. Add observability projections, influence trace, lifecycle history, and health.
12. Integrate read-only workflow and repository summary consumption.
13. Add certification service, reports, and end-to-end fixture.

## Completion Definition

The implementation is complete when:

- Decision-session lifecycle code is isolated in `CommandCenter.DecisionSessions`.
- The new project has no execution dependency.
- At most one active decision session can exist per repository.
- Governance session analysis includes metrics, economics, coherence, TTL, and cache miss risk.
- Metrics, economics, coherence, policy evaluations, eligibility checks, observability, and reports are rebuildable.
- Policy decisions are deterministic and explainable.
- Transfer eligibility prevents unsafe transfer execution and explains blocked or deferred transfer.
- Transfer creates and uses a first-class continuity artifact.
- Transfer preserves continuity and never creates parallel active sessions.
- Recovery survives restart, missing snapshots, duplicate-active corruption, and interrupted transfer states with diagnostics.
- Workflow and repository projections consume lifecycle state without mutating it.
- Certification proves authority boundaries, analysis determinism, policy determinism, eligibility, continuity artifact correctness, transfer correctness, recovery correctness, workflow consumption, diagnostics, health, and the active-session invariant.
- All relevant backend tests pass.
