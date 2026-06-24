# audit-01.md — Governed Workflow Orchestration Architectural Audit

> Architecture archaeology + capability discovery + roadmap recovery.
> The repository is authoritative. Where it conflicts with `.agents/plan.md`, the repository wins.
> Generated 2026-06-24 against branch `dev` @ `800963d`.

---

# Audit Overview

## Roadmap Purpose

`.agents/plan.md` ("Governed Workflow Orchestration Implementation Plan") set out to add a
**coordination layer** that observes the full repository work cycle —
`WorkSelection → Execution → Handoff → Decision → OperationalContext → Commit → Push → Completed` —
without ever owning domain authority.

Intended capabilities:

- Project current workflow stage and blocking gate from existing domain evidence.
- Model a canonical transition graph and a unified gate catalog mapping human authority
  checkpoints to existing domain commands.
- Persist derived, disposable workflow evidence under `.agents/workflow`, recoverable from domains.
- Mechanically advance between non-authority stages (continuation) and request reviewable
  artifacts via existing domain commands (preparation), halting at every human gate.
- Report health/progression/governance/readiness and certify the above with evidence.
- Surface all of this through a Tauri bridge and a dedicated UI workflow workspace.

Core assumption: **workflow is derived and never authoritative.** Domains keep lifecycle truth;
if workflow evidence conflicts, domains win and workflow rebuilds.

## Current Architectural Reality

Command Center is a four-runtime system: `.NET` backend sidecar (`CommandCenter.Backend`),
React/TS UI (`CommandCenter.UI`), Rust/Tauri shell (`CommandCenter.Shell`), with domain logic
split across `Core`, `Execution`, `Decisions`, `Continuity`, `Reasoning`, and `Middle`.

The roadmap's central deliverable **exists and is substantial**: `src/CommandCenter.Workflow`
is a ~94-file project containing all 15 planned service contracts (each DI-registered and
implemented), the full planned primitive/model set, and several **unplanned** seams. It is wired
into the backend (`AddWorkflow()`, `MapWorkflowEndpoints()`, both hosted services) and exposes a
near-complete repository-scoped HTTP API.

- **Authority boundaries:** honored with unusual discipline. `CommandCenter.Workflow.csproj`
  references only `Core`, `Execution`, `Decisions`, `Continuity` — **not** `Middle`. Continuation
  is mechanical-only (invokes no domain command); preparation is the sole domain-command invoker
  and degrades to no-op when domain services are absent (optional/nullable injection).
- **Lifecycle ownership:** unchanged — Execution/Decisions/Continuity/Git remain owners.
  Workflow observes, projects, recovers, explains, certifies.
- **Persistence model:** `IArtifactStore`/`ArtifactPath` + `FileSystemWorkflowRepository`
  writing JSON+MD evidence to `.agents/workflow/*` **at runtime only**. The directory does not
  exist in the working tree (correct: evidence is derived/disposable, runtime-generated).
- **Orchestration model:** state-machine graph + gate catalog + recovery-first startup
  (`WorkflowRecoveryHostedService`) + optional background continuation
  (`WorkflowContinuationHostedService`, **disabled by default**).
- **Certification model:** a `WorkflowCertificationService` producing pass/fail findings, plus an
  end-to-end fixture — but certification lives as **one consolidated test class**, not the planned
  15 suites, and **has no committed evidence artifacts** (runtime-only).

**The defining reality:** the coordination *engine* is essentially done and exceeds the plan; the
*human-facing surface* (Tauri bridge, dedicated UI workspace, workspace summary) was never built.
**The workflow HTTP API currently has zero live consumers outside the test suite.**

---

# Architectural Compatibility Review

Classification is by **capability presence**, independent of milestone-execution status.
Evidence paths are repo-relative.

## Milestone 0 — Workflow Coordination Foundation

- **Intended Capability:** Read-only projection deriving current stage/gate from execution,
  decision, continuity, and git evidence; deterministic; non-mutating.
- **Original Assumptions:** Stage is purely derivable; identical inputs → identical projection.
- **Current Reality:** `IWorkflowProjectionService`/`WorkflowProjectionService` compose sub-projections
  + state-machine diagnostics into a master read model. `WorkflowInstance`, `WorkflowStage` (with
  added `Unknown/Blocked/Failed` sentinels), `WorkflowProjectionDiagnostics` all present.
- **Compatibility:** **Already Implemented.**
- **Evidence:** `src/CommandCenter.Workflow/Services/WorkflowProjectionService.cs`,
  `Primitives/WorkflowStage.cs`, `tests/.../WorkflowProjectionServiceTests.cs`.
- **Completion:** **100%** (backend).
- **Required Adjustments:** Keep as-is.
- **Notes:** Stage enum is richer than the roadmap's implied happy-path — terminal/sentinel states
  were a sound addition.

## Milestone 1 — Workflow State Machine

- **Intended Capability:** Evaluate valid/blocked transitions and next stages without advancing.
- **Assumptions:** Linear graph; gates block transitions; no authoritative persistence.
- **Current Reality:** `WorkflowStateMachineService` with canonical 7-stage graph,
  `WorkflowTransition`/`WorkflowTransitionResult`, and a **19-value** `WorkflowBlockingCondition`
  set far more granular than planned.
- **Compatibility:** **Already Implemented.**
- **Evidence:** `Services/WorkflowStateMachineService.cs`, `Primitives/WorkflowBlockingCondition.cs`.
- **Completion:** **100%.** Keep as-is.
- **Notes:** A single graph model serves validation, next-stage discovery, *and* continuation —
  a reuse seam (D0001) the roadmap treated as separate concerns.

## Milestone 2 — Persistence & Recovery

- **Intended Capability:** Durable-but-disposable evidence; domain artifacts win; startup recovery.
- **Assumptions:** Workflow view rebuildable; fingerprints detect divergence.
- **Current Reality:** `FileSystemWorkflowRepository`, `WorkflowRecoveryService` (+ hosted),
  `WorkflowFingerprint`, `WorkflowHistoryProjection`, `WorkflowRecoveryResult` (unplanned).
- **Compatibility:** **Already Implemented.**
- **Evidence:** `Services/WorkflowRecoveryService.cs`, `WorkflowRecoveryHostedService.cs`,
  `Persistence/WorkflowArtifactPaths.cs`.
- **Completion:** **100%.** Keep as-is.
- **Notes:** **Best-effort startup recovery** (D0003): a per-repository recovery failure must not
  disable unrelated repositories — an error-isolation seam absent from the plan's mechanism.
  Fingerprints derive from **normalized, canonically ordered** evidence, becoming the universal
  idempotency key reused everywhere.

## Milestone 3 — Gate Catalog

- **Intended Capability:** Unify human authority checkpoints; deterministic gate→command map;
  catalog never executes.
- **Assumptions:** Gates open/satisfy only from domain evidence; map to existing command names.
- **Current Reality:** `WorkflowGateCatalogService`, `WorkflowGate`, `WorkflowGateStatus`,
  `WorkflowGateEvidence`, plus unplanned `WorkflowGateCatalogProjection`/`WorkflowGateHistoryProjection`.
  `WorkflowGateType` **splits operational-context into Review + Promotion** (richer than planned).
- **Compatibility:** **Already Implemented.**
- **Evidence:** `Services/WorkflowGateCatalogService.cs`, `Primitives/WorkflowGateType.cs`.
- **Completion:** **100%.** Keep as-is.
- **Notes:** Decision-resolution gate binds to the **existing** `resolve_decision_proposal`
  command (D0004) — logged explicitly as "a roadmap refinement, not a deviation."

## Milestones 4–8 — Execution / Handoff / Decision / Operational-Context / Git Integration

- **Intended Capability:** Make workflow aware of each domain's state while that domain stays
  authoritative; never launch, accept, resolve, promote, commit, or push.
- **Current Reality:** Each has a dedicated service + projection + status enum + diagnostics:
  `WorkflowExecutionService`, `WorkflowHandoffService`, `WorkflowDecisionService`,
  `WorkflowOperationalContextService`, `WorkflowGitService` (all in `Services/`), with matching
  `Workflow*Projection`/`Workflow*Status`/`Workflow*Diagnostics` models and `WorkflowCompletionEvaluation`.
- **Compatibility:** **Already Implemented** (M4–M7); **M8 Partially Compatible** (see note).
- **Evidence:** `Services/Workflow{Execution,Handoff,Decision,OperationalContext,Git}Service.cs`,
  corresponding `Models/*`.
- **Completion:** M4–M7 **100%**, **M8 ~90%.** Keep as-is.
- **Notes (M8):** `WorkflowGitStatus.PushSkipped` is **modeled but intentionally never inferred**
  (D0008/D0013/D0023/D0024). Legitimate push-skip completion is deferred until Execution/Git emit
  domain-owned push-skip evidence. This is the one acknowledged behavioral gap inside the engine.
- **Notes (M6):** Decisions exposed no deeper non-authority proposal-generation seam (D0018); the
  plan's assumption of a richer preparation surface had to be narrowed to existing commands.

## Milestone 9 — Continuation Engine

- **Intended Capability:** Mechanically advance between non-authority stages; introduce separately
  governed artifact **preparation**; halt at every gate; full idempotency; gated hosted rollout.
- **Assumptions:** Timelines/events derived and rebuildable; open gates yield `WaitingForHuman`.
- **Current Reality:** `WorkflowContinuationService` (mechanical, invokes **no** domain command)
  and `WorkflowPreparationService` (**genuinely invokes** `IDecisionDiscoveryService`,
  `IDecisionGenerationService`, `IOperationalContextGenerationService`, `IExecutionSessionService`).
  Four-outcome preparation vocabulary `Allowed/Refused/Skipped/Duplicate` (D0016). Per-concern
  fingerprints (`WorkflowContinuationFingerprint`, `WorkflowPreparationFingerprint`) provide
  idempotency. `WorkflowContinuationHostedService` reuses endpoint services as a thin scheduler.
- **Compatibility:** **Already Implemented** (with a deliberate deferral).
- **Evidence:** `Services/WorkflowContinuationService.cs`, `WorkflowPreparationService.cs`,
  `WorkflowContinuationHostedService.cs`, `Models/WorkflowContinuationOptions.cs`.
- **Completion:** **~90%.** Keep as-is.
- **Notes:** Hosted continuation is **shipped but disabled** — `CommandCenter:Workflow:ContinuationEnabled`
  defaults `false` (D0021). The continuation/preparation separation is implemented exactly as the
  plan's architecture rules demand; "allowed ≠ executed" is honored.

## Milestone 10 — Certification

- **Intended Capability:** Prove correctness/recovery/authority-preservation via a certification
  service, four reports, scenario coverage, and an end-to-end fixture.
- **Current Reality:** `WorkflowCertificationService` + `WorkflowCertificationResult`/`Finding`;
  all four reports (`RepositoryWorkflowReport`, `WorkflowProgressionReport`, `HumanGovernanceReport`,
  `WorkflowReadinessReport`) via `WorkflowReportService`; `WorkflowHealthService` + `WorkflowInfluenceTrace`.
  The end-to-end fixture `EndToEndWorkflowFixtureValidatesProgressionGatesRecoveryReportsAndCertification`
  exercises the full lifecycle with gate-halts, idempotent restart, and recovery.
- **Compatibility:** **Partially Compatible / Requires Refactor (test shape + surface).**
- **Evidence:** `Services/WorkflowCertificationService.cs`, `WorkflowReportService.cs`;
  `tests/CommandCenter.Backend.Tests/WorkflowProjectionServiceTests.cs` (~108–113 `[Fact]/[Theory]`).
- **Completion:** **~80%** (backend certification present; surface + planned granularity absent).
- **Required Adjustments:** **Replace** the planned 15-suite test layout (see Obsolete Work) and
  **Add** the missing UI/e2e certification.
- **Notes:** Certification is real but **monolithic** — one integration-style class rather than 15
  isolated suites — and produces **no committed evidence** (runtime-only). `GET .../workflow/certification/reports`
  (the certification *list* endpoint) appears absent; only GET+POST `/workflow/certification` exist.

## Plan Sections Beyond Milestones — UI / Bridge / Workspace Summary

These were first-class roadmap deliverables (the "UI Plan", "Tauri Bridge Updates", "Workspace
Projection Integration" sections), not numbered milestones, and are **the audit's primary gap**.

- **Tauri Bridge (27 commands):** **0/27 present.** `src/CommandCenter.Shell/src/main.rs` registers
  ~92 commands (execution, decisions, reasoning, continuity, git, artifacts) but **no** `*_workflow_*`
  command. **Compatibility: Missing. Completion: 0%.**
- **UI Workflow Workspace:** No `src/CommandCenter.UI/src/features/workflow/`, no `types/workflow.ts`,
  no `api/workflow.ts`, no `useWorkflow*` hooks, no dedicated `workflow` tab (the `PrimaryWorkspaceTab`
  union has no `workflow` member). Workflow surfaces only as an **execution-centric `WorkflowRail`**
  (5-step: Context→Execution→Handoff→Commit→Push) plus `GitWorkflowPanel`, both derived from
  *execution* data, not the workflow projection API. **Compatibility: Missing/Superseded-by-stub.
  Completion: ~10%.**
- **Workspace Projection Integration (`RepositoryWorkflowSummary`):** **absent** — no such type in
  the tree; `Middle` contains no workflow reference. **Compatibility: Missing. Completion: 0%.**

---

# Cross-Milestone Findings

## Already Satisfied Future Work

- The **entire backend coordination engine (M0–M9)** is implemented and certified at the integration
  level. A regenerated roadmap should treat these as a **completed baseline**, not re-plan them.
- Reusable infrastructure the plan anticipated as separate efforts is **already unified**: one
  transition graph, one fingerprint idempotency key, one `evaluate→persist→recover→certify→act`
  scaffold lifted verbatim from continuation into preparation (D0009/D0015/D0016).
- All four governance/health/readiness reports and the certification service already exist.

## Hidden Progress

- Workflow surfaced to users **indirectly and early** via `WorkflowRail`/`ExecutionWorkflowRail`/
  `GitWorkflowPanel` in the execution/workspace tabs before the dedicated workspace was built — an
  execution-shaped, 5-step approximation of the richer 8-stage backend model.
- The backend exposes 26 of the 27 planned HTTP endpoints, so the capability is **fully queryable
  today** — it simply has no caller.

## Architectural Divergence

- **Backend fidelity is high; surface fidelity is near-zero.** The split is the central finding:
  engine ≈ done + exceeds plan; bridge/UI/Middle summary ≈ not started.
- **Test shape diverged:** one consolidated `WorkflowProjectionServiceTests` (~108 cases) replaces
  the planned 15 per-service suites; UI characterization tests are execution-flavored
  (`executionWorkflow.test.ts`, `executionWorkflowRail.test.tsx`, `gitWorkflowEvidence.test.tsx`),
  not the 10 planned `workflow*Panel.test.tsx`.
- **Evidence is runtime-only.** No `.agents/workflow/*` artifacts are committed (correct by design,
  but it means the roadmap's implied "browseable certification artifacts" do not exist at rest).
- **Production reachability gap:** `api/tauri.ts` is `invoke`-only with no HTTP path for feature
  APIs; a future workflow UI cannot reach the workflow API in production until bridge commands exist.

## Obsolete Work

- The **15 separate backend test suites** in the plan's Test Plan are effectively obsolete — behavior
  is already certified by the consolidated integration class + end-to-end fixture. Re-mandating 15
  suites would be churn, not capability.
- The original **10-panel UI decomposition** predates the richer backend (gates split into
  Review/Promotion, continuation vs preparation, health dimensions, influence trace). It should be
  redesigned around what the backend actually exposes, not restored literally.

## Missing Capabilities (genuinely absent)

1. **Tauri bridge workflow commands** (0/27) — the hard blocker for any UI.
2. **Dedicated workflow UI workspace** — tab, types, api client, hooks, panels.
3. **`RepositoryWorkflowSummary`** on dashboard/workspace projections (Middle integration).
4. **UI characterization + e2e** for workflow panels.
5. **Legitimate push-skip completion** — deferred pending Execution/Git domain evidence (D0023/D0024).
6. **Hosted continuation rollout** — implemented but disabled by default; operational enablement absent.
7. **Certification *list* endpoint** (`GET .../workflow/certification/reports`).

## Refactor Requirements

- Building the workflow UI requires **first** a transport for it: either the 27 Tauri bridge
  commands in `main.rs`, or an HTTP-direct path in `api/`. This is a sequencing prerequisite, not
  an afterthought — the invoke-only `api/tauri.ts` shape currently makes the workflow API
  unreachable from a production build.
- Push-skip completion is **blocked by domain shape**: Workflow must not infer it; Execution or Git
  must emit push-skip evidence first. This is a cross-domain change, not a workflow-only task.

---

# Regeneration Guidance

## Preserve

- The **authority model** and the six **architecture rules** — they are realized in code and proven
  by certification. Domains own truth; workflow is derived, reconstructable, disposable.
- **Continuation vs preparation separation**, the **four-outcome preparation vocabulary**, the
  **gate catalog → existing-command mapping**, **fingerprint idempotency**, and **recovery-first
  startup with per-repository isolation**.
- The backend project structure and DI surface as-is.

## Replace

- The **UI Plan**: redesign around the *actual* backend surface (8-stage timeline, split
  Review/Promotion gates, continuation history, preparation history with Allowed/Refused/Skipped/
  Duplicate, health dimensions + influence trace, certification findings), not the original panel list.
- The **Test Plan**: codify the consolidated-integration + end-to-end-fixture reality as the backend
  baseline, and add UI characterization + e2e as the new frontier — drop the 15-suite mandate.

## Remove

- Re-planning of completed backend milestones **M0–M9**.
- Any assumption that workflow evidence is committed to the repository at rest.

## Add (deserve first-class roadmap status)

1. **Workflow transport milestone** — Tauri bridge commands (or HTTP-direct), unblocking the UI.
2. **Workflow UI workspace milestone** — tab, types, api, hooks, panels, dev mock, wired into
   `shellState`/`WorkspaceTabs`/`CommandPalette`/navigation.
3. **Workspace summary milestone** — `RepositoryWorkflowSummary` on dashboard/workspace projections.
4. **Push-skip evidence milestone** — cross-domain (Execution/Git emit, Workflow consumes).
5. **Hosted continuation enablement** — config rollout, operational guardrails, monitoring.
6. **UI/e2e certification** — the human-facing counterpart to backend certification.

---

# Final Assessment

## Architectural Alignment

**High** between roadmap *intent* and backend *reality* — the authority model and architecture rules
were honored with notable discipline, and the implementation discovered superior reusable seams.
**Low** at the surface layer (bridge/UI/Middle summary). Net intent fidelity: **High, with a
bounded and clearly identifiable gap.**

## Capability Completion

- Backend coordination engine: **~95%** (only push-skip + one list endpoint outstanding).
- End-to-end human-facing capability: **~55%** (the engine is done; the experience is unwired).
- **Overall: ~65%.** Command Center can already *compute and certify* the complete workflow; it
  cannot yet *show a human* the workflow through the intended UI.

## Roadmap Health

**Revise Existing Roadmap.**

- Not **Continue**: continuing as written would re-plan finished, certified backend milestones.
- Not **Regenerate**: the architecture, authority model, and ~95% of the engine are sound and built —
  discarding the roadmap would discard correct, proven design.
- **Revise**: collapse M0–M9 into a *completed and certified* baseline, and regenerate the remaining
  roadmap around the real frontier — **transport → UI workspace → workspace summary** — plus the two
  known deferrals (**push-skip completion**, **hosted continuation enablement**) and **UI/e2e
  certification**. The next roadmap is a *surfacing* roadmap, not a *building-the-engine* roadmap.
