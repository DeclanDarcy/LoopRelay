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

(See ./milestones/m1-foundation-and-registry.md)

## Stage 2: Governance Session Analysis

(See ./milestones/m2-governance-session-analysis.md)

## Stage 3: Governance Lifecycle

(See ./milestones/m3-governance-lifecycle.md)

## Stage 4: Lifecycle Observability

(See ./milestones/m4-lifecycle-observability.md)

## Stage 5: Workflow And Repository Consumption

(See ./milestones/m5-workflow-and-repository-consumption.md)

## Stage 6: Certification

(See ./milestones/m6-certification.md)

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

