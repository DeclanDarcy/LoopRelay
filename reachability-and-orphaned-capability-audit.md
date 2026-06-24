# Reachability & Orphaned Capability Audit — CommandCenter

**Date:** 2026-06-24
**Scope:** `src/CommandCenter.*` (8 .NET projects, Rust/Tauri shell, React UI) — operational reachability, not reference counting.
**Method:** Endpoint route inventory (158 routes) → consumer surface (92 Tauri commands + direct `fetch`) → DI registration map → hosted-service entrypoints → internal call graph. Every claim below is backed by a code location.

---

## How this application is actually reached

```
React UI  ──invokeCommand('cmd')──▶  Rust shell (main.rs, 92 #[tauri::command])  ──HTTP──▶  .NET sidecar (127.0.0.1:5000)  ──▶  Endpoint  ──▶  Service  ──▶  Domain
                                          │
UI ──fetch()──────────────────────────────┴───────────────────────────────────────────────▶  (only 2 routes: execution status + event stream)
```

**Critical architectural fact:** the .NET backend is a localhost-only sidecar (`start_backend()` in `main.rs`; CORS limited to dev origins). It has **no external consumer**. The *only* ways into it are (a) one of the **92 Tauri commands** in `src/CommandCenter.Shell/src/main.rs`, (b) **2 direct `fetch`/`EventSource` calls** in the UI (`src/api/execution.ts`, `src/api/executionEvents.ts`), or (c) one of the **4 hosted services** that run in-process. Therefore **an HTTP endpoint with no Tauri command, no direct fetch, and no hosted-service caller is operationally orphaned** — it is registered and routable but nothing in the shipped product can invoke it.

The backend exposes **158 routes**. The shell + UI consume **~78** of them. **~80 routes (≈51%) are operationally orphaned.**

---

## Executive Summary

### Endpoint Summary
| Metric | Count |
|---|---|
| Total routes | 158 |
| Consumed (Tauri command or direct fetch) | ~78 |
| **Operationally orphaned (no consumer)** | **~80** |

Orphaned breakdown: **Workflow 28** (entire group), **Decision Sessions 31** (entire group), **Decisions 18** (lifecycle actions), **Planning 1**, **Artifacts 1**, **Execution events 1**.

### Service Summary
| Class | Notes |
|---|---|
| Reachable via UI path | Repository, Artifact(+rotation), Continuity, OperationalContext, Decision context/review/refinement/resolution/governance/quality/certification/influence, Reasoning (all), Execution session/monitoring, Git, Planning (via workspace) |
| Reachable via **background hosted service only** | `WorkflowProjectionService`, `WorkflowStateMachineService`, `WorkflowContinuationService`, `WorkflowPreparationService`, `WorkflowRecoveryService`, `DecisionSessionRecoveryService`, `ExecutionSessionService.RecoverAsync` |
| **Reachable only through orphaned endpoints (effectively unreachable)** | All Decision-Session analytics/lifecycle/transfer/certification services; Workflow `GateCatalog`/`Health`/`Report`/`Certification` + 6 granular projection facet services |
| Test-only / Unreferenced | **None confirmed** (every registered service has a production caller) |

### Workflow Summary
- **Reachable workflows:** continuation (timer), preparation, recovery (startup) — they execute in the **background** via hosted services and persist state.
- **Unreachable workflows:** every workflow *read/report/health/gate/certification* surface (28 endpoints) — the engine runs but **its output is never shown to a user**.

### Lifecycle Summary
- **Reachable:** execution-session lifecycle (start/accept/reject/commit/push), operational-context lifecycle (generate/review/accept/reject/promote), decision *resolve*, and all three *recovery* paths (decision-session, execution-session, workflow) via hosted services.
- **Implemented but unreachable:** decision **candidate** lifecycle (discover→promote/dismiss/expire/duplicate), decision **proposal** lifecycle (generate/expire/discard + review transitions + notes + revisions), decision **post-resolution** lifecycle (supersede/archive), decision-**session** lifecycle (transfer/eligibility/observability/certification), and all workflow governance surfaces.

### Highest-Priority Remediation (ordered)
1. **Decision lifecycle actions are unreachable (F1)** — the decision pipeline looks complete but its state-advancing verbs (promote candidate→proposal, supersede, archive, proposal review transitions) have no command. Highest functional impact.
2. **Entire Decision-Session subsystem is unreachable (F2)** — 31 endpoints, ~12 services, recovery hosted service; only startup recovery runs. Large appears-operational subsystem with no UI.
3. **Entire Workflow read/governance surface is unreachable (F3)** — 28 endpoints; engine runs headless in background.
4. **Planning / Artifacts / Execution-events redundant endpoints (F4–F6)** — low-risk duplicates.

---

## Findings

### F1 — Decision lifecycle action endpoints (Critical)
**Type:** Endpoint group + Lifecycle
**Reachability:** Unreachable (no Tauri command, no fetch, not referenced anywhere in the UI — verified by negative grep of every candidate command verb).
**Entry path:** Would require a Tauri command in `main.rs`; none exists.
**Consumer path:** Nobody. `list_decision_candidates` and `build_decision_context` exist, so candidates can be *listed/built*, but none of the state-advancing actions can be triggered.
**Evidence:** `src/CommandCenter.Backend/Endpoints/DecisionEndpoints.cs` defines, with no corresponding command in `main.rs`:

| Route | Service method | Reachable? |
|---|---|---|
| GET `…/decisions/context/snapshots` | `IDecisionContextService.ListSnapshotsAsync` | ✗ |
| POST `…/decisions/discover` | `IDecisionDiscoveryService.DiscoverAsync` | ✗ |
| POST `…/decisions/candidates/{id}/promote` | `DecisionDiscoveryService.PromoteCandidateAsync` | ✗ |
| POST `…/decisions/candidates/{id}/dismiss` | `…DismissCandidateAsync` | ✗ |
| POST `…/decisions/candidates/{id}/expire` | `…ExpireCandidateAsync` | ✗ |
| POST `…/decisions/candidates/{id}/duplicate` | `…MarkCandidateDuplicateAsync` | ✗ |
| POST `…/decisions/candidates/{id}/proposals` | `IDecisionGenerationService.GenerateProposalAsync` | ✗ |
| POST `…/decisions/proposals/{id}/expire` | `…ExpireProposalAsync` | ✗ |
| POST `…/decisions/proposals/{id}/discard` | `…DiscardProposalAsync` | ✗ |
| POST `…/proposals/{id}/review/viewed` | `IDecisionReviewService.MarkProposalViewedAsync` | ✗ |
| POST `…/proposals/{id}/review/needs-refinement` | `…MarkProposalNeedsRefinementAsync` | ✗ |
| POST `…/proposals/{id}/review/ready-for-resolution` | `…MarkProposalReadyForResolutionAsync` | ✗ |
| GET / POST `…/proposals/{id}/notes` | `…ListReviewNotesAsync` / `AddReviewNoteAsync` | ✗ |
| GET `…/proposals/{id}/revisions` | `IDecisionRefinementService.ListProposalRevisionsAsync` | ✗ |
| GET `…/proposals/{id}/revisions/{rid}/comparison` | `…GetProposalRevisionComparisonAsync` | ✗ |
| POST `…/decisions/{id}/supersede` | `IDecisionResolutionService.SupersedeDecisionAsync` | ✗ |
| POST `…/decisions/{id}/archive` | `…ArchiveDecisionAsync` | ✗ |

**Risk:** The decision domain *appears* to support a full candidate→proposal→review→resolution→supersede/archive lifecycle, but a user cannot advance a candidate to a proposal, run discovery, transition a proposal through review, or supersede/archive a decision. The backing services are fully implemented and unit-tested; the capability is real but inert. `GenerateProposalAsync` in particular is the bridge between discovery and the (reachable) review/refinement UI — without it the reachable half of the pipeline has no live input.
**Recommendation:** **Wire Up** — add Tauri commands (and UI affordances) for the verbs the product is expected to support; **Document/Remove** any deliberately deferred (e.g. notes, revisions comparison).

---

### F2 — Decision-Session subsystem (High)
**Type:** Endpoint group + Service + Projection + Lifecycle (entire `CommandCenter.DecisionSessions` surface)
**Reachability:** Unreachable from UI. The UI has **no decision-session feature** (no component/hook/api references `decision-session`). Zero of the 31 routes have a Tauri command.
**Entry path:** Only `DecisionSessionRecoveryHostedService` (StartAsync, startup) exercises `IDecisionSessionRecoveryService`.
**Consumer path:** All 31 endpoints in `DecisionSessionEndpoints.cs` are orphaned. The services they front are reached *only* through those endpoints:
- Analytics: `IDecisionSessionMetricsService`, `IDecisionSessionEconomicsService`, `IDecisionSessionCoherenceService`
- Lifecycle: `IDecisionSessionLifecyclePolicy`, `IDecisionSessionTransferEligibilityService`, `IDecisionSessionObservabilityService`
- Transfer: `IDecisionSessionTransferService` (→ `IDecisionSessionContinuityCaptureService`, `IDecisionSessionContinuityIntegrationService`)
- Continuity artifacts: `IDecisionSessionContinuityArtifactService`
- Certification: `IDecisionSessionCertificationService`
- Workflow projection: `IWorkflowDecisionSessionService` (also fronted by 4 orphaned `…/decision-sessions/workflow*` routes)

**Evidence:** `src/CommandCenter.Backend/Endpoints/DecisionSessionEndpoints.cs` (31 routes); all services registered in `src/CommandCenter.DecisionSessions/Extensions/ServiceCollectionExtensions.cs` (lines 14–33); no `main.rs` command matches `/decision-sessions`.
**Risk:** A substantial, fully-DI-wired subsystem (sessions registry, economics/coherence analytics, transfer + continuity capture, lifecycle policy, observability, certification) is dead weight at runtime except for startup recovery. It compiles, is tested, and persists nothing a user can ever see or act on. High maintenance cost with zero delivered value.
**Recommendation:** **Wire Up** if decision-session analytics/transfer is a product goal (add commands + UI), else **Remove** or formally **Document as dormant** the analytics/lifecycle/transfer/certification services. Keep `DecisionSessionRecoveryHostedService` (legitimate startup infrastructure).

---

### F3 — Workflow read / governance endpoints (Medium-High)
**Type:** Endpoint group + Service + Projection
**Reachability:** Endpoints Unreachable from UI; underlying engine Reachable via background hosted services.
**Entry path:** `WorkflowContinuationHostedService` (timer) drives `IWorkflowContinuationService` + `IWorkflowPreparationService`; `WorkflowRecoveryHostedService` (startup) drives `IWorkflowRecoveryService`. These transitively exercise `WorkflowProjectionService` and `WorkflowStateMachineService`.
**Consumer path:** All **28** routes in `WorkflowEndpoints.cs` lack a Tauri command. The UI's "WorkflowRail"/"GitWorkflowPanel" components render *execution* state, not these `/workflow/*` projection endpoints (verified: no command, no fetch).
**Services reachable ONLY through these orphaned endpoints (built-but-never-surfaced):** `IWorkflowGateCatalogService`, `IWorkflowHealthService`, `IWorkflowReportService` (repository/progression/human-governance/readiness reports), `IWorkflowCertificationService`, and the 6 granular projection facets `IWorkflowExecutionService` / `IWorkflowHandoffService` / `IWorkflowDecisionService` / `IWorkflowOperationalContextService` / `IWorkflowGitService` / `IWorkflowDecisionSessionService`.
**Evidence:** `src/CommandCenter.Backend/Endpoints/WorkflowEndpoints.cs`; hosted services in `src/CommandCenter.Workflow/Services/Workflow{Continuation,Recovery}HostedService.cs`; injection sites confirmed in `WorkflowCertificationService.cs:13`, `WorkflowContinuationService.cs:11`, `WorkflowGateCatalogService.cs:8`, `WorkflowHealthService.cs:10`, `WorkflowPreparationService.cs:17`, `WorkflowRecoveryService.cs:11`, `WorkflowReportService.cs:10`.
**Risk:** The workflow engine advances and recovers state headlessly, but **none of its diagnostics, timeline, gates, health, reports, or certification are ever displayed** — operators are blind to the automation running underneath. `POST …/workflow/recover` also duplicates what the recovery hosted service already does at startup.
**Recommendation:** **Wire Up** the projection/diagnostics/timeline/health/reports endpoints (high observability value for an autonomous workflow engine); **Keep As Internal Infrastructure** the continuation/recovery services; **Document** the redundant manual `/recover`.

---

### F4 — `GET …/planning` endpoint (Low)
**Type:** Endpoint
**Reachability:** Endpoint orphaned; **service reachable**.
**Evidence:** `PlanningEndpoints.cs` → `IPlanningService` has no Tauri command, but `IPlanningService` is injected by `RepositoryProjectionService.cs:23` and `OperationalContextGenerationService.cs:19`, so milestone/readiness data reaches the UI through the **workspace projection** (`get_repository_workspace`). The standalone endpoint is redundant.
**Risk:** Low — duplicate exposure path.
**Recommendation:** **Remove** the standalone endpoint or **Document** it as a debug/alternate read.

---

### F5 — `GET …/repositories/{id}/artifacts` (Low)
**Type:** Endpoint
**Reachability:** Orphaned; redundant. Returns `IRepositoryProjectionService.GetWorkspaceAsync`, identical to the consumed `…/workspace` route (`get_repository_workspace`). No command targets the bare `/artifacts` GET.
**Recommendation:** **Remove** (duplicate of `/workspace`).

---

### F6 — `GET …/execution-sessions/{id}/events` (non-stream) (Low)
**Type:** Endpoint
**Reachability:** Orphaned; superseded. The UI consumes `…/events/stream` via `EventSource` (`src/api/executionEvents.ts`) and `…/status` via `fetch`. The paginated non-stream `GetEventsAsync` route has no consumer.
**Recommendation:** **Keep As Internal Infrastructure** (useful for debugging/replay) or **Document**; not harmful.

---

## Explicit Non-Findings (correctly NOT reported as orphaned)

- **4 hosted services** — all registered and valid operational entrypoints: `DecisionSessionRecoveryHostedService` (AddDecisionSessions), `ExecutionSessionRecoveryHostedService` (AddExecution), `WorkflowContinuationHostedService` + `WorkflowRecoveryHostedService` (AddWorkflow). *Not orphaned.*
- **Internal infrastructure with no direct endpoint caller but live production callers** (verified): `IDecisionReasoningCaptureService` (called by Decision/Execution/OperationalContext endpoints), `IUnderstandingDiffService` / `IUnderstandingCompressionService` / `IDecisionAnalysisService` (→ `OperationalContextGenerationService`), `IDecisionArtifactProjectionService` (→ many decision services), `IReasoningArtifactProjectionService` (→ `FileSystemReasoningRepository` — initially suspected dead, **confirmed reachable**), `IDecisionContextProjectionService`, `IHumanAuthoringBurdenService`, `IDecisionQualitySignalService`, `IWorkflowStateMachineService`, `IDecisionSessionEvidenceReader`, repositories/stores/parsers/`IProcessRunner`/`IExecutionProvider`. These are DTOs-adjacent plumbing and are intentionally not flagged.
- **`IPlanningService`** — reachable via workspace projection (only its standalone endpoint is redundant; see F4).

---

## Appendix — Reachability ledger (route group → consumer)

| Endpoint group | Routes | Consumed | Orphaned | Consumer |
|---|---|---|---|---|
| Ping | 1 | 1 | 0 | `ping_backend` |
| Repositories | 5 | 5 | 0 | repository commands |
| Artifacts | 5 | 4 | 1 | content/rotate commands (F5) |
| Planning | 1 | 0 | 1 | none (service via workspace, F4) |
| OperationalContext | 7 | 7 | 0 | operational-context commands |
| Continuity | 3 | 3 | 0 | continuity commands |
| Execution | 3 | 3 | 0 | preview/start/active |
| ExecutionSessions | 6 | 5 | 1 | session/status/stream/accept/reject (F6) |
| Git | 4 | 4 | 0 | git commands |
| Decisions | ~57 | ~39 | 18 | review/refine/resolve/governance/quality/cert commands (F1) |
| Reasoning | 24 | 24 | 0 | reasoning commands |
| **Workflow** | **28** | **0** | **28** | none (F3) |
| **DecisionSessions** | **31** | **0** | **31** | none (F2) |
| **Total** | **158** | **~78** | **~80** | |

*Direct UI `fetch`/`EventSource` (not via Tauri): `GET …/execution-sessions/{id}/status`, `GET …/execution-sessions/{id}/events/stream`.*
