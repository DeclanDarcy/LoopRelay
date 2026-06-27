# Command Center — Vision Realization Audit

Status: architectural audit
Date: 2026-06-26
Question answered: **Given everything already built, what is the highest-cohesion evolutionary architecture that realizes the intended product (`design.md`) with the greatest reuse of proven assets?**

Inputs treated as authoritative: the current implementation (`src/`), its domain/authority graph, the certified governance/contract/regression infrastructure (`.agents/`, `tests/`, `docs/`), and the design proposal (`design.md`) as the intended destination. This is not a gap analysis and not a divergence report. Where the implementation differs from the vision, the question is *what does the difference reveal about the best evolution*, not *where is the mistake*.

A companion document, `refactor-readiness.md`, answers a narrower adjacent question (*where should the current roadmap stop*). This audit subsumes and generalizes it: it describes the target evolutionary architecture, not the stopping point.

---

## Executive Assessment

**The current architecture is unusually well-positioned for this vision, because the vision's hardest-to-retrofit property is already structurally true.** The governing invariant — *a Decision Session and an Operational session are distinct roles that must never reference each other's orchestration* — is not something the design has to introduce. It already holds in the dependency graph: `CommandCenter.DecisionSessions` references `Core`, `Decisions`, `Reasoning`, and `Continuity`, but **not** `Execution`. The two roles are already separated; the design's job is to give the separated decision role a *real codex process* without breaking that separation.

The mechanism for doing that safely is also already practiced. The design's "new singleton orchestrator" that composes both roles is not a new architectural pattern — it is a specialization of a composition role that `CommandCenter.Workflow` (references both `Execution` and `DecisionSessions`) and `CommandCenter.Middle` (references both, plus everything else) already perform. Composition-without-cross-reference is an established habit of this codebase, not an invention.

Consequently the vision decomposes into a small amount of genuinely new capability sitting on a large amount of proven foundation:

- **One genuinely new runtime primitive** — a persistent/interactive codex process — concentrates essentially all the technical risk.
- **One load-bearing relocation** — extracting `Execution`'s process/stream/handoff machinery into a role-agnostic `CommandCenter.Agents` runtime — which is a no-behavior-change refactor that *strengthens* the existing invariant.
- **One authority inversion at a single seam** — `CommandCenter.Decisions` moves from *computing* decisions to *validating/falling back/parsing* them, with codex as the live authority.
- **Everything else is reuse**: the router, transfer machinery, SSE streaming, handoff rotation, the entire contract/governance/regression safety net, the domain authority of every backend context, and a large fraction of the UI.

Rough readiness: **the backend domain model, the contract/governance/regression infrastructure, and the streaming/handoff/recovery runtime are foundational and survive largely intact (~70–75% of what the vision needs already exists in reusable form).** The genuinely new architecture is narrow and concentrated: a persistent agent process, the per-repository orchestrator that holds it, and a forward-driving UI surface.

---

## Architectural Strengths to Preserve

These are the highest-value investments. Each continues to create value because the vision *consumes* it rather than reshaping it.

1. **The role-separation invariant in the dependency graph.** `DecisionSessions` not referencing `Execution` is the single most valuable structural fact for this vision. It means the design's hardest correctness property is free. Preserve it absolutely — the extraction of `CommandCenter.Agents` must keep it true (both roles depend on `Agents`; neither depends on the other).

2. **The decision-session router and transfer machinery** (`IDecisionSessionLifecyclePolicy.EvaluateAsync` → `Continue`|`Transfer`, `IDecisionSessionTransferEligibilityService` → `Eligible`|`Blocked`|`Deferred`|`NotApplicable`, `DeterministicTokenEstimator`, `IDecisionSessionContinuityArtifactService`). The design's §9 reuse-vs-transfer logic is this machinery, almost unchanged. It already models exactly the decision the vision needs; it currently governs an abstraction, and the evolution simply points it at a real process.

3. **The streaming substrate** (`ExecutionMonitoringService`'s `Channel`-based multi-subscriber broadcast + the `text/event-stream` SSE endpoints + the client `useExecutionEvents` hook). The vision needs three streams (planning, execution, decision); this is one proven stream generalized threefold. Preserve the observer→channel→SSE shape verbatim.

4. **Handoff rotation and `.agents` IO** (`IArtifactStore`, `HandoffService`'s 4-digit `handoff.{NNNN}.md` rotation). All of the vision's `.agents` writes (specs, plan, operational_context, decisions, operational_delta, handoff history) route through this. Preserve the store and the rotation convention.

5. **Orphan-recovery as a runtime habit** (`ExecutionSessionRecoveryHostedService`, `DecisionSessionRecoveryHostedService`, `WorkflowRecoveryHostedService`, `WorkflowContinuationHostedService`). The vision's long-lived processes need disposal/recovery on crash and shutdown. The codebase already runs recoverable, long-lived background services — the orchestrator extends this established pattern rather than inventing process-lifecycle management.

6. **The contract/governance/regression safety net** (M0.2 contract oracle, M1.1 canonical contract model, M1.2 generation pipeline, M0.3 regression framework, M0.4 decision governance; `ContractGenerationSupport`, the `ContractFixtures`, `ArchitecturalDecisionGovernanceTests`, `ArchitecturalRegressionFrameworkTests`). This is the single highest-leverage transition asset. Every new contract the vision introduces — three SSE stream shapes, the `executionState` repo-state additions, the structured decision JSON schema — can be *born generated and drift-protected*, and the new session-role invariant can be *ratified through certified governance and enforced as an executable regression test*. The vision does not need a new safety net; it inherits one.

7. **The deterministic `CommandCenter.Decisions` model** (`DecisionProposal`, option templates, weighted-sum scoring, quality assessment). Even as codex takes over live authority, this model survives with three durable jobs: the **parse target** for codex's structured output, the **test oracle** for non-deterministic codex paths, and the **offline fallback** when the codex path is flagged off. Its value changes shape; it does not diminish.

8. **The backend domain authority of every bounded context.** Decisions, DecisionSessions, Execution, Workflow, Reasoning, Continuity are mature, owned, and singleton-composed under one host. The vision leans entirely on this domain model and invents no new domain authority — it adds a runtime and a surface on top of a settled core.

9. **The UI inspection surface** (the 7 `WorkspaceTabs` — workspace, execution, operational-context, governance, decisions, reasoning, continuity — over `useShellState`, plus `SelectedRepositorySummary`, `ExecutionEventFeed`, `GeneratedHandoffReviewPanel`). This is a deep, working *inspection* product. The vision adds a *driving* surface in front of it; the tabs become the depth behind the flow, not throwaway work.

---

## Architectural Evolution Opportunities

Places where existing architecture naturally extends — small adjustments that unlock large capability.

- **`Execution`'s runtime trio wants to become role-agnostic.** `ProcessRunner` + `CodexExecutionProvider` + `ExecutionMonitoringService` are already written without decision-specific or execution-specific semantics in their core — they spawn a process, stream it, and observe exit. They are *accidentally* operational-only because they live in `Execution`. Relocating them into `CommandCenter.Agents` requires no behavior change and immediately makes the decision role able to run codex. This is the highest-leverage small move in the entire evolution.

- **The router is already shaped for a live process.** `IDecisionSessionLifecyclePolicy` takes a `repositoryId` and returns `Continue`/`Transfer` from a token-count input. Today the token count is a `(len+3)/4` estimate over a transcript abstraction; the only change the vision needs is to feed it the *live* process's token count. The decision interface does not change — its input source does. The estimator stays as the fallback.

- **`Middle` is the natural home for operational-context lifecycle.** `IOperationalContextGenerationService` already owns cross-cutting context generation and already depends on `Execution`, `DecisionSessions`, and the rest. The vision's `operational_context.md` / `operational_delta.md` / `UpdateOperationalContext` / transfer-payload mechanics belong here, not in a new context. An existing composition layer expands to own context *evolution*, not just context *generation*.

- **`Workflow`'s continuation loop prefigures the decision loop.** `WorkflowContinuationHostedService` + `IWorkflowExecutionService`/`IWorkflowHandoffService`/`IWorkflowStateMachineService` already model "run a turn → produce a handoff → decide whether to continue." The vision's Execute→handoff→decision→Submit→continue loop is a more interactive instance of the same state machine. The orchestrator can reuse `Workflow`'s milestone extraction and handoff orchestration rather than re-deriving them.

- **The generated-contract pipeline wants its second through Nth consumers.** M1.2 proved the IR→TypeScript→freshness→consumer-verification pipeline on exactly one projection (`repository-dashboard`). The vision introduces several new contracts; each is a natural next application of a pipeline that is *built but barely exercised*. Finishing the in-flight `repository-dashboard` generated-consumer migration (retiring the manual `types/repositories.ts` wrapper) is the clean path through which the `executionState` repo-state additions land — because that is the very contract the vision modifies first.

- **`Core` wants a generated prompt library.** `Lib.Prompts` is a proven mechanism (PoC-verified) with no prompts yet in `Core`. Adding the 11 `.prompt` files under `Core/Prompts/` is a zero-risk, fully-isolated capability that every codex call depends on. It can ship entirely ahead of the runtime work.

---

## Runtime Evolution

The runtime undergoes the largest evolution, but along a single well-defined axis: **from stateless request/response to a stateful, per-repository orchestrated runtime that holds long-lived agent processes across requests.**

- **Today.** Endpoints are stateless. Each codex invocation is a one-shot: `codex exec --cd <repo> -`, prompt written once, `ProcessRunner` closes stdin after the first write, stream to exit, `SupportsReattach => false`. Background work (recovery, continuation) runs in hosted services, but no process is *held open* for interactive multi-turn use.

- **The one genuinely new primitive.** A **persistent/interactive codex process** that accepts multiple sequential prompts on stdin and streams each turn to a turn-complete signal without closing the channel. This does not exist anywhere and cannot be `codex exec -`. It is the load-bearing prerequisite and the concentration point of all runtime risk. The preferred mechanism is a codex MCP/app-server session *owned by `CommandCenter.Agents`*, exposed as an `IAgentProcess` with `WritePromptAsync`, a per-turn stream, `TurnCompleted`, and disposal — keyed by session id in a process registry.

- **The runtime composition object.** A **singleton, per-repository orchestrator** holds the open plan-authoring process and the open decision-session process, the run state (cached plan, current handoff, current decisions, iteration counter), and the three SSE channels. It is effectively a hosted singleton whose process handles outlive any single request — a direct extension of the existing recovery-hosted-service pattern, now managing *interactive* rather than *recoverable-batch* processes.

- **Two configuration axes on the agent spec.** The runtime gains an `AgentSessionSpec { Role, Effort, SandboxProfile, WorkingDirectory, SessionId }`: two effort tiers (ExtraHigh, Medium) and two sandbox profiles (Operational = `workspace-write`; Decision = most-restrictive `read-only` + approvals `never` + no MCP/tools), both delivered as codex `-c` config. Neither axis exists today; both are additive enum fields on the new spec.

- **What stays.** The streaming substrate, the recovery hosted-service pattern, the session JSON stores, and handoff rotation all carry forward. The runtime *grows a stateful interactive layer*; it does not replace its batch-and-stream core.

**Trajectory:** `CommandCenter.Execution`'s process/stream/handoff machinery → `CommandCenter.Agents` (role-agnostic agent runtime) → plus a new persistent-process capability → consumed by a per-repository orchestrator hosted singleton. A thinner `Execution` remains as the *operational consumer* of `Agents`; `DecisionSessions` becomes the *decision consumer* of the same `Agents`.

---

## Product Evolution

The product's center of gravity shifts from **inspection** to **drive-then-inspect**, and the existing surface becomes the depth behind a new flow rather than the whole product.

- **Today's mental model is post-hoc inspection.** A repository is selected; seven workspace tabs let the user *observe* what has happened — execution sessions, decisions, reasoning, continuity, governance. The user reacts to state (`executionState` drives UI mode: Idle → Executing → AwaitingCommit → AwaitingHandoffReview). The product answers "what is the state of this repository's work?"

- **The vision adds a forward-driving spine.** When `!File.Exists(.agents/plan.md)`, a **Plan Authoring** surface replaces the dashboard: author a roadmap + specs, generate and iteratively revise a plan via a held-open codex process, then Execute into a sustained **decision loop** where each operational handoff feeds a zero-permission decision process that proposes user-editable decisions. The product now answers "drive this repository's work forward, one governed decision at a time."

- **The two halves compose; they don't compete.** The Plan Authoring + decision-loop surface is the *driving* half; the seven existing tabs are the *inspection* half. The natural product architecture is a lifecycle where the driving surface is primary while a repository is pre-plan or mid-loop, and the inspection tabs are the always-available depth (the `ExecutionEventFeed`, `GeneratedHandoffReviewPanel`, decision/reasoning/continuity views) the user drops into for detail. The repository lifecycle gains two new states (`PlanAuthoring`, `ExecutingPlan`) that gate *which half is foregrounded*.

- **Navigation implication.** `WorkspaceTabs` + `useShellState` already switch primary surfaces by repository and tab. The Plan Authoring/decision-loop screen is a new primary surface conditioned on lifecycle state — the same conditional-by-state rendering the shell already performs, extended with a state that points at the new surface. The streaming hook generalizes from one execution stream to three stream kinds behind one reusable live-view component.

- **Continuity of experience.** The handoff-review and git-workflow surfaces (`GeneratedHandoffReviewPanel`, `GitWorkflowPanel`) embody a *human-acceptance gate* the vision relocates — from accepting a handoff to **submitting an edited decision**. The UX investment in "review a generated artifact, edit, accept" transfers directly to the decision Submit step.

**Trajectory:** an inspection product → a lifecycle product where authoring and a decision loop drive the repository forward and the existing tabs become inspection depth. Existing UI investment evolves by re-rooting under a new primary flow, not by restarting.

---

## Domain Evolution

How each major subsystem evolves. Responsibilities that remain, expand, narrow, or relocate.

- **`Core`** — *expands.* Gains a generated prompt library (`Core.Prompts.*` via `Lib.Prompts`) and the repository lifecycle states (`PlanAuthoring`, `ExecutingPlan`). Remains the leaf foundation; the additions are additive and dependency-free.

- **`Execution`** — *narrows and donates.* Its process/stream/handoff/recovery machinery relocates into `CommandCenter.Agents`. What remains is the *operational* semantics: git/commit/push, the operational session lifecycle, operational handoff completion — now a consumer of `Agents` rather than the owner of the process model. The `AwaitingAcceptance` gate narrows (the vision's flow bypasses it; `HandoffService`'s state side-effect must not fire on the orchestrator's rotation path).

- **`Decisions`** — *inverts authority.* From the live authority that computes decisions (templates + weighted-sum scoring) to the **validator/oracle/fallback** behind a codex live authority. `DecisionProposal` becomes the parse target for codex's structured output; the scoring services become the deterministic oracle and offline fallback. Same model, inverted role.

- **`DecisionSessions`** — *expands into a runtime consumer.* Today a non-operational lifecycle/transfer/economics layer that *cannot run codex* (no `Execution` reference, by design). After the `Agents` extraction it gains the ability to run codex *through `Agents`*, preserving its non-reference to `Execution`. Its router and transfer machinery become the live governor of a real process. This is the context that gains the most capability while keeping its boundary.

- **`Reasoning`** — *largely stable.* Reasoning graph/threads/certification continue to materialize over decisions and sessions. As decision authorship moves to codex, reasoning's input becomes codex-authored decisions; the subsystem itself is unchanged. A future opportunity is reasoning-over-the-decision-loop, but nothing is forced.

- **`Continuity`** — *stable, more central.* The continuity artifact becomes the literal transfer payload (`operational_delta.md` + rewritten `operational_context.md`) seeding a fresh decision session, rather than an audit record. Its role sharpens from record-keeping to *active state hand-off*.

- **`Middle`** — *expands to own context lifecycle.* `IOperationalContextGenerationService` becomes the home for operational-context *evolution* (delta production, context rewrite on transfer), not just generation. A natural fit, since it already composes all contexts and depends on `Execution`.

- **`Workflow`** — *feeds the orchestrator.* Milestone extraction, handoff orchestration, state machine, and the continuation loop feed the Execute pipeline and the decision loop. `Workflow` either hosts the orchestrator or the orchestrator reuses its services; its continuation pattern is the loop's precursor.

- **`Backend`** — *gains the runtime composition.* Hosts the orchestrator singleton, the three SSE endpoints, the plan-status/write/revise/execute/submit endpoints, and the memory cache. Remains the sole composition root — the only place allowed to compose both session roles.

- **`UI` / `Shell`** — *re-roots under a driving flow.* New Plan Authoring + decision-loop primary surface; three stream consumers behind one reusable live view; the Tauri/Rust passive-transport boundary (`invokeCommand`, `shell-transport-classification.md`) extends to relay the new commands without gaining domain logic.

---

## Authority Evolution

How authority boundaries evolve while preserving semantic ownership. The vision moves authority in exactly three places and preserves it everywhere else.

1. **Decision authority inverts: deterministic → codex-live, with `Decisions` retained as validator/oracle/fallback.** This is the most consequential authority change. `CommandCenter.Decisions` ceases to be the source of the live decision and becomes the *schema, the oracle, and the fallback*. Semantic ownership of "what a valid decision is" stays in `Decisions` (the `DecisionProposal` shape); ownership of "what the decision should be, here, now" moves to a codex process governed by `DecisionSessions`. Preserve the model; relocate the *liveness*.

2. **Process/runtime authority extracts: `Execution` → `CommandCenter.Agents`.** Authority over "spawn, stream, hold, and dispose an agent process" moves out of the operational context into a role-agnostic runtime that both roles depend on. This *strengthens* rather than erodes boundaries: `DecisionSessions` gains codex without ever gaining a path to `Execution`'s orchestration. The operational *semantics* (git/commit/push) stay in `Execution`; only the generic process plumbing leaves.

3. **A new orchestration authority appears — and must be bounded.** The per-repository orchestrator gains authority over cross-request run state and process handles. The critical constraint: **orchestration authority must remain composition, not domain.** It may hold processes and sequence calls; it must *not* absorb decision semantics, contract shapes, or handoff rules — those stay owned by `DecisionSessions`, the contract oracle, and `Workflow`/`HandoffService` respectively. The orchestrator is the conductor, not a new domain.

Unchanged authority (preserve as-is): backend domain authority for every bounded context; the router's authority over reuse-vs-transfer; `IArtifactStore`'s authority over `.agents` IO; the contract oracle's authority over contract shape; M0.4 governance's authority over architectural decisions; the Shell's passive-transport authority (it relays, it does not decide).

---

## Shared Infrastructure Evolution

Infrastructure that should become common runtime capability rather than context-local code.

- **Agent runtime (the centerpiece).** `ProcessRunner` + `CodexExecutionProvider` generalize into `CommandCenter.Agents`: role-agnostic process spawn, streaming, handoff plumbing, recovery, plus the new persistent/interactive capability and the `AgentSessionSpec` (role/effort/sandbox). This is the one piece of infrastructure whose *generalization* is the architectural keystone — both session roles become consumers of one runtime.

- **Process lifecycle.** The recovery-hosted-service pattern generalizes from "recover orphaned batch sessions at startup" to "own, supervise, and dispose long-lived interactive processes across their full lifetime." One supervision model serves both.

- **Streaming.** `ExecutionMonitoringService`'s channel/observer/SSE becomes a shared multi-kind stream service (planning/execution/decision) behind one reusable client live-view. The shape is already generic; only the number of stream kinds grows.

- **Prompts.** `Lib.Prompts`-generated `Core.Prompts.*` becomes the shared, type-safe prompt surface every codex call uses — replacing any ad-hoc prompt strings with generated `Render(...)`/`Text` members.

- **Artifact management.** `IArtifactStore` + handoff rotation generalize to all `.agents` writes (specs, plan, context, delta, decisions, handoff history) under one store and one rotation convention — with a single, explicit decision about *who* rotates (a dedicated orchestrator rotation that does not trigger `HandoffService`'s `AwaitingAcceptance` side-effect).

- **Routing / session lifecycle.** The router + transfer-eligibility + continuity-artifact services generalize from governing an abstraction to governing a real process, with real token accounting replacing the deterministic estimate (estimate retained as fallback).

- **Memory.** A new shared `IMemoryCache` (`AddMemoryCache`, `{repositoryId}:Plan`) — small, additive, run-scoped — becomes the cross-request run-state cache the stateless model never needed.

- **Contracts.** The generation pipeline becomes the *default birthplace* for every new contract (endpoints, repo states, decision schema), turning a proven-once mechanism into routine shared infrastructure.

---

## Implementation Dependency Graph

Prerequisite relationships between major architectural initiatives. **This is a dependency structure, not a roadmap** — no sequencing, sizing, or milestones implied.

```
INDEPENDENT FOUNDATIONS (no prerequisites; can proceed in parallel)
├─ A. Generated prompt library in Core (Lib.Prompts + 11 .prompt files)
├─ B. Ratify the session-role invariant through M0.4 governance
├─ C. Finish repository-dashboard generated-consumer migration (retire manual wrapper)
└─ D. Extract CommandCenter.Agents from Execution (no behavior change)
        │  preserves: DecisionSessions ⊥ Execution; both → Agents
        ▼
GATING SPIKE (depends on D)
└─ E. Persistent/interactive codex process  ◀── concentrates all runtime risk
        │     (codex MCP/app-server session owned by Agents)
        ▼
RUNTIME COMPOSITION (depends on E)
├─ F. Per-repository orchestrator (hosted singleton; holds open processes + run state)
│       depends additionally on: router (exists), IArtifactStore (exists), memory cache (new), git (exists)
└─ G. Decision-session-drives-codex (DecisionSessions consumes Agents)
        │     depends on A (prompts), D (Agents), E (persistence)
        │     inverts: Decisions authority → codex; Decisions → oracle/fallback
        ▼
GOVERNED SURFACES (depend on F and/or G)
├─ H. Router wired to the live process + real token accounting        (F, G)
├─ I. Structured decision JSON schema → DecisionProposal parse target  (G, C)
├─ J. Repo lifecycle states (PlanAuthoring, ExecutingPlan) + contract regen  (F, C)
├─ K. Three SSE streams + plan endpoints (status/write/revise/execute/submit)  (F)
└─ L. UI: Plan Authoring + decision-loop surface (re-root under driving flow)   (K, J)

CROSS-CUTTING (span the whole graph)
├─ Feature-flag every codex path; deterministic/offline path is the default until certified
├─ Contract regeneration for each new shape (states, endpoints, decision schema)  ── leans on C
└─ Test strategy split: deterministic Decisions services as oracle/fixture; codex paths via integration only
```

Critical-path observations: **A, B, C, and D have no prerequisites and unblock the most**; D is a no-behavior-change refactor that can ship alone; E is the sole gating spike and everything *live* waits on it; the orchestrator (F) and decision-session-drives-codex (G) are the two pillars the governed surfaces (H–L) all hang from. The contract migration (C) is a quiet prerequisite of three downstream initiatives (I, J, and the contract-regen cross-cut) because the repo-state and decision-schema contracts the vision adds are most safely born through the same pipeline.

---

## Strategic Risks

Risks introduced by the *evolution*, each with why it exists, likely impact, and an architectural mitigation.

1. **Persistent-codex infeasibility (highest).** *Why:* `codex exec -` is one-shot/non-interactive; the held-open multi-turn capability is unproven. *Impact:* the plan-authoring and decision-session loops cannot run as designed; the router degrades to transfer-only. *Mitigation:* validate the codex MCP/app-server session early as an isolated spike inside `Agents`; design the degraded path (one-shot re-seeded planning revise; per-turn one-shot decisions forcing transfer-only) as a first-class fallback, not an afterthought.

2. **Orchestration becoming a new domain authority (high).** *Why:* the orchestrator naturally accretes — once it holds processes and run state, it is tempting to let it own decision semantics, handoff rules, or contract shapes. *Impact:* a god-object that erodes every context's semantic ownership and re-couples the roles it was meant to keep separate. *Mitigation:* constrain it by construction to *composition only* (hold processes, sequence calls, route SSE); forbid domain logic by keeping it dependent on — never a reimplementation of — `DecisionSessions`, the contract oracle, and `HandoffService`. Encode the boundary as an M0.3 regression test.

3. **Authority erosion of `Decisions` (medium-high).** *Why:* demoting `Decisions` from live authority to oracle/fallback risks it bit-rotting until the fallback no longer works and the oracle no longer matches. *Impact:* loss of the deterministic safety path and the test oracle exactly when codex non-determinism makes them most needed. *Mitigation:* keep the deterministic services on a live, tested code path (the offline/flag-off default), and make the codex JSON schema *converge to* `DecisionProposal` so the oracle stays meaningful.

4. **Lifecycle fragmentation — two handoff-rotation owners (medium-high).** *Why:* `HandoffService` rotates handoffs *and* transitions state to `AwaitingAcceptance`; the orchestrator needs rotation *without* that gate. *Impact:* double-rotation, mis-transitioned state, or a silent `AwaitingAcceptance` that stalls the loop. *Mitigation:* a single explicit decision — a dedicated orchestrator rotation that reuses the `{NNNN}` format and `IArtifactStore` but not `HandoffService`'s state side-effect; never two owners of the same artifact's lifecycle.

5. **Long-lived process leakage (medium-high).** *Why:* held-open processes must be disposed on cancel, shutdown, error, and crash; the current model never holds processes across requests. *Impact:* orphaned codex processes, leaked file handles, zombie sessions. *Mitigation:* make the orchestrator a hosted singleton that extends the existing recovery-hosted-service supervision to interactive processes — recovery and disposal are an existing competency, applied to a new process kind.

6. **Runtime coupling / `Agents` as a god-runtime (medium).** *Why:* a shared runtime tends to absorb role-specific concerns (git for operational, sandbox quirks for decision). *Impact:* the role-agnostic guarantee weakens, re-coupling the two roles through their shared runtime. *Mitigation:* keep `Agents` strictly generic (spawn/stream/hold/dispose + spec); operational semantics (git/commit/push) stay in `Execution`; the sandbox/effort differences live in `AgentSessionSpec` data, not in runtime branches.

7. **Product fragmentation between driving and inspecting (medium).** *Why:* a new primary flow bolted beside seven existing tabs can read as two products. *Impact:* a fractured experience where the user cannot tell when to drive vs inspect. *Mitigation:* root both halves in the repository lifecycle state — the driving surface is foregrounded by `PlanAuthoring`/`ExecutingPlan`, the tabs are the always-reachable depth — so one lifecycle, not two products.

8. **Contract drift on repo-state change (medium).** *Why:* adding `PlanAuthoring`/`ExecutingPlan` touches `repository-dashboard`'s `executionState`, the exact contract mid-migration. *Impact:* a forked half-generated/half-manual contract. *Mitigation:* land the state additions *only after* the generated-consumer migration (C) completes, through the generated pipeline, drift-protected by freshness/consumer verification.

9. **Test-strategy evolution under non-determinism (medium).** *Why:* codex-authored plans/decisions are not byte-stable; golden-file assertions break. *Impact:* either brittle snapshot tests or an untested codex path. *Mitigation:* split the strategy — deterministic `Decisions` services remain the golden/oracle path; codex paths are exercised through integration tests only; the structured JSON schema gives a stable assertion surface even when prose varies.

10. **Outward-facing auto-commit/push without a gate (medium).** *Why:* the Execute pipeline commits and pushes automatically. *Impact:* hard-to-reverse, outward-facing side effects fired without confirmation. *Mitigation:* confirm this is intended; if a gate is wanted, reuse the existing `GitWorkflowPanel` acceptance pattern rather than inventing a new one.

11. **Over-centralization vs unnecessary compatibility layers (low-medium).** *Why:* the temptation to either centralize everything in the orchestrator or to preserve every current surface behind a shim. *Impact:* either a brittle center or a thicket of temporary adapters. *Mitigation:* prefer evolving surfaces in place (re-root the UI, extend the router) over compatibility shims; the only deliberate adapter is the feature flag, which is temporary by construction and independently revertible.

---

## Vision Readiness Assessment

**How much of the existing implementation becomes foundational.** The large majority. The entire backend domain model (Decisions, DecisionSessions, Execution, Workflow, Reasoning, Continuity), the streaming/handoff/recovery runtime, the router and transfer machinery, the contract/governance/regression safety net, and the UI inspection surface all carry forward as foundation. Approximately **70–75% of what the vision requires already exists in directly reusable form.**

**Where genuine architectural investment is still required.** Three places, and only three:
- **The persistent/interactive codex process** — the one true greenfield capability and the concentration of all runtime risk.
- **The per-repository orchestrator** — a new composition object (a hosted singleton holding processes and run state across requests), built on patterns the codebase already practices but not previously assembled this way.
- **The forward-driving UI surface** — Plan Authoring + the decision loop, a new primary flow that re-roots the existing inspection surface beneath it.

**Which parts of the vision are already largely enabled by existing work.**
- Role separation (DecisionSessions ⊥ Execution): *already structurally true.*
- Reuse-vs-transfer routing: *already implemented; needs a live input.*
- Streaming for three surfaces: *one proven stream, generalized.*
- `.agents` IO and handoff rotation: *exists; needs one rotation-ownership decision.*
- The safety net for every new contract and the new invariant: *built and certified; needs application.*
- Type-safe prompts: *mechanism proven; needs the files added.*

**Which parts represent genuinely new architectural capability.**
- Holding a codex process open for interactive multi-turn use (new primitive).
- Cross-request, per-repository stateful orchestration of long-lived agent processes (new composition object).
- Codex as the live decision authority with the deterministic model demoted to oracle/fallback (authority inversion).
- A driving, lifecycle-rooted product surface in front of the inspection surface (product reshaping).

**Verdict.** The vision is *evolutionarily reachable with high reuse*. Its hardest correctness property is free, its safety net is already built, and its domain model is settled. The work that remains is narrow and concentrated — one runtime primitive, one orchestrator, one UI flow — rather than broad. The optimal evolutionary path is therefore not a rewrite and not an incremental feature plan: it is the **extraction of a shared agent runtime, the proving of a single new process primitive, and the composition of both into a per-repository orchestrator and a driving surface — with every new contract and the new invariant born into the existing governance net.** The architecture is ready to evolve; the risk lives almost entirely in one spike, and the leverage lives almost entirely in what already exists.
