# Implementation Preservation Platform — Architectural Integration Audit

**Date:** 2026-07-07
**Scope:** Entire LoopRelay codebase (`src/`, `tests/`, `docs/`, `issues/`, root plans/audits), audited against the proposed Implementation Preservation Platform: Implementation-Oriented File Classification Policy, Semantic Preservation Analysis, Knowledge/Insight Extraction, Repository Preservation Ledger, HITL Preservation Review, Implementation-First planning guidance, Repository Knowledge Distillation, and future structured knowledge extraction.
**Outcome:** Architectural observations only. No milestones, sequencing, effort estimates, or API designs. Findings use the format Observation / Why it matters / Architectural impact / Recommended architectural direction.

---

## 0. Executive Summary

Five theses govern everything below.

1. **The capability's conceptual ground is already ~80% written — in this repository.** The reasoning-framework docs define knowledge capture, materialization gating, and authority boundaries; the operational-context schema defines a Preserve/Summarize/Retire distillation doctrine; and three *working code precedents* exist: `ArtifactPromotionService` (classify → validate → promote-or-preserve-evidence), `DecisionLedger` (schema-versioned repository ledger), and `SynthesizeCompletedEpic` (free-form insight synthesis at epic archival, including per-source disposition). The platform should be an *evolution and generalization of these three precedents*, not a new parallel system.

2. **This is a two-era codebase; the capability must be built exclusively on the CLI era.** Roughly half the `docs/` corpus (architecture.md's orchestration loop, orchestration-loop-governance, architectural-capabilities, most of architectural-mechanisms, final-acceptance, compatibility-structure-governance, and the six reasoning-*.md as *implementations*) describes a retired backend/Tauri architecture whose projects (`LoopRelay.Backend`, `.UI`, `.Shell`, `.Reasoning`, `.Execution`, `.DecisionSessions`) no longer exist in `src/`. The live architecture is three disconnected CLIs (Roadmap → Plan → Main) over shared libraries (Core, Agents, Infrastructure, Projections, Permissions, Completion, Orchestration.Primitives).

3. **The capability decomposes onto existing lifecycle points; it is not one place in the architecture.** Classification belongs at the Main CLI's post-execution change-detection boundary; preservation analysis and insight extraction belong at the two existing pruning moments (transfer-time document optimization and epic-completion archival); planning guidance belongs at the prompt-context/catalog choke points; the ledger belongs in the established schema-versioned JSON persistence pattern under `.agents/`; HITL review must reuse the paused-state + `unblock` pattern because **no interactive HITL exists anywhere in the running system**.

4. **Prohibition cannot be trusted at the command-permission layer; it must be enforced post-mutation.** The execution session runs `danger-full-access`; one-shot sessions bypass the permission gateway entirely; and the shell classifier is bypassable via redirection (`echo x > NOTES.md` is auto-approved as read-only, issue 008). The deterministic preventive mechanism that *does* exist — per-operation write allowlists with snapshot/rollback (`ArtifactOperationPlan` + `ArtifactMutationTransaction`) — covers scoped artifact operations, not the main work turn. Implementation-First enforcement is therefore architecturally a **detect-classify-remediate** posture at the change boundary, plus prompt-level discouragement, plus write-allowlists where operations are already scoped.

5. **A small set of debt items are true integration prerequisites.** The completion archive flow (the prime preservation attachment point) is non-transactional, non-idempotent, and collision-prone (issues 004/005/006 — all confirmed still present). No settings channel exists from any CLI invocation to planning or execution behavior. The structured persistence helper is duplicated across assemblies. These materially block clean integration; the rest of the debt backlog does not.

---

## 1. Architectural Ground Truth

### F-1.1 The live architecture is three disconnected CLIs over shared libraries

**Observation.** Current `src/` contains eleven projects: three executables — `LoopRelay.Roadmap.Cli` (strategy: epic selection/promotion, milestone specs, ends at `MilestoneSpecsReady`), `LoopRelay.Plan.Cli` (one-shot planning pipeline: plan → adversarial review → revise → details/milestone extraction), `LoopRelay.Cli` (the unbounded execution loop: decision proposal → execution turn → handoff → commit gate → epic-completion certification) — plus shared libraries `Core` (prompt authority + artifact store), `Agents` (Codex process runtime), `Infrastructure`, `Projections`, `Permissions`, `Completion`, `Orchestration.Primitives`, and the `Prompts.Generator` analyzer. `insights.md` records the deliberate decision that the three CLIs remain disconnected. All repository-owned state lives in the `.agents/` git submodule; machine-local runtime state lives in self-gitignored `.LoopRelay/` (`DecisionSessionResumeStore.cs:123`).

**Why it matters.** Every integration decision below keys off which CLI owns which lifecycle stage, and off the `.agents/` (committed, shared) vs `.LoopRelay/` (local, ephemeral) persistence split.

**Architectural impact.** The platform cannot be "one subsystem wired into one pipeline" — it necessarily has planning-side presence (Roadmap + Plan CLIs), execution-side presence (Main CLI), and completion-side presence (Completion library used by both Main and Roadmap CLIs).

**Recommended architectural direction.** Treat the platform as a shared library in the mold of `LoopRelay.Completion` and `LoopRelay.Permissions`: owned responsibilities and persistence, invoked from the existing lifecycle points of each CLI. No fourth CLI; no standalone daemon.

### F-1.2 Staleness map — documentation that must not ground this work

**Observation.** Verified against `src/`: `docs/architecture.md` (Orchestration Loop and Command Center sections), `orchestration-loop-governance.md`, `architectural-capabilities.md`, `architectural-mechanisms.md` (except the prompt-authority mechanisms), `final-acceptance.md`, `compatibility-structure-governance.md`, and the six `reasoning-*.md` docs (as implementations) all reference projects and endpoints that no longer exist. `docs/prompt-architecture.md` is self-aware ("the retired backend loop") but its 10-prompt catalog is stale — the live catalog is 41 prompts. The maturity audit already flags this (OSR-6: "Internal docs and issue notes are stale or broader than the checked-in CLI repo").

**Why it matters.** Several stale docs describe mechanisms (decision Submit gate, execution-context preview, prompt-provenance DTOs) that a designer would naturally reach for as integration points — and they do not exist.

**Architectural impact.** The reasoning framework and governance docs remain valid as *doctrine* (rules, boundaries, gates) even though their described *implementations* are dead. They should be consumed as policy, not as architecture.

**Recommended architectural direction.** Before or alongside integration, quarantine/annotate the stale docs (as `.agents/planning-to-relay-audit.md` already recommends) so the preservation capability's own governance artifacts are written against live architecture. Note the irony: this repo's root is itself littered with autonomous non-implementation artifacts — the capability's first proving ground is its own repository.

### F-1.3 In-flight work with direct collision surface

**Observation.** Four in-flight plans overlap the capability: `epic-refactor-plan.md` (Roadmap epic file-authoring via `ArtifactPromotionService` — classification/validation/evidence-preservation of agent-written files; **highest overlap**), `plan.md` (operation-scoped "perfect permissions" replacing sandbox-copy one-shots; defines the per-operation permission-contract + mutation-transaction model), `permissions-plan.md` (the closed-world approval engine, partially shipped), and `projection-plan.md` (shared projection infrastructure, partially shipped). The pre/post-refactor plans impose the operating rule "complete only work that survives the relay architecture."

**Why it matters.** The Implementation-Oriented File Classification Policy and `ArtifactPromotionService`'s `ArtifactOutputClassification` (`ArtifactPromotion.cs:21`) are the same conceptual move — classify an agent-produced file and route it to promotion or evidence-preservation. Building a second classifier forks authority.

**Architectural impact.** The platform's classification concept must be designed as a generalization of the promotion-boundary classification, and its analysis/extraction turns must ride the permission-contract + transaction model that `plan.md` is establishing, not a parallel mechanism.

**Recommended architectural direction.** Explicitly reconcile with these four plans during roadmapping: extend `ArtifactPromotionService`'s classification family rather than sibling it; run preservation agent turns through the operation-scoped permission/transaction pattern; model distillation outputs as projections.

---

## 2. Current Architectural Fit (Required Analysis 1)

### F-2.1 Three working precedents already implement the platform's core mechanics in miniature

**Observation.**
- **Classification + evidence preservation:** `ArtifactPromotionService.PromoteAsync` (`src/LoopRelay.Roadmap.Cli/ArtifactPromotion.cs:74-101`) classifies agent output (Promotable/Blocked/Malformed/Ambiguous), validates, and either promotes or preserves the rejected output as numbered evidence with a lifecycle-state record — never silently discarding.
- **Ledger:** `DecisionLedger`/`DecisionLedgerStore` (`.agents/decision-ledger.json`, schema `decision-ledger.v1`, ID format + duplicate validation) is a schema-versioned, validated, repository-owned ledger.
- **Insight extraction:** `SynthesizeCompletedEpic.prompt` produces a structured free-form synthesis of an entire archived epic — including a **Source Disposition** section marking each source as preserved/partial/superseded/ignored — written by `CompletedEpicArchiveService.ArchiveAndSynthesizeAsync` (`CompletedEpicArchiveService.cs:66`).

**Why it matters.** The audit's primary directive is to evolve existing architecture rather than introduce parallel systems. All three pillars of the platform (classify, ledger, extract) have live, tested, doctrinally-approved implementations.

**Architectural impact.** The platform is an act of *generalization*: promotion-boundary classification generalized from "epic file" to "any repository file"; the decision-ledger shape generalized to preservation records; completed-epic synthesis generalized to arbitrary pruning events.

**Recommended architectural direction.** Anchor the platform's design vocabulary and mechanics on these three precedents. Any design review should ask "why is this not the promotion/ledger/synthesis pattern?" before accepting a new mechanism.

### F-2.2 No file-classification, settings, or knowledge types exist anywhere — the gaps are real but narrow

**Observation.** Grep-verified: no type named `Reasoning*`, `Insight*`, `Knowledge*`, or `Preservation*` exists in `src/`; `LoopRelay.Core` contains only four hand-written classes (artifact store, path safety, `Repository`) plus 41 prompt templates; no implementation-vs-non-implementation concept exists; no settings/options types exist in Core; no interactive HITL input path exists in any CLI (`ILoopConsole` is write-only; `OperationPermissionHandler` hard-denies `UserInput`/`McpElicitation`).

**Why it matters.** Distinguishes "extend existing abstraction" work from genuine greenfield: classification *policy*, the Implementation-First *setting and its propagation channel*, and *interactive HITL* are greenfield; everything else has a home.

**Architectural impact.** Greenfield pieces must be placed respecting the layering constraints: Core is the dependency-free base layer (it may host a pure classification-policy vocabulary and prompts); enforcement and orchestration belong in the CLI/library layers.

**Recommended architectural direction.** Keep the greenfield surface minimal: one classification-policy concept, one settings document + propagation channel, one HITL gate pattern (reusing the roadmap paused/unblock shape, F-4.4). Resist inventing an event bus, a knowledge graph, or a rules engine — the doctrine (reasoning-materialization-policy's "Derived If Reconstructable") explicitly gates such materializations.

### F-2.3 Responsibility redistribution: pruning authority is currently implicit and scattered

**Observation.** Three mechanisms already delete or consolidate repository content with no classification, no preservation analysis, and no record of what was lost: live-artifact rotation and retirement (`LoopArtifacts.RetireLiveDecisionsAsync`, rotation at `LoopArtifacts.cs:125-144`), transfer-time document optimization (`DecisionSession.OptimizeOperationalDocumentsAsync`, `DecisionSession.cs:323-356`, which "prunes/consolidates" plan/details/operational context via an agent turn), and epic archival (moves at `CompletedEpicArchiveService.cs:56-62`).

**Why it matters.** "Semantic preservation analysis before pruning" is not a new stage in a vacuum — the system already prunes in three places, each with its own implicit policy.

**Architectural impact.** The platform should become the *single owner of pruning policy* — the answer to "may this content be removed, and what must be preserved first" — consumed by all three existing pruning sites, rather than a fourth independent pruner.

**Recommended architectural direction.** Redistribute: pruning *mechanics* stay where they are (rotation, optimization, archival); pruning *policy and preservation obligations* consolidate under the platform. This is the cohesion-preserving cut line.

---

## 3. Epic Execution Lifecycle Integration (Required Analysis 2)

### F-3.1 Stage-by-stage placement

**Observation.** The epic lifecycle across the three CLIs has these concrete stages and owners:

| Lifecycle stage | Owner | Integration point |
|---|---|---|
| Roadmap/epic generation | Roadmap CLI (`RoadmapStateMachine`) | Prompt-context injection (F-6.2); `PromptContractRegistry` output contracts (F-5.3) |
| Plan authoring + decomposition | Plan CLI (`PlanPipeline.RunAsync`, `PlanPipeline.cs:26-142`) | Prompt guidance; plan-content verification gate; write allowlists (F-5.2) |
| Execution turn (repo mutation) | Main CLI (`ExecutionStep.RunAsync`) | Post-turn classification at the change-detection boundary (F-4.1) |
| Milestone completion | Main CLI (`MilestoneGate` checkbox counting) | No distinct hook exists; per-iteration classification subsumes it |
| Context refresh / transfer | Main CLI (`DecisionSession.TransferAsync` → `OptimizeOperationalDocumentsAsync`) | Preservation analysis before optimization prune (F-4.2) |
| Epic completion + archival | `LoopRelay.Completion` (invoked from `LoopRunner.cs:36` and `RoadmapStateMachine.cs:1022`) | Preservation analysis + insight extraction + ledger update at the archive boundary (F-4.3) |
| Session continuity | `.LoopRelay/` resume store; ledger provides cross-run memory | Ledger lookup before re-analysis (F-9.2) |

**Why it matters.** The capability's stages map 1:1 onto existing lifecycle moments; none require inventing a new lifecycle.

**Architectural impact.** Each stage has a single existing owner; the platform adds behavior *at* these points without changing who owns the stage.

**Recommended architectural direction.** Bind each platform capability to exactly one lifecycle moment as above. Avoid a "preservation sweep" that runs orthogonally to the lifecycle — that would be the isolated-subsystem anti-pattern the objective forbids.

### F-3.2 The loop has no step abstraction — insertion means editing fixed sequences

**Observation.** `LoopRunner.RunAsync` is a fixed `while(true)` sequence with nine hand-wired collaborators (`LoopRunner.cs:11-20`, composition hand-`new`ed in `LoopCliComposition.Create`, `LoopCliComposition.cs:38-129`); `PlanPipeline.RunAsync` is a single linear method; `RoadmapStateMachine` is a 1,984-line coordinator with 27 constructor collaborators. `LoopRunner`, `ExecutionStep`, `DecisionSession` are `internal sealed` concretes with no interfaces. Sequencing invariants are encoded as long prose XML-doc essays (e.g. `LoopRunner.cs:64-135`).

**Why it matters.** Every insertion point identified in this audit requires editing a fixed sequence and its composition root simultaneously; the prose-documented rotation/stall/gate invariants create high regression risk.

**Architectural impact.** This is the single largest structural friction for the integration — not any missing capability, but the absence of seams to add stages safely.

**Recommended architectural direction.** Do not introduce a generic pipeline framework for its own sake. But where a platform stage is inserted (post-execution classification in `LoopRunner`, pre-archive analysis in the completion flow), take the opportunity to extract that segment behind an interface with characterization tests — consistent with the state-machine audit's existing call to "separate prompt execution lifecycle from artifact promotion lifecycle." Strengthen the seam you touch; leave the rest.

---

## 4. Repository Mutation Lifecycle (Required Analysis 3)

### F-4.1 Classification belongs at the change-detection boundary; owner: Main CLI loop

**Observation.** The only place the system observes *what the agent changed* is `WorkingTreeChangeDetector.GetRealChangedPathsAsync` (`WorkingTreeChangeDetector.cs:23-36`) — `git status --porcelain`, paths only, no diff content, no add/modify/delete status, `.agents/` filtered out. It has exactly two consumers: `ExecutionStep.cs:83` (handoff-prompt choice) and `CommitGate.cs:58` (commit-vs-stall). Post-execution verification is existence-only (handoff file present).

**Why it matters.** Classification is a function of changed paths (and, for semantic analysis, content) — this boundary is where the input already flows. It sits *before* the commit gate (`CommitGate.CommitPushAndEvaluateAsync`, `CommitGate.cs:53-93`), i.e. before mutations become durable in git history.

**Architectural impact.** Classification of newly created/modified files is a per-iteration Main CLI concern, ordered: execution turn → change detection → **classification** → (remediation/ledger/flag) → commit gate. The detector's paths-only output is sufficient for policy classification (path/extension/location rules); semantic analysis reads content on demand through the artifact store.

**Recommended architectural direction.** Anchor post-mutation classification on the change-detector output in the `LoopRunner` segment between `execution.RunAsync()` (`LoopRunner.cs:114`) and the commit gate (`:137`). Enrich the detector's output (change kind per path) rather than shelling out to git a second time. Classification results feed three consumers: the ledger, the commit gate (an Implementation-First violation can be surfaced or block staging), and telemetry.

### F-4.2 Semantic preservation analysis attaches to the two existing pruning moments

**Observation.** Content is destroyed or compressed at exactly two moments: transfer-time `OptimizeOperationalDocumentsAsync` (agent-driven pruning of plan/details/operational context, run as a scoped, transaction-wrapped artifact operation via `RunArtifactOperationAsync`, `DecisionSession.cs:358-449`) and epic archival (F-4.3). Both already run agent turns under `OperationPermissionProfile` scoping with `ArtifactMutationTransaction` snapshot/rollback.

**Why it matters.** "Preservation analysis before pruning" needs a *trigger*, and these are the only pruning triggers in the system. Both already have the exact execution shape a preservation-analysis turn needs (scoped reads/writes, rollback on failure, isolated one-shots via `TempSandboxWorkspaceFactory` where needed).

**Architectural impact.** Preservation analysis is not a new pipeline — it is a pre-stage of each pruning operation, reusing the scoped-operation pattern verbatim.

**Recommended architectural direction.** Define the platform's preservation-analysis obligation as a contract that pruning sites invoke before destructive action, with results recorded in the ledger. The operational-context schema's Preserve/Summarize/Retire triad and its deterministic-diff, warnings-not-mutation posture (`docs/operational-context-schema.md`) is the governing doctrine for what analysis may conclude.

### F-4.3 The archive boundary is the primary preservation moment — and it is currently unsafe

**Observation.** `CompletedEpicArchiveService.ArchiveAndSynthesizeAsync` (`CompletedEpicArchiveService.cs:32-80`) performs destructive moves (`:56-62`: decisions, deltas, handoffs, milestones, details.md, operational_context.md, plan.md) *before* synthesis (`:66`). Issues 004 (non-transactional), 005 (rerun breaks idempotency — archive removes milestones, so `MilestoneGate.IsEpicCompleteAsync` returns false and the loop re-executes against a moved plan), and 006 (count-based index collides after gaps: `ComputeArchiveIndexAsync` returns `directories.Count + 1`, `:82-86`) are all confirmed present in current code. There is no durable epic-completed marker. The close/archive/update sequence is duplicated between `CompletionCertificationService` (Main CLI) and `RoadmapStateMachine.RunCompletionCertificationAsync` (`RoadmapStateMachine.cs:1022`).

**Why it matters.** This is where insight extraction, ledger finalization, and HITL preservation review naturally attach — inserting expensive agent stages *after* irreversible moves widens an already-open data-loss window.

**Architectural impact.** Preservation stages at this boundary are architecturally blocked until the flow is copy-first/staged, idempotent (durable completion marker), and single-sourced (one orchestration shared by both CLIs).

**Recommended architectural direction.** Treat issues 004/005/006 plus the dual-orchestration duplication as integration prerequisites (F-12.1). Then attach preservation analysis + insight extraction *between certification and archival moves*, so analysis always runs against live, intact inputs and a failed analysis leaves the repository untouched.

### F-4.4 HITL review: the only viable pattern is paused-state + resume, not interactive prompts

**Observation.** No interactive human input exists anywhere: the Main CLI decision session **auto-submits the agent's proposal verbatim** (`DecisionSession.cs:102-103`; the legacy human Submit gate is retired), `ILoopConsole` has no read methods, permission approvals are auto-answered accept/decline by `CodexPermissionAdapter` with `UserInput` explicitly denied, and TD-4 records that one-shots emit `approval_policy="never"` unconditionally. The only working HITL shape in the system is the Roadmap CLI's terminal-paused outcomes plus the operator-driven `unblock` command (`RoadmapStateMachine.UnblockAsync`, `:68-97`), and the human-authored `.agents/decisions/decisions.md` channel.

**Why it matters.** "HITL Preservation Review" cannot be an interactive console dialog without building an input capability that contradicts the system's deliberate fully-autonomous-loop design.

**Architectural impact.** HITL review must be modeled as *state*, not *conversation*: uncertain preservation cases produce a review-pending record (evidence + proposed disposition), the affected destructive action is deferred or blocked, and a human resolves via an explicit command or by editing the review artifact — mirroring the unblock pattern and the decisions.md authority channel.

**Recommended architectural direction.** Adopt the paused/evidence/unblock shape for preservation review. This also cleanly encodes the Implementation-First exception ("non-implementation artifacts may be created when explicitly requested by HITL"): the request is a durable, human-authored artifact (a decisions.md entry or review resolution), giving the classifier a provenance input rather than a runtime conversation.

---

## 5. Planning Integration (Required Analysis 4)

### F-5.1 Planning prompts already carry prohibition language — extend the precedent

**Observation.** Every roadmap planning prompt already contains explicit scope prohibitions ("You are NOT generating code / creating execution prompts / creating implementation tasks" — e.g. `CreateNewEpic.prompt:19-27`, `GenerateMilestoneDeepDivesForEpic.prompt:17-26`). The Plan CLI's three extraction one-shots (`CollectDetails`, `ExtractMilestones`, `ExtractDetails`) are themselves the system's own generators of non-implementation artifacts, governed by per-operation write allowlists.

**Why it matters.** Implementation-First planning guidance has an established prompt idiom and an established deterministic enforcement mechanism; neither needs invention.

**Architectural impact.** Planning integration is two-layered: *advisory* (prompt guidance discouraging planned non-implementation artifacts) and *deterministic* (output contracts and write allowlists that reject them).

**Recommended architectural direction.** Express Implementation-First in both layers. Advisory guidance flows through the injection seams in §6; deterministic enforcement extends `PromptContractRegistry` declared outputs (Roadmap) and `ArtifactOperationPlan.AllowedWrites`/`AllowedWriteGlobs` (Plan CLI scoped operations).

### F-5.2 The Plan CLI's post-turn verification gate is the natural plan-content lint point

**Observation.** `PermissionedArtifactOperationStep.VerifyOutputsAsync` (`PermissionedArtifactOperationStep.cs:122-175`) already inspects produced files after each scoped turn, with `ArtifactMutationTransaction` rollback on failure; `PlanSession.VerifyPlanGateAsync` gates plan content existence. Meanwhile the adversarial review verdict is advisory only — a FAIL does not block (`ReviewStep.TryExtractVerdict`, `ReviewStep.cs:77-103`).

**Why it matters.** A plan that *schedules* non-implementation artifact creation should be caught at planning time, not discovered post-execution. The verification-gate seam already has the right transactional semantics; the advisory-verdict precedent is a warning about wiring review outcomes without teeth.

**Architectural impact.** Plan-content linting (detecting planned non-implementation artifacts in plan/milestone prose) fits the existing deterministic-gate architecture; whether it blocks or warns must be an explicit, mode-dependent decision — not accidentally advisory.

**Recommended architectural direction.** Place Implementation-First plan linting in the existing post-turn verification gates with rollback semantics, and make its blocking behavior an explicit function of the mode setting.

### F-5.3 Roadmap generation: inject via context builder and output contracts, not per-state logic

**Observation.** Roadmap prompt inputs are assembled by `RoadmapPromptContextBuilder.Build*` (`RoadmapPromptContextBuilder.cs:11-90`) — titled, composed sections passed as the single `{projectContext}` placeholder — and every prompt's allowed outputs are declared in `PromptContractRegistry` (`PromptContractRegistry.cs:13-22`). The `.agents/ctx/01..08-*.md` project-context contract (including `05-authority-model.md` and `03-invariants.md`) feeds all projections.

**Why it matters.** These are single choke points reaching all planning prompts without touching 20K lines of templates or the 33-state machine.

**Architectural impact.** Implementation-First reaches roadmap and milestone generation through three compounding channels: a durable policy statement in the project-context files (reaches every projection), a mode-conditional section in the context builder (reaches every runtime prompt), and tightened output contracts (deterministic).

**Recommended architectural direction.** Use all three; add zero per-state logic to `RoadmapStateMachine`.

---

## 6. Prompt Architecture (Required Analysis 5)

### F-6.1 The prompt engine has no conditional composition — and the codebase already chose its idiom

**Observation.** The source generator (`PromptSourceGenerator.cs`) supports only positional `string?` placeholders; null renders as empty string; no sections, fragments, or conditionals. The established optional-content idiom is the empty-string placeholder (`{details}` in `StartExecution`/`ContinueExecution`, documented at `OrchestrationArtifactPaths.cs:19-25`). The repo previously had prompt *variants* (`WritePlanForNewCodebase` vs `WritePlanAgainstCodebase`) and deliberately consolidated them into one template (technical-debt.md, 2026-07-02).

**Why it matters.** The cleanest injection architecture question is already answered by precedent: one template + optional placeholder + call-site composition, never per-mode template variants.

**Architectural impact.** Implementation-First guidance is a single shared guidance text (one constant, one authority) passed into an optional placeholder on the affected prompts, rendered as empty when the mode is off. Prompt edits are automatically versioned — every template carries a build-time `SourceHash` that flows into projection provenance, so a guidance change is a detectable, provenance-visible policy change.

**Recommended architectural direction.** One optional guidance placeholder per affected prompt family; guidance text defined once (Core is the natural dependency-free home, consistent with its prompt-authority role); injection performed at the existing catalog choke points (`RoadmapPromptCatalog.RenderRuntime`, `ProjectionPromptCatalog.RenderProjection`, `CompletionRuntimePrompts`, and the Main/Plan CLI call sites). No prompt variants; no duplicated guidance strings.

### F-6.2 Prompt duplication is the injection hazard

**Observation.** The catalog is 41 templates, ~20,133 lines; the 12 `ProjectionFor*` templates are near 1:1 mirrors of their Planning counterparts (549–1,133 lines each); the two catalog switch statements (`RoadmapPromptCatalog`, `ProjectionPromptCatalog`) are near-identical registries; prompt-name strings are architectural keys across catalogs, contracts, input resolvers, and projection registries (state-machine audit finding). Four prompts are dead (TD-6 `GetNextDecisions`, TD-9 `GenerateSystemPromptForFirstExecutionAgent`, plus both legacy `StartDecisionSession*`).

**Why it matters.** Naively adding Implementation-First guidance "to all planning prompts" means ~23 large template edits duplicated across mirrored files — the exact proliferation failure mode the platform exists to prevent.

**Architectural impact.** The injection design must minimize per-template surface: placeholder additions only where a prompt actually drives artifact creation, guidance text single-sourced, dead prompts excluded.

**Recommended architectural direction.** Scope placeholder additions to the artifact-creating prompt families (plan authoring, extraction one-shots, epic creation/realignment, milestone deep-dives, execution start/continue). Treat the Planning/Projection mirror duplication as a known debt to avoid worsening — not to fix in this effort (it does not block integration).

---

## 7. Settings Architecture (Required Analysis 6)

### F-7.1 No settings channel exists from configuration to behavior — this is the platform's largest greenfield piece

**Observation.** Configuration today is: (a) `settings.json` resolved relative to the *binary's* directory, schema limited to permissions (`CliSettingsLoader.cs:30-57`, `SettingsDocument` at `:94-97`); (b) environment-variable toggles (`LoopRelay_SESSION_LOG`, `LoopRelay_DECISION_RESUME`); (c) options records with inline defaults, not bound from any config (`DecisionSessionRouterOptions`). There is no per-repository settings store, no general options flow into any pipeline stage, and the one CLI flag that exists beyond the repo path (`--elevated`) is parsed but never wired (`RoadmapExecutionOptions` unreached from `RoadmapCliComposition.Create`). Roadmap CLI loads no settings at all (issue 003).

**Why it matters.** Implementation-First Mode must be visible to planning (Roadmap, Plan CLIs), execution (Main CLI), and completion — across machines, since the loop's durable state travels via the `.agents/` submodule. No existing mechanism provides this.

**Architectural impact.** A settings decision has three architectural dimensions: **location** (per-repository and committed → `.agents/`, following the schema-versioned JSON store doctrine; machine-local `.LoopRelay/` or env vars are wrong for a policy that must bind all three CLIs consistently), **propagation** (a loaded settings document threaded through each CLI's composition root into the seams identified in §5/§6 — building the channel that `RoadmapExecutionOptions` shows is missing), and **visibility** (the mode must reach prompt rendering, output contracts, classification behavior, and telemetry so every subsystem agrees on the active policy).

**Recommended architectural direction.** Introduce a repository-owned, schema-versioned settings artifact under `.agents/` (via the consolidated structured-store helper, F-12.2) as the home for Implementation-First Mode and future preservation policy configuration; read it at composition time in all three CLIs; surface the active mode in prompt context and in every ledger/telemetry record so behavior is auditable. Default the mode to **disabled** initially: enabling it changes agent behavior contractually, and the governance process (F-11.2) requires evidence before a baseline behavior change. Env-var override may exist as a machine-local escape hatch, consistent with existing toggle precedent, but the committed artifact is authoritative.

---

## 8. Repository Preservation Platform as a Subsystem (Required Analysis 7)

### F-8.1 Yes — a first-class shared library, shaped like Completion/Permissions, with narrow authority

**Observation.** The codebase's successful subsystem pattern is a shared library with: owned domain models and stores, prompts registered in the Core catalog, a runtime prompt-runner over the shared agent runtime, deterministic policy/router components, and invocation from CLI composition roots (`LoopRelay.Completion` and `LoopRelay.Permissions` both follow this).

**Why it matters.** The capability has enough cohesive, cross-CLI responsibility (classification policy, preservation analysis orchestration, insight synthesis, ledger ownership, review routing) to warrant a boundary — but its lifecycle is entirely parasitic on the three CLIs' lifecycles.

**Architectural impact.** Responsibilities: own the classification policy and its evaluation; own the preservation ledger (document, store, invalidation); orchestrate preservation-analysis and insight-extraction agent turns (via the existing runtime, scoped-operation, and sandbox-workspace patterns); produce preservation review records and route them to the paused/unblock HITL shape; expose deterministic results to the loop (commit gate), planning gates, and completion flow. Boundaries: it evaluates and proposes; it never commits/pushes, never rewrites operational context (that authority is owned, F-11.1), never deletes without a pruning site invoking it, and never becomes a second source of truth (repository files remain authoritative). Internal components mirror the Permissions engine shape — classification, policy matching with a non-removable floor, decision, and a post-evaluation invariant guard — applied to files instead of commands.

**Recommended architectural direction.** Create it as a sibling shared library, consumed by all three CLIs at the lifecycle points in §3–§5. Its public surface should be small and deterministic-first: classification and ledger operations are pure/deterministic; agent-driven analysis is behind the same runner abstraction the Completion subsystem uses.

---

## 9. Preservation Ledger (Required Analysis 8)

### F-9.1 Persistence model: repository-owned schema-versioned JSON, following the established doctrine

**Observation.** Persistence reality: 100% file-based (the SQLite refactor doc is aspirational — no SQLite/Dapper exists in the tree); doctrine per `docs/roadmap-structured-persistence.md` is versioned JSON with explicit DTOs, schema-version validation, deterministic serialization, optional Markdown *projection* (never a rewritten shadow authority); atomic writes are free via `FileSystemArtifactStore.WriteAsync` (temp + `File.Replace`, `FileSystemArtifactStore.cs:188,248`); the single-writer assumption holds (one CLI process per repo). Precedents: `decision-ledger.json`, `state.json`, `projection manifest.json`, `lifecycle.json` — all `StructuredDocumentStore<T>`-shaped under `.agents/`.

**Why it matters.** The ledger records repository knowledge (what was classified, analyzed, preserved, and why) — knowledge that must survive machine changes and be reviewable by humans; `.LoopRelay/` (machine-local, gitignored) would silently discard it.

**Architectural impact.** The ledger is a repository-owned document under `.agents/`, committed via the existing submodule publish path; a Markdown projection makes it human-reviewable per the JSON-source + MD-projection contract in the reasoning-repository-contracts doctrine.

**Recommended architectural direction.** Schema-versioned JSON ledger under `.agents/` through the consolidated structured-store helper. If entry volume grows append-heavy, follow the telemetry sink's true-append JSONL pattern rather than the transition journal's whole-file-rewrite pattern (a known smell).

### F-9.2 Invalidation, versioning, and duplicate-work avoidance: the projection provenance system is the ready-made mechanism

**Observation.** `ProjectionProvenance` + `ProjectionCausalInput(Kind, Identity, Version)` where Version is a content SHA-256, evaluated by `ProjectionFreshnessEvaluator` into typed stale reasons (`ProjectionProvenance.cs:8-13`, `:71`, `:181`); the same pattern exists as `DerivedArtifactProvenance` + `TransitionInputSnapshot` (per-artifact SHA-256 embedded in every journal record) in the Roadmap CLI. Prompt templates carry build-time `SourceHash`.

**Why it matters.** "Avoid duplicate work across future executions" and "invalidation / policy evolution" are exactly causal-input staleness: a ledger entry is valid while (file content hash, classification-policy version, analysis-prompt SourceHash) are unchanged.

**Architectural impact.** Ledger entries carry causal inputs: the file's content hash, the policy version, and the SourceHash of any prompt that produced an analysis/synthesis. Lookup-before-analysis skips unchanged files; a policy or prompt change automatically invalidates affected entries with a typed reason — giving policy evolution a deterministic, auditable mechanism rather than a manual migration.

**Recommended architectural direction.** Reuse the provenance/freshness pattern verbatim. Do not design bespoke invalidation.

---

## 10. Insight Extraction (Required Analysis 9)

### F-10.1 MVP: generalize the completed-epic synthesis; constrain it with the operational-context doctrine

**Observation.** `SynthesizeCompletedEpic` already performs free-form, concise, implementation-focused synthesis with explicit authority-resolution rules and per-source disposition; it runs as a workspace-write agent turn at the archive boundary. The `TempSandboxWorkspaceFactory` pattern provides isolated, seeded workspaces so a synthesis turn sees only the files under analysis. The operational-context schema defines what distilled knowledge may and may not contain (no raw history, transcripts, status tracking) and mandates preservation-over-silent-discard.

**Why it matters.** The MVP requirement (free-form, no schema, concise, implementation-focused) is already met by an existing prompt family; the risk is not building it but *un*constraining it into a shadow knowledge base — which the reasoning authority-boundary doctrine explicitly forbids.

**Architectural impact.** Insight outputs are *derived evidence artifacts*: stored under the archive/evidence tree, carrying provenance (causal-input hashes), never authoritative, never a replacement for operational context or decisions.

**Recommended architectural direction.** Model MVP insight extraction as a sibling of `SynthesizeCompletedEpic` — same runner pattern, read-only or sandbox-seeded sessions, output as a provenance-carrying evidence artifact recorded in the ledger.

### F-10.2 Future-proofing without pre-building: envelope now, structure later, gated promotion

**Observation.** The reasoning-materialization-policy defines the exact gate for the future evolution: derived-if-reconstructable; outcomes Remain Derived / Add Derived Cache / Add Read Model / Promote To First-Class / Reject; promotion is a separate governed slice. The structured-persistence doctrine already pairs free-form Markdown bodies with schema-versioned JSON envelopes.

**Why it matters.** The stated future (structured knowledge, semantic dedup, knowledge promotion) tempts premature schema design; the doctrine says free-form content is fine as long as its *records* are versioned and provenance-carrying.

**Architectural impact.** MVP syntheses should be free-form bodies wrapped in the same schema-versioned, provenance-carrying record shape as everything else. Future structured extraction then supersedes bodies without migrating authority or identity; semantic dedup layers on the ledger's keyed, hashed entries; knowledge promotion runs through the materialization gate.

**Recommended architectural direction.** Version the envelope, not the insight. Explicitly record in the platform's governance artifact that structured knowledge is a *promotion* requiring the materialization-gate review — preventing MVP drift into an ungoverned knowledge store.

---

## 11. Authority Model (Required Analysis 10)

### F-11.1 A preservation authority is warranted — as a proposing authority, subordinate to HITL and repository authority

**Observation.** The consolidated authority model (from reasoning-authority-boundary, reasoning-ownership-boundaries, agents-artifact-ownership, and code): repository files are authoritative; HITL owns `decisions.md` and all recovery gates; planning owns roadmap/plan/milestone artifacts within declared contracts; execution owns provider sessions, handoffs, commit/push; operational-context authority alone rewrites `operational_context.md`; the Decision session holds zero operational authority (read-only posture, structural). These boundaries are mostly *structural or doc-only* — the only code-level authority vocabulary is `TrustPolicy` (descriptive) and the permission engine's invariant floor.

**Why it matters.** The platform introduces genuinely new questions no existing authority answers: "is this file implementation?", "may this content be pruned?", "what must be preserved first?". Assigning these to an existing authority would either give execution self-judging power (conflict of interest) or overload operational-context authority (which owns understanding, not file lifecycle).

**Architectural impact.** A preservation authority owns classification verdicts, preservation obligations, and the ledger — and is deliberately *not* given: deletion authority (pruning sites act, preservation gates), repository mutation authority beyond its own artifacts, or override of HITL. The Implementation-First exception makes HITL request-provenance an explicit authority input: autonomous creation of non-implementation artifacts is prohibited *unless* a durable human-authored request exists.

**Recommended architectural direction.** Define preservation authority in the ownership-matrix style of reasoning-ownership-boundaries, and back its critical boundary (non-authoritative, may not mutate outside its own artifacts) with an executable guard, per the governance requirement below — not prose alone.

### F-11.2 Governance owes: duplicate-authority scan, executable guards, evidence, rollback

**Observation.** `architecture-decision-governance.md` requires new-authority and new-projection changes to provide: a duplicate-authority scan, named owners, an evidence package per change, a *named executable guard* per boundary ("documentation alone does not certify a boundary"), baseline updates, and a rollback path. The live enforcement precedents are the layering tests, the prompt-authority scan, and the permission invariant guard.

**Why it matters.** The platform overlaps two existing authorities — operational-context compression (both distill knowledge) and the promotion boundary (both classify agent output) — precisely the duplicate-authority risk the governance process exists to catch.

**Architectural impact.** The duplicate-authority scan must produce explicit verdicts: insight extraction is *not* operational-context evolution (different inputs, outputs, and authority); file classification *is* the promotion-boundary classification generalized (shared lineage, single policy authority).

**Recommended architectural direction.** Budget the governance artifacts (evidence package, guards, capability/mechanism baseline entries, rollback path) as part of the platform's definition of done, and reuse the existing guard idioms (source-scanning tests, invariant floors) rather than inventing new certification machinery.

---

## 12. Pre-Integration Technical Debt (Required Analysis 11)

Only debt that materially improves this integration; unrelated cleanup deliberately ignored.

### F-12.1 Completion archive safety (issues 004, 005, 006 + dual orchestration) — hard prerequisite

Destructive moves before synthesis, no idempotency marker, count-based index collisions, and the close/archive sequence duplicated across Main CLI and `RoadmapStateMachine`. Every preservation stage added at this boundary multiplies the stranded-archive failure surface, and dual orchestration means every stage lands twice. **Direction:** copy-first/staged archive, durable completion marker, max+1 indexing, single shared orchestration — before preservation stages attach here.

### F-12.2 Structured-store and path-catalog consolidation — strong prerequisite

`StructuredDocumentStore<T>` (Roadmap) and `StructuredJsonDocumentStore<T>` (Projections) are near-identical; JSON options, Markdown-table parsing, and the `.agents/ctx` path constants are copy-pasted per assembly (maturity-audit REF-4: "artifact access and path catalogs are fragmented"). The ledger and settings document would otherwise become the third and fourth copies. **Direction:** consolidate one structured-store helper and one path authority in a shared layer before adding new consumers.

### F-12.3 Settings/options channel — prerequisite by definition

No mechanism carries configuration into planning or execution behavior (F-7.1); `RoadmapExecutionOptions` demonstrates the missing wiring. Implementation-First Mode cannot exist without building this channel; build it as the settings architecture in §7, not as another env var.

### F-12.4 Permission-layer trust gaps (issues 007, 008; ungated one-shots; issue 003) — constraint, not blocker

Redirection and wrapper bypasses plus the ungated one-shot path mean a command-layer file-creation prohibition is untrustworthy. This does not block the platform — it *dictates* the post-mutation enforcement posture (F-4.1) — but fixing 007/008 remains worthwhile defense-in-depth, and issue 003 (Roadmap CLI loads no permission policy) should be fixed when the settings channel is built since the same loader wiring is touched.

### F-12.5 Dead prompts and stale docs — hygiene that prevents mis-integration

TD-6/TD-9 dead prompts must be excluded from guidance injection; stale-era docs (F-1.2) must be quarantined so the platform's governance artifacts cite live architecture. Low cost, high confusion-avoidance.

Explicitly *not* prerequisites: the Planning/Projection prompt mirror duplication (avoid worsening, don't fix), the `RoadmapStateMachine` mega-coordinator decomposition (only the completion-certification segment matters here), the Plan CLI's per-run `Guid` repository identity (the ledger is repo-relative and needs no cross-repo key), and the backend-era TD items (moot).

---

## 13. Cross-Cutting Concerns (Required Analysis 12)

- **Events/telemetry:** No event bus exists; observability is parallel append logs. Preservation actions (classifications, analyses, prunes, reviews) should emit to a JSONL stream following `RotatingJsonlTelemetrySink` (true append, rotation, fail-open) — giving future preservation metrics a source without new infrastructure.
- **State transitions:** The Roadmap CLI's persisted-state + resume-planner + journal pattern is the model for any platform state (review-pending); every transition should journal with input snapshots, as roadmap transitions already do.
- **Persistence:** One doctrine — schema-versioned JSON, atomic writes, repository-owned under `.agents/` vs machine-local under `.LoopRelay/`; the platform adds documents, never a new persistence technology.
- **Caching/invalidation:** Content-hash causal inputs everywhere (F-9.2); the `FileSystemArtifactStore` signature-keyed caches make repeated ledger/policy reads cheap.
- **Prompt construction:** All new prompts enter the Core catalog with SourceHash provenance and register in the (unfortunately duplicated) catalog switches; guidance text single-sourced (F-6.1).
- **Policy evaluation:** The Permissions engine shape — classification, closed-world policy, non-removable invariant floor, post-evaluation guard — is the house pattern for the file-classification policy; mirror the shape as a sibling, do not extend the command-oriented substrate (parser/canonicalizer/fingerprint do not fit files).
- **Configuration:** The new settings channel (F-7.1) becomes the single configuration story for mode flags across all three CLIs; design it as shared infrastructure, not a Main-CLI special.
- **Concurrency:** Single-writer-per-repo holds; ledger and settings writes go through the atomic artifact store; no cross-process locking needed now, but whole-file-rewrite stores must not be used for append-heavy streams.
- **Extensibility:** The systemic weakness is absent step/stage seams (F-3.2); each platform insertion should leave behind an interface + characterization tests at the point it touches.
- **Diagnostics:** Classification verdicts and preservation decisions must be explainable — carry the matched rule/policy version in every record, as permission denials carry their rule provenance.

---

## 14. Future Evolution (Required Analysis 13)

The MVP architecture above was checked against each stated future direction; none is blocked, and most have designated landing zones:

- **Structured syntheses:** envelope-versioned free-form records (F-10.2) upgrade in place; promotion is a materialization-gate event, not a migration.
- **Semantic repository knowledge / dedup:** layers on ledger keys + content hashes; embedding or entailment machinery would be a new *derived cache* under the materialization policy — the deterministic-diff boundary in the operational-context schema (no NL entailment, no confidence scoring) is the line MVP must not cross prematurely.
- **Preservation metrics:** a projection over the ledger + preservation telemetry stream; no new collection needed.
- **Policy evolution:** policy version as a causal input (F-9.2) makes policy changes auditable and re-analysis automatic; the settings artifact carries policy configuration forward.
- **Repository health / documentation-debt analysis:** projections over the ledger (counts, staleness, non-implementation density) — the Projections subsystem is the designated home.
- **Semantic garbage collection:** classification + ledger + preservation analysis are precisely its substrate; GC becomes a governed retire path mirroring Preserve/Summarize/Retire, with HITL review as the safety gate.
- **Repository certification:** extends the completion-certification pattern (evaluate → policy → route) with preservation evidence as an input — the Completion subsystem's policy/router shape generalizes.

The one evolution risk worth recording: if insight artifacts accumulate without the materialization gate being enforced, the platform becomes the "private knowledge database" the authority doctrine forbids — and, ironically, a generator of the non-implementation proliferation it polices. The executable guard on preservation authority (F-11.1) is the structural defense.

---

## 15. Consolidated Integration-Point Map

| Capability | Lifecycle moment | Existing seam | Owner |
|---|---|---|---|
| File classification (policy) | Post-execution, pre-commit | `WorkingTreeChangeDetector` output in `LoopRunner` between execution and `CommitGate` | Main CLI + platform library |
| File classification (planning-side) | Artifact promotion; scoped one-shot outputs | `ArtifactPromotionService` classification; `VerifyOutputsAsync` gates; `ArtifactOperationPlan` allowlists | Roadmap/Plan CLIs |
| Implementation-First guidance | Prompt rendering | Optional placeholder + shared constant at catalog choke points; `RoadmapPromptContextBuilder` section; `.agents/ctx` policy statement | Core (text) + catalogs (injection) |
| Implementation-First enforcement | Post-mutation detection; plan lint; output contracts | F-4.1 boundary; F-5.2 gates; `PromptContractRegistry` | Platform library |
| Semantic preservation analysis | Before any prune | Pre-stage of `OptimizeOperationalDocumentsAsync` and (post-fix) the archive flow, via scoped-operation + sandbox-workspace patterns | Platform library, invoked by pruning sites |
| Insight extraction (MVP) | Epic archival; prune events | Sibling of `SynthesizeCompletedEpic` in the completion prompt-runner pattern | Completion + platform library |
| Preservation ledger | Classification + preservation events | Schema-versioned JSON under `.agents/` via consolidated structured store; provenance/freshness invalidation | Platform library |
| HITL preservation review | Uncertain classifications; prune approvals | Paused-state + evidence + `unblock`-style resolution; `decisions.md` as request provenance | Platform library + each CLI's outcome handling |
| Implementation-First Mode setting | Composition time, all CLIs | New repository-owned settings artifact under `.agents/`; threaded through composition roots (the channel to build) | Shared infrastructure |
| Knowledge distillation (long-term) | Completion + transfer | Existing `SynthesizeCompletedEpic` / operational-context evolution, governed by Preserve/Summarize/Retire doctrine | Existing owners, policy from platform |
