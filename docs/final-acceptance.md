# Final Acceptance — Phase 12 (Deferred Non-Goals and Final Definition of Done)

This is the capstone certification for the CommandCenter refactor (`.agents/plan.md`, milestones m0–m12). The
milestone file is named `m12-adaptive-engineering-intelligence.md`, but its title and body are **"Phase 12 —
Deferred Non-Goals and Final Definition of Done"** (a stale filename, as with m8 and m11; the body is
authoritative). m12 builds no features. Its bar is faithfulness: prove every Definition-of-Done acceptance
criterion is satisfied by the **implemented** system, and prove every explicit non-goal was genuinely **not**
built.

## Verification method

A five-lens read-only audit verified each criterion against real source, followed by an adversarial
default-refute pass that hunted for a reason each "met" claim was actually false (missing step, wrong prompt,
broken order, UI inferring authority, a non-goal that *was* built, a tautological guarantee). Outcome:

- 14 Final Acceptance criteria: **14 met** (under default `OrchestrationFeatureFlags`; see FA-5 nuance).
- 6 Non-Goals: **6 honored** (correctly absent / repository-scoped, not platform-wide; see NG-2/NG-3 nuances).
- Adversarial refute pass: **0 implementation defects.** Two precision findings (FA-5 flag-gating, FA-14 a
  verifier miscount) are recorded below as faithful nuances, not defects.

The full per-criterion evidence (file + line anchors) is the audit record; the anchors below are the primary
landing points. Line numbers are indicative of the audited revision and may drift; the symbol names are stable.

## Final Acceptance criteria

| # | Criterion | Verdict | Primary evidence |
|---|-----------|---------|------------------|
| FA-1 | No `.agents/plan.md` ⇒ Plan Authoring is shown | met | `App.tsx` gate `!planStatus.planExists || isAuthoringSessionActive`; `RepositoryOrchestrator.GetPlanStatusAsync`; `PlanStatusEndpoints` |
| FA-2 | Epic and Specs written to `.agents/specs` | met | `RepositoryOrchestrator.BeginWritePlanAsync` → `PersistPlanInputsAsync` writes `epic.md` + `s{n}.md` **before** the turn |
| FA-3 | Write Plan uses the correct generated prompt; creates `.agents/plan.md` | met | `BuildWritePlan` renders `WritePlan.Text`; `FinishPlanningTurnAsync` verifies `plan.md` |
| FA-4 | Revise Plan uses `RevisePlan.Render(feedback)` in the same planning process | met | `BeginRevisePlanAsync` → `RevisePlan.Render(feedback)` on the held-open planning `IAgentSession` (warm-session reuse) |
| FA-5 | Execute Plan: close planning, copy operational context, cache plan, extract milestones, commit/push, set `ExecutingPlan`, start execution, rotate `handoff.0001.md` — in order | met *(default flags)* | `BeginExecutePlanAsync` → `RunExecutionAsync` runs the nine steps in order; **commit/push is gated by `AutomaticCommitPushAfterExecuteEnabled` (default ON)** — see nuance |
| FA-6 | Decision session starts in a separate zero-permission Codex process | met | `EnsureDecisionSessionAsync`/`BuildDecisionSpec`: read-only sandbox, no network, approvals never, distinct decision gate; `Decision_session_uses_a_read_only_zero_permission_sandbox` |
| FA-7 | `GetNextDecisions.Render(handoff)` streams proposals; editable after completion | met | `RunDecisionAsync` streams to `delta`, emits `review-ready{decisions}` only after the turn completes; nothing persisted until submit |
| FA-8 | Submit persists edited decisions and runs `ContinueExecution.Render(plan, handoff, decisions)` | met | `BeginSubmitDecisionsAsync` persists `decisions.000N.md` + canonical `decisions.md`, then `RunContinuationAsync` → `ContinueExecution.Render(plan, handoff, decisions)` |
| FA-9 | Each continuation produces and rotates the next handoff | met | `RunContinuationAsync` verifies the live handoff, computes the next sequence from disk (`NextHandoffSequenceAsync`), rotates to `handoff.000N.md` under the `continued` guard |
| FA-10 | Router `Continue` reuses the warm Decision process | met | `RouteNextDecisionRunAsync` → `Evaluate` = Continue → `BeginDecisionRunAsync` reuses the held-open session (no close) |
| FA-11 | Router `Transfer`: write delta, rewrite operational context, fresh process, resume streaming | met | Transfer mechanics: `ProduceOperationalDelta.Text` → write `operational_delta.md` → `CloseDecisionSessionAsync` → `UpdateOperationalContext.Text` → fresh session → `StartDecisionSessionFromTransfer.Render(newContext)` → propose |
| FA-12 | All prompt text comes from generated `CommandCenter.Core.Prompts` classes | met | `PromptAuthorityTests.Production_source_does_not_duplicate_canonical_prompt_text` (in `ArchitectureLayeringTests.cs`); 11 `.prompt` templates |
| FA-13 | Execution and DecisionSessions reach Codex only through `CommandCenter.Agents` | met | `ArchitectureLayeringTests` (11 layering methods + `DecisionRuntime_cannot_depend_on_…_m5`); both roles inject `IAgentRuntime`; Orchestration is the sole composition root |
| FA-14 | Full certification commands and contract/governance suites pass | met | Cert commands `.agents/plan.md`:178–199; suites present: `ArchitectureLayeringTests`, `OrchestrationGovernanceTests`, `FinalAcceptanceTests`, `BackendEndpointDispositionTests`, `ContractVerification/*`, `ProcessLeakDetectionTests`, `OrchestrationDeterministicFallbackTests`, `OrchestrationSnapshot/StreamContractTests` |

### Faithful nuances (not defects)

1. **FA-5 — the ordered guarantee is the default-flag behavior.** The eight-step order holds exactly when
   `OrchestrationFeatureFlags.AutomaticCommitPushAfterExecuteEnabled` is `true` (its default). When the flag is
   `false` — m10 **rollback path 4**, the documented commit/push off-switch — the publish step and its
   `committed` frame are skipped and the run proceeds from milestone extraction straight to `StartExecution`.
   This is the designed rollback lever, not a contract break: the acceptance criterion describes the default
   build, and the off-switch is governed in `docs/orchestration-loop-governance.md`.

2. **FA-14 — `ArchitectureLayeringTests` has 11 layering methods** (plus the separate `PromptAuthorityTests`
   class), not the "8" an audit note first stated. All cited suites exist and are discoverable; the count
   correction is recorded here for faithfulness.

## Non-Goals (must be absent / out of scope)

| # | Non-Goal | Status | Evidence |
|---|----------|--------|----------|
| NG-1 | No Repository Knowledge platform in this flow | honored | The only conversation surface is `ConversationProjection` — an append-only narrow transcript (`Planning/OperationalOutput/DecisionOutput/Submit/Continuation`) read via `GET …/conversation`; no indexing, query, or cross-repository aggregation |
| NG-2 | No adaptive engineering intelligence, opportunity discovery, recommendation generation, or platform-wide learning | honored | The loop adds none; see near-miss note |
| NG-3 | No knowledge graph, lineage explorer, repository query surface, or trend analysis | honored | The loop adds none; see near-miss note |
| NG-4 | UI does not infer lifecycle legality, decision validity, router behavior, prompt selection, or artifact authority | honored | `api/planning.ts`/`api/decisionRuntime.ts` send only commands (epic/specs/`newCodebase`/feedback/edited decisions); reducers transition on backend-published events; route is **received** via `event.route`, never decided |
| NG-5 | Prompts are not semantic authority; they remain generated communication mechanisms | honored | `.prompt` files are pure text templates generated via Lib.Prompts `PromptSourceGenerator`; `PromptAuthorityTests` forbids hand-composed prompt bodies in production |
| NG-6 | Orchestrator is not a domain service for Execution, Decisions, Continuity, Git, Workflow, or contracts | honored | `RepositoryOrchestrator` owns only transient run state and delegates to `IAgentRuntime`, `IArtifactStore`, `IPlanArtifactPublisher` (over `IGitService`), and `IDecisionSessionRouter` |

### Faithful near-misses (NG-2 / NG-3)

The non-goals prohibit adding **platform-wide / cross-repository** intelligence to **this flow**. Three
pre-existing, **repository-scoped** subsystems use adjacent vocabulary but are neither platform-wide nor wired
into the orchestration loop:

- `CommandCenter.Decisions` — `DecisionDiscoveryService.DiscoverAsync(Guid repositoryId)` and
  `RecommendationService` provide per-repository decision *support* (the documented deterministic/offline
  fallback subsystem, not the live loop authority). Both are scoped by `repositoryId`; neither performs
  cross-repository learning or opportunity discovery.
- `CommandCenter.Reasoning` — `ReasoningGraphService` (`GetGraphAsync`/`TraceBackward`/`TraceForward`, all by
  `repositoryId`) is a per-repository reasoning lineage view, a separate concern (the `reasoning-*` docs), not
  a cross-repository knowledge graph or global query surface.
- `CommandCenter.Continuity` — `CompressionTrend` records metrics for a single operational-context proposal,
  not trend analysis across repositories.

**The orchestration loop adds none of these and consumes none of them.** This is verified structurally:
`CommandCenter.Orchestration`'s compiled manifest references `Agents`, `Core`, `Continuity`, `Decisions`,
`DecisionSessions`, and `Execution` — and **does not reference `CommandCenter.Reasoning`** — and its source
contains zero references to `ReasoningGraphService`, `DecisionDiscoveryService`, or `RecommendationService`. The
`FinalAcceptanceTests.Orchestration_loop_does_not_absorb_the_reasoning_or_knowledge_subsystem` guard pins this
boundary so a future change that folds the reasoning/knowledge subsystem into the loop fails the build graph.

## Completion Statement — satisfied

> The design is complete when the user can stay on one repository screen from initial epic/spec authoring
> through repeated decision-mediated execution turns, with persistent planning and decision Codex processes
> where required, faithful artifact writes under `.agents`, generated prompt provenance for every agent turn,
> and router-driven reuse or transfer of the active Decision process.

Each clause maps to verified behavior: the single-screen flow (FA-1, the `isAuthoringSessionActive` mount latch,
m9's in-place lifecycle); persistent planning + decision processes (FA-4, FA-6, FA-10; held-open
`CodexAppServerSession`); faithful `.agents` artifact writes (FA-2, FA-5, FA-8, FA-9, `OrchestrationArtifactPaths`);
generated prompt provenance per turn (`PromptProvenance` recorded for every planning/operational/decision/transfer
turn); and router-driven reuse or transfer (FA-10, FA-11; the registry-free `DecisionSessionRouter`).

## Governance pin

`tests/CommandCenter.Backend.Tests/Orchestration/FinalAcceptanceTests.cs` pins the four m12-specific boundaries
no earlier milestone test guarded: the five-method Completion-Statement command surface, the non-goal isolation
from the reasoning/knowledge subsystem, the compositional delegation (NG-6), and the completeness of the
generated eleven-prompt catalog (FA-12 / NG-5). It is a pure unit class (no host boot), so it stays outside the
`ProcessEnvironment` serialized collection.

## Verification

`dotnet test tests/CommandCenter.Backend.Tests` — full backend suite green; see `docs/refactor-plan-status`
memory for the run count. m12 is additive-test + documentation only (no prompt, Rust, UI, wire, or
orchestration production-code change), so the m8 contract goldens, the three m8-frozen UI type files, and the UI
suite are untouched and remain green by construction.
