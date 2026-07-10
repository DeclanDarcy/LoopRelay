# Canonical Workflow Runtime Discovery Audit

Audit target: the workflow runtime architecture implicit across the roadmap, plan, and execution workflow implementations.

Audit mode: architectural discovery only. This document does not propose a replacement architecture, extraction sequence, implementation plan, class hierarchy, framework choice, or roadmap.

Evidence convention:

- **Evidence** statements are grounded in inspected source, prompts, tests, or existing repository documentation.
- **Inference** statements are conclusions drawn from evidence across multiple workflows.
- **Uncertainty** statements identify facts not established by the inspected repository.

## 1. Executive Summary

- **Evidence**: The repository contains three explicit workflow engines: `LoopRelay.Roadmap.Cli` with durable roadmap state and transition services, `LoopRelay.Plan.Cli` with a sequenced planning pipeline, and `LoopRelay.Cli` with the execution loop. Key entry points are `RoadmapStateMachine`, `PlanPipeline`, and `LoopRunner`.
- **Evidence**: The roadmap implementation already has explicit workflow-runtime concepts: durable state, startup/resume/unblock planning, prompt contracts, projection freshness, input snapshots, transition journals, lifecycle records, blockers, and recovery intents.
- **Evidence**: Plan and Execution do not expose the same named state-machine vocabulary, but they repeat the same orchestration shape: preflight gates, agent session execution, output gates, artifact mutation transactions, publication, cancellation translation, failure translation, and completion or pause outcomes.
- **Evidence**: A separately wired "Eval-Driven Roadmap Workflow" engine was not found. Evaluation-driven roadmap behavior appears in the roadmap prompt and projection layer: `SelectNextEpic.prompt` treats `.agents/roadmap/*.md` as a strategic hypothesis, allows new intermediary/split/revision outcomes, and uses candidate evaluation; project context includes evaluation-model and eval-details files; completion uses `EvaluateEpicCompletionAndDrift`.
- **Inference**: The architectural opportunity is not to merge domain workflows. It is to recognize a small common runtime shape that already exists beneath distinct domain transitions: resolve context, validate readiness, run a prompt or deterministic operation, parse or classify output, persist outputs, record evidence, update lifecycle/state, and block or recover with durable intent when unsafe.
- **Inference**: Architectural convergence is high around lifecycle, context, prompt execution, artifact effects, validation, evidence, and failure semantics. Convergence is lower around durable state representation: Roadmap uses named persisted states, Plan uses a fixed pipeline outcome, and Execution uses an open loop plus resumable decision-session history.
- **Uncertainty**: The repository does not prove whether the eval-driven roadmap is intended to become a separate CLI/state machine or remain a domain variant expressed through roadmap prompt contracts and projections.

## 2. Workflow Inventory

| Workflow | Evidence | Purpose | Inputs | Outputs | Responsibilities | Termination Conditions |
| --- | --- | --- | --- | --- | --- | --- |
| Traditional Roadmap Workflow | **Evidence**: `RoadmapStateMachine`, `RoadmapState`, `PromptContractRegistry`, `SelectNextEpicTransition`, `EpicPreparationAuditTransition`, `CreateNewEpicTransition`, `SplitEpicTransition`, `GenerateMilestoneDeepDivesTransition`. | Select and prepare an implementation epic from roadmap sources and current roadmap completion context. | Project context, `.agents/roadmap/*.md`, roadmap completion context, active/completed epic artifacts, projections, selection/audit evidence. | `.agents/core/roadmap-completion-context.md`, `.agents/selection.md`, `.agents/epic.md`, `.agents/specs/*.md`, lifecycle records, decision ledger, transition journal, blocker/evidence artifacts. | Bootstrap/update roadmap context; select next initiative; audit existing epic; create, split, realign, reimagine, or retire epics; generate milestone specs; persist state and recovery evidence. | Pauses at `MilestoneSpecsReady`, terminal selection states, `EvidenceBlocked`, `ExecutionBlocked`, `Failed`, `Cancelled`, or `Completed`. |
| Eval-Driven Roadmap Workflow | **Evidence**: no separate engine found; evaluation-driven semantics appear in `SelectNextEpic.prompt`, project context source contract, projection definitions, `EpicPreparationAudit.prompt`, and `EvaluateEpicCompletionAndDrift.prompt`. | Evaluate roadmap hypotheses against project context, projection criteria, dependency validity, strategic drift, and completion evidence before selecting or revising roadmap direction. | Same runtime inputs as Traditional Roadmap plus evaluation-specific project context and projection criteria. | Same runtime output classes as Traditional Roadmap: selection, active epic, milestone specs, evidence, blockers, decision ledger entries. | Select by evaluation rather than roadmap order alone; allow new intermediary epics, split epics, strategic investigation, or roadmap revision; audit dependency validity and drift. | Same observed runtime termination model as the roadmap state machine; exact separate eval-workflow termination is not established. |
| Plan Workflow | **Evidence**: `PlanPipeline`, `PlanSession`, `ReviewStep`, `PermissionedArtifactOperationStep`, `OneShotSteps`, `PreflightGate`. | Turn an epic/spec input into an executable plan, details, operational context, and milestone files. | `.agents/specs/epic.md`, optional `.agents/specs/*.md`, generated adversarial-review projection, repository context available to planning session. | `.agents/plan.md`, `.agents/operational_context.md`, `.agents/details.md`, `.agents/milestones/m*.md`, submodule/parent git publication. | Preflight clean planning state; write plan; generate adversarial review projection; review plan; revise plan; seed operational context; collect details; extract milestones; extract details; publish `.agents` artifacts. | `Completed`, `PreflightBlocked`, `Failed`, or `Cancelled`. |
| Execution Workflow | **Evidence**: `LoopRunner`, `DecisionSession`, `ExecutionStep`, `MilestoneGate`, `CommitGate`, `LoopArtifacts`, `CompletionCertificationService`. | Execute plan milestones through repeated decision and execution slices until completion, blockage, stall, cancellation, or failure. | `.agents/plan.md`, optional `.agents/details.md`, `.agents/operational_context.md`, `.agents/milestones/m*.md`, latest handoff, optional decisions, repository working tree, decision-session resume state. | Code/worktree changes, `.agents/decisions/decisions.md`, historical decisions, `.agents/handoffs/handoff.md`, historical handoffs, updated milestones, operational deltas/context, execution evidence, completion certification artifacts, commits/pushes. | Determine epic completion; maintain decision session; run execution turns; generate handoffs; rotate consumed artifacts; publish `.agents`; commit/push real repository changes; detect stall; run non-implementation and completion certification reviews. | `EpicCompleted`, `CompletionBlocked`, `Stalled`, `Cancelled`, or `Failed`. |

- **Inference**: The four requested workflows are best separated by domain purpose and entry contract, not by the number of executable projects. Traditional and Eval-Driven Roadmap share an observed runtime implementation while expressing different roadmap-domain behavior.
- **Uncertainty**: The exact code path that materializes Roadmap output into Plan's required `.agents/specs/epic.md` input is not proven. Roadmap generates `.agents/epic.md` and bundles under `.agents/specs/`; Plan preflight requires `.agents/specs/epic.md`.

## 3. Workflow Chain Analysis

- **Evidence**: The requested chains converge downstream:

```text
Traditional Roadmap Workflow
  -> Plan Workflow
  -> Execution Workflow

Eval-Driven Roadmap Workflow
  -> Plan Workflow
  -> Execution Workflow
```

- **Evidence**: Roadmap CLI currently pauses at `MilestoneSpecsReady`; `RoadmapResumePlanner` explicitly reports legacy execution-preparation states as no longer advanced by Roadmap CLI. `LoopRunner` owns active execution.
- **Evidence**: Plan CLI is reusable downstream of any upstream workflow that can provide its clean-start preflight inputs, especially `.agents/specs/epic.md` and no preexisting plan/context/details/milestones.
- **Evidence**: Execution CLI is reusable downstream of any workflow that can provide `.agents/plan.md`, operational context or plan seed, optional details, and milestone files with strict checkboxes.
- **Inference**: Roadmap workflows are entry workflows. Plan and Execution are reusable downstream workflows. Execution contains nested/reusable subflows: decision session, scoped operational-context transfer, execution slice, post-execution review, completion certification.
- **Inference**: The Roadmap-to-Plan convergence point is a prepared epic/spec artifact set. The Plan-to-Execution convergence point is an executable operational artifact set: plan, operational context, details, and milestone files.
- **Uncertainty**: The repository does not expose a single automated command that chains Roadmap -> Plan -> Execution end to end.

## 4. Canonical Workflow Contracts

### Roadmap to Plan

- **Evidence**: Roadmap contracts produce active epic and milestone/spec artifacts through `RoadmapArtifactPaths.ActiveEpic`, `RoadmapArtifactPaths.SpecsDirectory`, and `GenerateMilestoneDeepDivesTransition`.
- **Evidence**: Plan preflight requires `OrchestrationArtifactPaths.SpecsEpic` (`.agents/specs/epic.md`) and requires no existing `.agents/plan.md`, `.agents/operational_context.md`, `.agents/details.md`, or milestone files.
- **Evidence**: Roadmap milestone specs contain `Epic Path` references that are validated against `.agents/epic.md` by `RoadmapResumePlanner` and `InvariantValidator`.
- **Inference**: Plan requires a roadmap output that is more semantic than "a file exists": it needs an epic/spec basis cleanly placed in the planning input namespace and compatible with later milestone extraction.
- **Uncertainty**: Whether `.agents/epic.md` is copied, transformed, or separately authored into `.agents/specs/epic.md` is not established by inspected code.

### Plan to Execution

- **Evidence**: Plan writes `.agents/plan.md`, seeds `.agents/operational_context.md` from the revised plan, writes `.agents/details.md`, and extracts `.agents/milestones/m*.md`.
- **Evidence**: Execution reads plan/details, ensures operational context exists by copying plan if missing, and uses milestone files for completion and no-progress checks.
- **Evidence**: `MilestoneGate` requires at least one strict checkbox across milestone files and all strict checkboxes checked before epic completion.
- **Inference**: Execution requires plan artifacts to be machine-consumed, not merely explanatory. Milestones must contain trackable checklist items, and details/operational context must constrain execution behavior.

### Execution back to Roadmap/Completion

- **Evidence**: Completion certification runs `EvaluateEpicCompletionAndDrift`, writes evaluation evidence, routes closure recommendations, archives completed epics, and updates roadmap completion context.
- **Evidence**: Both Roadmap completion transition and Main CLI completion service use the same completion vocabulary around evaluation, policy validation, closure routing, archive/synthesis, and roadmap context update.
- **Inference**: Execution completion is not complete until independent certification validates milestone completion against repository reality and updates roadmap completion state.

## 5. Universal Workflow Concepts

| Concept | Evidence | Workflows Using It | Inference |
| --- | --- | --- | --- |
| Workflow | CLI entry orchestration exists in `RoadmapStateMachine`, `PlanPipeline`, and `LoopRunner`. | Roadmap, Plan, Execution. | A workflow is a bounded orchestration that drives one domain objective through ordered guarded steps and returns a terminal/paused outcome. |
| State | Roadmap has `RoadmapState`; Plan has `PlanOutcome`; Execution has `LoopOutcome` plus decision resume state and live artifacts. | All, with different representation. | State can be explicit enum state, outcome state, or recoverable artifact/session state. |
| Transition | Roadmap prompt transitions, Plan pipeline steps, Execution loop slices and decision transfers. | All. | A transition is a guarded unit of advancement from one observable condition to another, usually with artifacts/evidence. |
| Guard | Roadmap startup/resume validation, Plan preflight/gates, Execution milestone/stall/commit gates. | All. | Guarding is universal; guard sources differ by domain. |
| Context | Project context, prompt context, operational context, decision context, transition input snapshot. | All. | Context is the runtime input surface passed to agents and validators. |
| Prompt execution | Roadmap one-shot prompts, Plan persistent sessions and one-shot operations, Execution decision/execution sessions. | All. | Agent turn execution is a universal runtime activity. |
| Artifact effect | `.agents` writes, evidence files, lifecycle records, decisions, handoffs, operational context, git commits. | All. | Workflows communicate through durable artifacts and domain-specific side effects. |
| Validation | Projection validation, parser/policy validation, artifact gates, checklist gates, lifecycle/freshness gates. | All. | Validation is both pre-transition and post-transition. |
| Failure/blocker | `EvidenceBlocked`, `PlanStepException`, `LoopStepException`, completion blocked, stalled. | All. | Failures become operator-visible outcomes and often durable evidence. |
| Recovery/resume | Roadmap unblock, decision-session resume, execution artifact rotation, cancellation salvage, preflight rerun. | Roadmap and Execution explicitly; Plan mostly manual rerun after cleanup. | Recovery is present but unevenly formalized. |

- **Uncertainty**: A single named "Workflow", "Transition", or "Runtime" abstraction is not implemented across all workflows.

## 6. Domain Concepts vs Runtime Concepts

| Category | Concepts | Evidence | Notes |
| --- | --- | --- | --- |
| Domain | Roadmap completion context, strategic initiative, active epic, split epic, retired epic, epic preparation audit, milestone deep dives, completion/drift evaluation. | Roadmap prompt contracts, roadmap state enum, transition classes, prompt files. | These preserve roadmap-specific behavior and differ between Traditional and Eval-Driven semantics. |
| Domain | Plan, adversarial plan review, details, milestones, implementation-first planning. | Plan pipeline, prompts, review step, one-shot steps. | These are planning-domain concepts. |
| Domain | Decision session, execution slice, handoff, operational delta, milestone checkbox completion, stall. | `DecisionSession`, `ExecutionStep`, `LoopRunner`, `MilestoneGate`, `CommitGate`. | These are execution-domain concepts. |
| Runtime | Prompt execution, session lifecycle, projection generation/freshness, input hashing, artifact persistence, lifecycle tracking, evidence capture, journaling, validation, failure/blocker recording, resume/unblock. | Shared patterns across Roadmap, Plan, Execution, Projections, Completion. | These recur independently of domain vocabulary. |
| Mixed | Roadmap `RoadmapState` values such as `GenerateMilestoneDeepDives`, `ExecutionLoop`, `EvidenceBlocked`; Plan pipeline step names; Execution loop comments naming "LoopStart". | State enum, pipeline, loop runner. | Domain lifecycle and runtime lifecycle are sometimes encoded in the same state names. |
| Mixed | Completion certification. | Roadmap transition and Completion service. | It is domain-specific to epic closure, but runtime-adjacent because it validates cross-workflow termination. |

- **Inference**: The cleanest discovered distinction is not "roadmap vs plan vs execution"; it is "domain decision/output semantics" versus "runtime mechanics for safely advancing, validating, persisting, and recovering a transition."

## 7. Transition Discovery

- **Evidence**: Roadmap has explicit prompt transition wrappers with `TransitionStarted`, `TransitionCompleted`, `PromptCompleted`, and `TransitionFailed` journal records.
- **Evidence**: Roadmap has two prompt transition modes: normal transitions that persist target state on prompt completion, and promotion-candidate transitions that persist prompt completion before artifact promotion/post-processing.
- **Evidence**: Plan transitions are fixed sequence steps. They do not journal the same transition model, but each major step has a phase, agent operation, output gate, and publication point.
- **Evidence**: Execution transitions occur per loop iteration: completion gate, decision generation or skip, context publication, execution turn, post-execution review, decision retirement, handoff publication, commit/stall evaluation.
- **Inference**: Universal transition behavior includes phase announcement, input/context resolution, controlled agent/runtime invocation, output materialization, validation, effect publication, and outcome translation.
- **Inference**: Workflow-specific transition metadata includes roadmap prompt/projection/allowed decisions, plan operation permission profiles, and execution session route/cost/stall data.
- **Uncertainty**: Plan and Execution do not currently emit roadmap-style transition journals, so any canonical transition metadata beyond the observed common fields is inferred rather than implemented uniformly.

## 8. Transition Lifecycle

| Stage | Roadmap Evidence | Plan Evidence | Execution Evidence | Universal? |
| --- | --- | --- | --- | --- |
| Input resolution | `TransitionInputResolver`, prompt contracts, project context loader. | `PreflightGate`, artifact operation allowed reads. | `LoopArtifacts`, handoff/decisions reads, milestone reads. | Yes. |
| Validation before execution | Startup/resume planner, projection freshness, lifecycle, active epic validation. | Clean preflight, required input checks. | Epic-complete gate, decision projection freshness, operational context existence. | Yes. |
| Context construction | `RoadmapPromptContextBuilder`, projections. | `WritePlan`, review prompt, one-shot prompts. | Decision proposal prompt, execution prompts, operational context. | Yes. |
| Prompt/session execution | `RoadmapPromptRunner`. | `PlanSession`, `ReviewStep`, `PermissionedArtifactOperationStep`. | `DecisionSession`, `ExecutionStep`. | Yes. |
| Output parsing/classification | Selection parser, audit parser, bundle extractor, completion parser/policy/router. | Review verdict extraction, output gates. | Handoff existence, milestone checkbox counting, completion parser/policy/router. | Yes, but depth varies. |
| Persistence | Roadmap state, journal, lifecycle, decision ledger, evidence. | `.agents` writes and publisher commits. | decisions/handoffs history, resume state, telemetry, commits, evidence. | Yes. |
| Publication | Roadmap artifact store and lifecycle; storage sync exists. | Agents submodule and parent gitlink publication. | Agents submodule, parent commits/pushes. | Yes. |
| Recovery bookkeeping | Blockers, transition intent, unblock review evidence. | Transaction rollback for artifact operations; outcome only for pipeline. | Decision resume state, salvage publish, blocked completion evidence. | Yes, unevenly. |

- **Inference**: The smallest lifecycle supported by evidence is: resolve -> validate -> construct context -> execute -> materialize -> validate/parse -> persist/publish -> classify outcome -> record recovery information when blocked.

## 9. Context Discovery

| Context Kind | Evidence | Ownership | Lifetime | Scope | Consumers |
| --- | --- | --- | --- | --- | --- |
| Project context | `.agents/ctx` nine-file contract; roadmap and shared projection loaders. | Shared project context contract. | Repository-level, changes invalidate projections. | All prompt projections. | Roadmap, Plan projection, Execution decision projection, Completion. |
| Projection context | Projection artifacts and manifests keyed by runtime prompt. | Projection services/roadmap projection cache. | Regenerated or reused based on freshness. | One runtime prompt consumer. | Runtime prompts and validators. |
| Transition input snapshot | Roadmap transition input snapshot with artifact hashes, prompt context hash, secondary input hash, prompt policy identity. | Roadmap transition runtime. | Per roadmap transition. | Journaling, provenance, resume safety. | Roadmap transitions, selection provenance, completion routing. |
| Planning session context | Warm planning session between WritePlan and RevisePlan. | Plan workflow. | One planning pipeline run until explicit close. | Plan authoring and revision. | `PlanSession`. |
| Operational context | `.agents/operational_context.md`. | Plan seeds it; Execution evolves it. | Across execution loop and transfers. | Execution decision prompts. | `DecisionSession`, `ExecutionStep`. |
| Decision context | `decisions.md`, historical decisions, handoffs, decision-session thread state. | Execution workflow. | Per execution slice plus resumable thread state. | Execution-agent system prompt generation. | `DecisionSession`, `ExecutionStep`. |
| Completion context | Roadmap completion context and completion evaluation context. | Roadmap/Completion service. | Across epics. | Roadmap selection and closure evaluation. | Roadmap transitions, completion certification. |

- **Inference**: Context has ownership and lifetime independent of workflow boundaries. Project context is long-lived; transition input snapshots are per-transition; operational context evolves during execution; completion context persists across epic cycles.

## 10. Effect Taxonomy

| Effect | Evidence | Workflows |
| --- | --- | --- |
| Artifact writes | `.agents` paths in Roadmap, Plan, Execution, Completion. | All. |
| State persistence | Roadmap state store; decision resume SQLite/file store; telemetry SQLite/JSONL. | Roadmap, Execution. |
| Journal/history | Roadmap transition journal; decisions/handoffs/deltas numbered history; completion evidence. | Roadmap, Execution, Completion. |
| Lifecycle metadata | Roadmap artifact lifecycle store; active epic lifecycle updates. | Roadmap, Completion. |
| Evidence capture | Roadmap evidence directories, non-implementation review evidence, completion blocker/evaluation evidence. | Roadmap, Execution, Completion. |
| Projection manifest | Roadmap/shared projection manifests with validation/freshness. | Roadmap, Plan, Execution, Completion via projections. |
| Git/submodule publication | Plan publisher; Execution submodule publisher and commit gate. | Plan, Execution. |
| Artifact rollback | `ArtifactMutationTransaction` in Plan scoped operations and Execution decision transfer operations. | Plan, Execution. |
| Human review/HITL capture | HITL capture from roadmap artifacts, plan, decisions, non-implementation review decisions. | Roadmap, Plan, Execution. |
| Recovery bookkeeping | Blocker artifacts, transition intents, unblock review evidence, resume state clearing/writing. | Roadmap, Execution. |

- **Inference**: Persistence, evidence, publication, and recovery bookkeeping are runtime effects. The specific artifact paths and semantics remain domain effects.

## 11. Validation Taxonomy

- **Evidence**: Input validation exists in Roadmap prompt contracts and resume planning, Plan preflight, and Execution artifact reads.
- **Evidence**: Projection validation checks required titles/sections, intended consumer, forbidden runtime-state headings, freshness, and provenance.
- **Evidence**: Artifact validation includes active epic validation, milestone spec `Epic Path` checks, required output existence, no-deletes checks, and changed-guard checks.
- **Evidence**: Output validation includes selection parsing, audit parsing, split bundle interpretation, completion evaluation parsing, policy validation, completion routing, review verdict extraction, and handoff existence.
- **Evidence**: Invariant validation includes active epic lifecycle uniqueness and downstream active epic/spec consistency.
- **Evidence**: Workflow validation includes Roadmap startup/resume safety, Plan clean-start checks, Execution stall/completion gates.
- **Inference**: Validation naturally belongs at four runtime moments: before transition selection, before prompt execution, after output materialization, and before terminal closure.
- **Uncertainty**: Plan and Execution validation is not represented in a unified validation result model; it is expressed through exceptions, booleans, or outcomes.

## 12. Failure Taxonomy

| Failure Category | Evidence | Workflow Semantics |
| --- | --- | --- |
| Preflight failure | Roadmap project context preflight returns `PreflightBlocked`; Plan preflight returns `PreflightBlocked`. | Blocks before mutation. |
| Prompt/session failure | Agent turn not `Completed` in Roadmap, Plan, Decision, Execution. | Throws workflow-specific step exception; Roadmap can persist blocker first. |
| Parser/classification failure | Roadmap selection/audit/completion parsing, bundle extraction, completion parsing. | Blocks or fails after output exists; evidence often preserved. |
| Validation failure | Projection invalid/stale, active epic invalid, required output missing, no checkboxes, unchanged guard. | Blocks, rolls back scoped operations, or fails outcome. |
| Persistence/publication failure | Git commit/push errors, submodule publish errors, state-store errors. | Plan/Execution fail; Roadmap state persistence failures are workflow failures. |
| Infrastructure failure | Agent runtime, process runner, filesystem, SQLite/logical artifact issues. | Generally failed outcome or blocked verification. |
| Cancellation | Roadmap persists `Cancelled`; Plan returns `Cancelled`; Execution salvages submodule and returns `Cancelled`. | Terminal cancellation outcome with different durability. |
| No-progress failure | Execution no substantive change count exceeds threshold. | Returns `Stalled`, not generic failed. |
| Completion blocked | Non-implementation completion review or completion certification blocks. | Returns completion-blocked/pause and writes evidence. |

- **Inference**: Failure semantics are mostly "safe stop with evidence" rather than automatic retry. Retry happens by rerun/resume/unblock after evidence changes.

## 13. Recovery Model

- **Evidence**: Roadmap has the richest recovery model: persisted blockers, transition intent, evidence paths, `unblock`, unblock review evidence, and narrow recovery actions.
- **Evidence**: Roadmap recovery covers preflight blocker recovery, execution disposition repair, invalid completion certification repair, and a legacy execution runtime repair path. Some blocker intents are explicitly unsupported.
- **Evidence**: Roadmap resume validates incomplete transitions by requiring durable output paths before interpreting them.
- **Evidence**: Execution recovery includes decision-session resume state, clearing stale resume state when projection is stale or session resume fails, idempotent artifact rotation, and best-effort submodule salvage on failure/cancellation.
- **Evidence**: Plan recovery is mostly manual: preflight requires cleanup, scoped artifact operations rollback on failure, and failed runs return `Failed` without durable transition intent.
- **Inference**: Universal recovery concepts are blocker, evidence path, resume state, repair by rerun, and replay from durable artifacts. Roadmap formalizes these; Plan and Execution encode them through gates, rollback, resume stores, and idempotent artifact movement.
- **Uncertainty**: A cross-workflow recovery vocabulary is not implemented. The evidence supports common concepts, not a shared recovery contract.

## 14. Runtime Responsibilities

Observed responsibilities that recur below individual workflow domains:

- **Evidence**: Transition/session execution is performed by roadmap prompt runner/transition runner, plan sessions, scoped operation sessions, decision sessions, and execution sessions.
- **Evidence**: Context construction and projection freshness are required before many prompt executions.
- **Evidence**: Artifact persistence and output gates appear in every workflow.
- **Evidence**: Telemetry/resume/persistence concerns exist outside domain prompt content, especially roadmap state/journal and execution decision resume/telemetry.
- **Evidence**: Retry is mainly represented by safe rerun/resume after durable artifacts and freshness checks, not by blind automatic retry loops.
- **Evidence**: Lifecycle and evidence records are runtime-visible, especially roadmap lifecycle and completion/non-implementation evidence.
- **Inference**: Runtime responsibilities are the mechanics that preserve safe advancement: command entry/outcome mapping, state/resume planning, transition execution, prompt/session lifecycle, context/projection handling, validation gates, persistence, evidence capture, lifecycle updates, publication, cancellation handling, and recovery bookkeeping.
- **Uncertainty**: The exact boundary of "telemetry" as a canonical runtime responsibility is weaker in Roadmap/Plan than in Execution; telemetry infrastructure is explicit in `LoopRelay.Cli`.

## 15. Workflow-Specific Responsibilities

### Traditional Roadmap

- **Evidence**: Uses roadmap files, roadmap completion context, existing epic selection, active epic promotion, splitting, retirement, milestone deep dives.
- **Inference**: Its unique responsibility is preserving roadmap-domain strategic sequencing and epic preparation behavior.

### Eval-Driven Roadmap

- **Evidence**: Evaluation semantics are visible in prompt and projection text: roadmap as hypothesis, candidate evaluation matrix, dependency validity, strategic drift, completion/drift evaluation.
- **Inference**: Its unique responsibility is evaluating roadmap candidates and drift against project context and projection criteria without treating roadmap order as authoritative.
- **Uncertainty**: A separate runtime implementation for this responsibility is not present.

### Plan

- **Evidence**: Writes/revises plan, runs adversarial review, collects details, extracts milestones, extracts details, seeds operational context.
- **Inference**: Its unique responsibility is decomposing an epic/spec basis into executable operational artifacts.

### Execution

- **Evidence**: Decision routing, execution slices, handoff generation, operational context transfer, milestone completion gate, commit/push/stall behavior, completion certification.
- **Inference**: Its unique responsibility is implementing work against the repository while maintaining enough operational memory to continue safely.

## 16. Convergence Analysis

### Roadmap -> Plan Boundary

- **Evidence**: Roadmap produces an active epic and milestone/spec bundle under `.agents`; Plan requires a clean `.agents/specs/epic.md` input and no existing downstream artifacts.
- **Evidence**: Roadmap validates specs against `.agents/epic.md`; Plan reads `.agents/specs/*.md` during detail collection.
- **Inference**: The semantic boundary is "selected/prepared epic with executable spec material", not "roadmap state completed".
- **Uncertainty**: The exact file-level adapter between `.agents/epic.md` and `.agents/specs/epic.md` is not established.

### Plan -> Execution Boundary

- **Evidence**: Plan produces `.agents/plan.md`, `.agents/operational_context.md`, `.agents/details.md`, and `.agents/milestones/m*.md`.
- **Evidence**: Execution consumes those artifacts directly and checks milestone boxes for completion.
- **Inference**: The semantic boundary is "execution-ready operational artifact set". The plan must be self-contained enough to seed execution, and milestones must be machine-trackable.

### Execution -> Roadmap Completion Boundary

- **Evidence**: Execution only reports epic completion after milestone checkboxes are exhausted and completion certification closes the epic.
- **Evidence**: Completion certification updates roadmap completion context and archives completed epic evidence.
- **Inference**: The closure boundary is "certified completion with evidence", not merely "all milestone boxes checked".

## 17. Architectural Duplication

This section identifies duplicated orchestration behavior, not duplicated code.

- **Evidence**: Prompt execution orchestration appears in Roadmap one-shots, Plan author/review/scoped operations, Execution decision/execution/scoped operations, and Completion prompt runner.
- **Evidence**: Context/projection orchestration appears in Roadmap projection cache, shared projection service, Plan adversarial review projection, Execution decision projection, Completion evaluation/update projections.
- **Evidence**: Validation after model output appears in Roadmap parsers and validators, Plan output gates, Execution handoff/milestone gates, and Completion parser/policy/router.
- **Evidence**: Persistence/evidence orchestration appears in Roadmap state/journal/evidence, Plan artifact publication, Execution histories/resume/commits, and Completion evidence/archive/update.
- **Evidence**: Failure translation appears in each CLI as domain-specific outcomes and console messages.
- **Inference**: The repeated orchestration pattern is not the domain sequence; it is the lifecycle around each step: prepare inputs, run bounded agent/deterministic work, verify outputs, persist/publish, classify outcome, and preserve recovery evidence.

## 18. Architectural Vocabulary

| Term | Definition | Responsibilities | Relationships | Workflows |
| --- | --- | --- | --- | --- |
| Workflow | A bounded orchestration with an entry command and terminal/paused outcomes. | Sequence guarded work toward a domain objective. | Contains transitions/steps and effects. | All. |
| Transition | A unit of workflow advancement with inputs, execution, outputs, validation, and outcome. | Move workflow from one observable condition to another. | Uses context, effects, validation, recovery. | Explicit in Roadmap; implicit in Plan/Execution. |
| State | Durable or observable workflow position. | Represent current progress, pause, failure, or completion. | Drives resume/report behavior. | All, differently represented. |
| Guard | A precondition or postcondition that prevents unsafe advancement. | Validate readiness or output correctness. | Produces block/fail outcomes. | All. |
| Context | Structured input supplied to prompts, validators, or decisions. | Carry project, projection, operational, or transition-specific knowledge. | Has owner/lifetime/scope. | All. |
| Projection | Runtime-prompt-specific project-context derivative with freshness/provenance. | Constrain prompt behavior and validate relevance. | Generated from project context; consumed by prompts. | Roadmap, Plan, Execution, Completion. |
| Prompt Contract | A declaration of runtime prompt inputs, outputs, decisions, parser/writer, and stale projection policy. | Define semantic IO and validation expectations. | Roadmap-specific explicit contract; shared concept inferred elsewhere. | Roadmap explicitly; Plan/Execution implicitly. |
| Artifact | Durable file/logical record that carries workflow state or output. | Communicate across transitions and workflows. | Has lifecycle/evidence/provenance in some workflows. | All. |
| Evidence | Durable artifact explaining a decision, failure, completion, review, or blocker. | Support recovery, auditability, and closure. | Referenced by transition intent, blockers, certification. | Roadmap, Execution, Completion; Plan indirectly. |
| Lifecycle | Artifact readiness/executing/completed/blocked metadata. | Tell runtime whether an artifact is usable. | Used by Roadmap resume/invariant checks. | Roadmap primarily. |
| Blocker | Operator-visible condition that stops safe progression. | Preserve reason, required next step, and evidence. | May have recovery intent. | Roadmap/Completion explicitly; Plan/Execution through outcomes. |
| Recovery Intent | Persisted indication of how blocked state can be reviewed or repaired. | Route `unblock` or rerun behavior. | Coupled to evidence paths. | Roadmap explicitly; Execution has resume state instead. |

- **Uncertainty**: This vocabulary is canonical as a discovery artifact, not as implemented shared type names.

## 19. Canonical Runtime Model

- **Evidence**: The repository already expresses a runtime model through repeated behavior across CLIs and services:
  - Workflows expose bounded entry points and outcome enums or states.
  - Transitions resolve artifacts and context before execution.
  - Agent prompts run under explicit session posture and permissions.
  - Projections adapt project context to runtime-prompt consumers and track freshness.
  - Outputs are validated before downstream reuse.
  - Effects are persisted as artifacts, state, journals, history, evidence, lifecycle, or git publication.
  - Failures become blocked/failed/cancelled/stalled outcomes with recovery information where implemented.
- **Inference**: The smallest coherent runtime capable of expressing all observed workflows has these concepts: workflow, state/outcome, transition/step, guard, context, projection, prompt/session execution, artifact effect, validation result, evidence, blocker, recovery/resume state, and publication.
- **Inference**: The smallest coherent runtime has these relationships:
  - A workflow contains ordered or conditional transitions.
  - A transition consumes context and artifacts.
  - A transition may require a projection or prompt contract.
  - A transition executes an agent turn or deterministic operation.
  - A transition emits artifacts/evidence and updates observable state/outcome.
  - Validators guard both transition entry and transition exit.
  - Recovery state links a blocker to evidence and a safe next action.
  - Downstream workflow entry depends on semantic outputs, not upstream implementation type.
- **Inference**: The runtime invariants already emerging are:
  - Do not advance without required inputs.
  - Do not reuse stale projection-dependent outputs without freshness validation.
  - Do not treat prompt completion as artifact validity.
  - Do not treat milestone checkbox completion as epic closure without certification.
  - Preserve evidence when blocking or failing after meaningful output exists.
  - Keep domain-specific decisions in domain transitions while keeping execution, validation, persistence, and recovery mechanics consistent.
- **Uncertainty**: Because Plan and Execution lack roadmap-style journals and lifecycle state, the exact minimal persistence surface for a shared runtime remains a material open question.

## 20. Unknowns

- **Uncertainty**: Is the Eval-Driven Roadmap Workflow intended to be a separate executable workflow engine, or a roadmap-domain strategy implemented through prompts/projections inside the existing roadmap runtime?
- **Uncertainty**: What is the authoritative adapter from Roadmap's `.agents/epic.md` and generated specs to Plan's `.agents/specs/epic.md` preflight contract?
- **Uncertainty**: Is there intended automation for Roadmap -> Plan -> Execution chaining, or are these intentionally manual CLI boundaries?
- **Uncertainty**: Are Roadmap's legacy execution-preparation states compatibility states, future active states, or historical persisted-state values only?
- **Uncertainty**: Which recovery intents emitted by Roadmap are intentionally unsupported versus incomplete coverage?
- **Uncertainty**: Is completion certification intentionally split between a Roadmap transition and the shared Completion service invoked by Main CLI, or is one path intended as canonical?
- **Uncertainty**: What level of transition journal/evidence parity is expected for Plan and Execution?
- **Uncertainty**: Which artifact lifecycle concepts are roadmap-specific and which are runtime-wide?
- **Uncertainty**: What concurrency model governs simultaneous Roadmap, Plan, Execution, storage sync, and human edits to `.agents` artifacts?
- **Uncertainty**: How much prompt output schema strictness is guaranteed by prompt text versus enforced only after generation by parsers and validators?

End of audit.
