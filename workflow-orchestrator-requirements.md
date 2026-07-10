# Workflow Orchestrator Requirements Discovery

This document records requirements discovered from the current LoopRelay repository for a future workflow orchestrator. It is intentionally limited to observed facts, inferred requirements, unresolved ambiguities, authority relationships, and invariants.

It does not define an orchestrator architecture, API, class model, orchestration algorithm, state-machine design, or implementation plan.

Notation:

- **Evidence**: Directly observed in repository files, prompts, CLI surfaces, or issue records.
- **Inference**: Requirement implied by current behavior or cross-file relationships.
- **Uncertainty**: A gap, conflict, missing source of truth, or unresolved product boundary.

## 1. Executive Summary

**Evidence**

- LoopRelay currently exposes separate CLI entry points for planning, execution, and roadmap work:
  - `LoopRelay.Plan.Cli <REPO_DIR>`
  - `LoopRelay.Cli <REPO_DIR>`
  - `LoopRelay.Roadmap.Cli [status|run|unblock|storage-init|storage-import|storage-export|storage-sync|storage-verify] <REPO_DIR> [...]`
- There is no observed single CLI command that selects or sequences all workflows automatically.
- Current workflow progression is artifact-driven. Important runtime artifacts are under `.agents`, with structured runtime persistence under `.LoopRelay/persistence/looprelay.sqlite3` when SQLite has been initialized/imported and is considered valid.
- Roadmap workflow behavior is the most explicitly persisted workflow surface. It has structured roadmap state, artifact lifecycle, prompt contracts, projection manifests, transition journals, decision ledgers, storage verification, and storage sync commands.
- Plan workflow behavior is a fixed pipeline from `.agents/specs/epic.md` to `.agents/plan.md`, `.agents/details.md`, and `.agents/milestones/m*.md`.
- Main execution workflow behavior is a loop over plan/details/milestones, handoffs, decisions, operational context, real repository changes, milestone checkbox progress, completion review, and completion certification.
- Completion certification and roadmap completion evaluation use structured policies and route outcomes. Closing an epic is not authorized solely by all milestones being checked.

**Inference**

- A future workflow orchestrator must treat workflow discovery as a runtime fact-gathering activity across CLIs, artifacts, lifecycle metadata, SQLite state, Git state, prompt contracts, and journals.
- Workflow identity cannot be inferred from one file or one flag. Current workflow identity emerges from entry point, persisted state, required artifacts, active transition evidence, artifact lifecycle, and expected next outputs.
- Stage completion must be based on the authority model already present in each workflow, not on artifact existence alone.
- Recovery and rerun behavior must account for known partial-state and archive idempotency problems before declaring a workflow safe to continue.

**Uncertainty**

- "Traditional Roadmap" and "Eval-Driven Roadmap" are not first-class CLI command names or explicit workflow identifiers in the current code. Their boundaries must be inferred from transition semantics.
- The repository includes a `CreateNewRoadmap` prompt, but no current Roadmap CLI registration or command path was observed for it.
- The README states that structured runtime state is canonical in SQLite, while Roadmap CLI code can fall back to filesystem stores when the database is missing, empty, invalid, or not imported/canonical.
- No observed current surface gives a unified "workflow status" across Plan, Execution, Roadmap, Completion, storage sync, and open decision sessions.

## 2. Workflow Inventory

### 2.1 Plan Workflow

**Evidence**

- Entry point: `src/LoopRelay.Plan.Cli/Program.cs`.
- Invocation shape: `LoopRelay.Plan.Cli <REPO_DIR>`.
- Fixed pipeline in `PlanPipeline`:
  - Preflight.
  - Write Plan.
  - Generate Adversarial Review Projection.
  - Adversarial Review.
  - Revise Plan.
  - Seed `.agents/operational_context.md` from revised `.agents/plan.md`.
  - Collect Details.
  - Extract Milestones.
  - Extract Details.
  - Record parent gitlink when `.agents` committed.
- Required starting input: `.agents/specs/epic.md`.
- Preflight blocks if any of these already exist:
  - `.agents/plan.md`
  - `.agents/operational_context.md`
  - `.agents/details.md`
  - non-empty `.agents/milestones`
- Produced planning artifacts include:
  - `.agents/plan.md`
  - `.agents/operational_context.md`
  - `.agents/details.md`
  - `.agents/milestones/m*.md`
- `PlanSession` keeps a warm two-turn planning session for `WritePlan` and `RevisePlan`.
- One-shot artifact operations verify required outputs and changed artifacts after the step.

**Inference**

- Plan workflow purpose is to convert an active epic specification into execution-ready planning artifacts.
- Plan workflow completion requires all expected planning artifacts and successful milestone extraction, not only the existence of `.agents/plan.md`.
- Plan workflow has no observed durable stage-resume command. Partial progress after failure is discoverable from artifacts but not fully authoritative.
- Plan workflow is upstream of main execution because main execution consumes plan, details, milestones, and operational context.

**Uncertainty**

- There is no explicit Plan workflow state document equivalent to Roadmap state.
- There is no observed Plan command for resume, repair, force rerun, or status.
- The precise treatment of partially written Plan outputs after a failed pipeline is not encoded as a durable recovery contract.

### 2.2 Main Execution Workflow

**Evidence**

- Entry point: `src/LoopRelay.Cli/Program.cs`.
- Invocation shape: `LoopRelay.Cli <REPO_DIR>`.
- Exit codes:
  - `0`: epic completed.
  - `4`: completion blocked.
  - `130`: cancelled.
  - `3`: stalled.
  - `1`: failed.
- `LoopRunner` performs a repeated execution loop over milestones, decisions, handoffs, repository changes, reviews, and completion certification.
- `MilestoneGate` treats an epic as complete only when at least one strict markdown checkbox exists in `.agents/milestones/m*.md` and all strict checkboxes outside fenced code blocks are checked.
- `CommitGate` treats progress as real non-`.agents` Git changes or a reduction in unticked milestone items.
- `.agents` submodule changes are filtered out as real implementation progress.
- `ExecutionStep` uses a held-open two-turn operational session:
  - execution turn from plan/details/decisions or plan/details;
  - handoff generation turn.
- `DecisionSession` is read-only, can persist/resume its Codex session thread in SQLite, and auto-submits agent proposals to decision history and live decisions.
- The execution workflow rotates handoffs, decisions, and operational deltas into history files.
- Completion path includes non-implementation review, delete-decision handling, completion certification, archive synthesis, and roadmap completion context update.

**Inference**

- Main execution workflow purpose is to apply implementation changes until milestone completion and certification close the active epic.
- Main execution workflow start requirements include plan/details/milestones and either seeded or existing operational context.
- Main execution workflow can resume implicitly from artifacts, handoff history, live decisions, SQLite decision-session resume state, and Git state.
- Main execution completion is a compound event:
  - milestone gate reports complete;
  - non-implementation completion review does not block;
  - completion certification policy and route allow closure;
  - archive and roadmap context update complete.

**Uncertainty**

- There is no observed explicit execution status command.
- There is no observed command to select a specific execution stage, repair a failed execution turn, or force a main execution rerun.
- Existing issue records document idempotency and archive retry risks after completion archive movement.

### 2.3 Traditional Roadmap Workflow

**Evidence**

- Entry point: `src/LoopRelay.Roadmap.Cli/Program.cs`.
- Roadmap command surface supports `status`, `run`, `unblock`, and storage commands.
- `RoadmapStateMachine` handles selection and preparation of active epic work from roadmap sources and completion context.
- `SelectNextEpic` prompt consumes:
  - roadmap completion context;
  - `.agents/roadmap/*.md`;
  - Project Context;
  - projection context.
- `SelectNextEpic` decisions include:
  - Select Existing Epic.
  - Select New Intermediary Epic.
  - Select Split Epic.
  - Strategic Investigation Required.
  - Roadmap Revision Required.
  - No Suitable Initiative.
- Epic preparation can audit, realign, reimagine, retire, create, or split epics.
- `GenerateMilestoneDeepDivesForEpic` materializes `.agents/specs` milestone files and stops with `MilestoneSpecsReady`.
- `RoadmapResumePlanner` treats `MilestoneSpecsReady` as a terminal pause: "Milestone specs are ready; Roadmap CLI stops before execution context generation."

**Inference**

- "Traditional Roadmap" is best identified as the roadmap selection and epic-preparation workflow that selects or creates an active epic and produces milestone specs for downstream planning/execution.
- Its purpose is to advance from strategic roadmap inputs to execution-preparable epic artifacts.
- Its downstream boundary is artifact-based, especially `.agents/specs` and `MilestoneSpecsReady`.

**Uncertainty**

- "Traditional Roadmap" is not an encoded workflow name, command, enum value, or prompt contract key.
- The exact product boundary between "traditional" roadmap selection and broader roadmap state behavior is not explicitly documented in code.
- The current Roadmap CLI does not advance legacy execution-preparation states beyond `MilestoneSpecsReady`; the handoff to Plan or main execution is not represented by one unified command.

### 2.4 Eval-Driven Roadmap Workflow

**Evidence**

- Completion and evaluation flow appears in:
  - `CompletionCertificationService`
  - Roadmap `CompletionCertificationTransition`
  - `EvaluateEpicCompletionAndDrift` prompt contract
  - `UpdateRoadmapCompletionContext` prompt contract
  - `RoadmapCompletionRouteMapper`
- Evaluation inputs include active epic, execution evidence, milestone spec inputs, projection context, and completion review evidence when applicable.
- Completion certification policy recognizes statuses:
  - Fully Complete.
  - Functionally Complete.
  - Partially Complete.
  - Not Complete.
  - Inconclusive.
- Drift classifications include:
  - None.
  - Positive.
  - Negative.
  - Mixed.
  - Unknown.
- Closure recommendations route to:
  - Close Epic.
  - Close With Follow-Up.
  - Continue Epic.
  - Reopen Epic.
  - Gather More Evidence.
- Closing routes update roadmap completion context and return toward selection of next strategic initiative.

**Inference**

- "Eval-Driven Roadmap" is best identified as the roadmap behavior that evaluates execution evidence and drift before updating roadmap completion context and choosing the next route.
- Its purpose is to ensure future roadmap selection incorporates completion outcomes, evidence, drift, and archive synthesis rather than milestone checkboxes alone.
- Its entry condition is an execution completion claim or evidence requiring evaluation.

**Uncertainty**

- "Eval-Driven Roadmap" is not an encoded workflow name, command, enum value, or prompt contract key.
- Whether eval-driven behavior is a separate workflow, a Roadmap subworkflow, or a completion phase is not currently explicit.
- The same completion-evaluation concepts appear both in the main CLI completion service and Roadmap CLI completion transition, creating a boundary that must be treated carefully.

## 3. Workflow Identity

### 3.1 Identity Signals Observed

**Evidence**

| Signal | Where Observed | Authority |
| --- | --- | --- |
| CLI binary | `LoopRelay.Plan.Cli`, `LoopRelay.Cli`, `LoopRelay.Roadmap.Cli` | Strong entry-point identity for an invoked process |
| Roadmap command | `status`, `run`, `unblock`, storage commands | Strong command identity inside Roadmap CLI |
| Roadmap persisted state | `.agents/state.json`, SQLite `roadmap_state` when active | Strong Roadmap workflow-stage identity |
| Prompt contract key | `PromptContractRegistry` | Strong identity for Roadmap prompt-mediated transitions |
| Projection identity and freshness | projection manifest | Strong validity signal for Roadmap projections |
| Artifact lifecycle | `.agents/artifacts/lifecycle.json` or SQLite lifecycle store | Strong artifact usability signal |
| Artifact paths | `.agents/plan.md`, `.agents/specs`, `.agents/milestones`, `.agents/handoffs`, etc. | Supporting identity signal |
| Git state | non-`.agents` `git status --porcelain` | Strong execution-progress signal |
| Decision resume state | SQLite `decision_session_resume` | Strong session-resume signal for decision workflow only |

### 3.2 Identity Requirements

**Inference**

- Workflow identity discovery must distinguish invoked workflow from persisted workspace workflow. A user can invoke a CLI while the workspace contains artifacts from another workflow.
- Workflow identity discovery must support at least these current identities:
  - Plan.
  - Main execution.
  - Roadmap selection/preparation.
  - Roadmap completion evaluation.
  - Completion certification.
  - Storage synchronization and verification.
  - Decision-session resume.
- Workflow identity cannot depend solely on artifact existence because stale, archived, superseded, blocked, or partially written artifacts can exist.
- Workflow identity must record whether an identity came from evidence or inference.
- Workflow identity must allow multiple active or relevant identities in one workspace, such as Roadmap `MilestoneSpecsReady` plus pending Plan workflow, or main execution completion plus roadmap context update.

**Uncertainty**

- There is no current canonical workflow identity registry.
- There is no current cross-workflow state document that names the active workflow.
- The labels "Traditional Roadmap" and "Eval-Driven Roadmap" require semantic mapping rather than direct lookup.

## 4. Workflow Discovery Requirements

### 4.1 Required Discovery Domains

**Evidence**

Current workflow status is distributed across:

- CLI invocation arguments.
- `.agents` files and directories.
- `.agents/artifacts/lifecycle.json`.
- `.agents/state.json` and legacy `.agents/state.md`.
- `.agents/journal/transitions.jsonl`.
- `.agents/decision-ledger.json` and legacy markdown.
- `.agents/projections/manifest.json` and projection markdown files.
- `.agents/contracts/prompt-contracts.md`.
- `.agents/evidence/**`.
- `.LoopRelay/persistence/looprelay.sqlite3`.
- `.LoopRelay` legacy compatibility files during migration.
- Git working-tree state.
- Environment variables such as `LoopRelay_DECISION_RESUME` and `LoopRelay_SESSION_LOG`.

### 4.2 Discovery Signal Categories

**Inference**

Workflow discovery must classify signals into these categories:

| Category | Examples | Required Treatment |
| --- | --- | --- |
| Authoritative state | Roadmap current state, valid SQLite state, lifecycle status | Can determine current stage when fresh and valid |
| Authoritative progress | non-`.agents` Git changes, milestone checkbox reduction, completion policy route | Can determine real progression |
| Required input | active epic, roadmap source, Project Context, plan/details, milestone specs | Missing input blocks specific stages |
| Required output | selection, active epic, milestone specs, plan, details, handoff, evaluation evidence | Presence alone may not prove completion |
| Freshness marker | projection manifest, input hashes, prompt hashes, project context hash | Stale marker blocks or invalidates transitions |
| Compatibility export | legacy markdown, JSONL exports | May be read or migrated but may not be canonical |
| Recovery marker | transition journal, blocker evidence, workflow transaction marker | Required for retry and failure classification |
| User override | `--force`, `--elevated`, env flags, `unblock` | Must be explicit and scoped to current CLI semantics |

### 4.3 Discovery Conflicts

**Evidence**

- SQLite can be valid imported/canonical, valid empty, missing, corrupt, unsupported, or incompatible.
- Roadmap CLI uses SQLite-backed stores only when the database status is valid imported or valid canonical; otherwise it uses filesystem stores.
- Storage sync detects stale export, conflict, divergent changes, missing exports, and unresolved references.
- Verification can detect stale/corrupt runtime decision resume and telemetry state.

**Inference**

- Discovery must preserve conflicting signals instead of collapsing them into a single status too early.
- Discovery must surface when SQLite and filesystem disagree, because the current code treats some disagreement as conflict requiring force or verification.
- Discovery must not infer stage completion from filesystem exports when SQLite is canonical and exports are stale.

**Uncertainty**

- The exact cross-workflow precedence between README-level SQLite canonicality and Roadmap CLI fallback behavior is not documented as a single rule.

## 5. Stage Discovery Requirements

### 5.1 Roadmap Stages

**Evidence**

Roadmap state values include:

- `CoreReady`
- `BootstrapRoadmapCompletionContext`
- `RoadmapCompletionContextReady`
- `SelectNextStrategicInitiative`
- `ExistingEpicSelected`
- `NewEpicProposed`
- `SplitEpicProposed`
- `EpicPreparationAudit`
- `RealignEpic`
- `ReimagineEpic`
- `RetireEpic`
- `EvidenceBlocked`
- `EvidenceGathering`
- `CreateNewEpic`
- `SplitEpic`
- `SplitChildSelection`
- `ActiveEpicReady`
- `GenerateMilestoneDeepDives`
- `MilestoneSpecsReady`
- `GenerateOperationalContext`
- `OperationalContextReady`
- `GenerateExecutionPrompt`
- `ExecutionPromptReady`
- `ExecutionLoop`
- `ExecutionBlocked`
- `EpicCompletionDetected`
- `CompletionEvaluationAndContextUpdate`
- `StrategicInvestigationRequired`
- `RoadmapRevisionRequired`
- `NoSuitableInitiative`
- `Completed`
- `Failed`
- `Cancelled`

Roadmap resume logic classifies some states as terminal pauses or report-only states.

**Inference**

- Roadmap stage discovery must use persisted state first, then validate it against artifacts, lifecycle, projection readiness, and transition journal evidence.
- Roadmap stage discovery must distinguish terminal pause from failure. Examples include `MilestoneSpecsReady`, `StrategicInvestigationRequired`, `RoadmapRevisionRequired`, and `NoSuitableInitiative`.
- Roadmap stage discovery must identify `Cancelled` as a resumable condition using transition intent where present.
- Roadmap stage discovery must identify `EvidenceBlocked` as requiring blocker evidence and specific recovery handling.

**Uncertainty**

- Not every Roadmap state has a general-purpose `unblock` recovery path.

### 5.2 Plan Stages

**Evidence**

Plan stages are encoded as sequential pipeline steps rather than persisted named states.

Required stage evidence includes:

- Preflight clean output area.
- `.agents/specs/epic.md`.
- `.agents/plan.md`.
- adversarial review projection and review output.
- revised `.agents/plan.md`.
- `.agents/operational_context.md`.
- `.agents/details.md`.
- `.agents/milestones/m*.md`.
- parent gitlink record when `.agents` committed.

**Inference**

- Plan stage discovery must be artifact-based because no durable Plan state document exists.
- Plan stage discovery must treat existing outputs as a preflight blocker for a fresh Plan run, not as automatic permission to continue.
- Plan stage discovery must record ambiguity when some but not all outputs exist.

**Uncertainty**

- Current code does not define authoritative semantics for resuming after partial Plan output creation.

### 5.3 Main Execution Stages

**Evidence**

Main execution stages are encoded in loop behavior and artifacts rather than a persisted named execution state.

Stage evidence includes:

- milestone checkbox status;
- live decisions at `.agents/decisions/decisions.md`;
- decision history;
- latest handoff;
- operational context;
- operational deltas;
- non-implementation review state;
- completion certification evidence;
- completed epic archive;
- Git working-tree changes excluding `.agents`;
- SQLite decision-session resume state.

**Inference**

- Main execution stage discovery must derive status from a combination of milestone gate, decision/handoff artifacts, Git status, review evidence, certification evidence, and archive state.
- Main execution discovery must distinguish:
  - ready to execute first slice;
  - ready to continue from handoff;
  - ready to execute live decisions;
  - in completion-review/certification path;
  - blocked completion certification;
  - stalled due to repeated no-progress loops;
  - archived or partially archived completion.

**Uncertainty**

- Main execution does not currently expose a status command or structured state document equivalent to Roadmap state.

### 5.4 Storage and Persistence Stages

**Evidence**

Roadmap CLI storage commands include:

- `storage-init`
- `storage-import`
- `storage-export`
- `storage-sync`
- `storage-verify`

SQLite validation statuses include:

- Missing.
- ValidEmpty.
- ValidImported.
- ValidCanonical.
- Corrupt.
- UnsupportedSchema.
- IncompatiblePartialState.

Storage result categories include:

- Initialized.
- Imported.
- Exported.
- Unchanged.
- StaleExport.
- Conflict.
- UnsupportedVersion.
- ValidationFailure.
- VerificationFailed.

**Inference**

- Storage stage discovery must precede any stage decision that depends on SQLite/file authority.
- Storage verification is a read-only discovery input and must be distinguishable from mutation commands.
- Storage sync and force options are override surfaces, not hidden automatic repairs.

**Uncertainty**

- A unified workflow orchestrator would need to know whether storage verification is mandatory before every workflow decision; current CLIs do not require it uniformly.

## 6. Workflow Boundary Requirements

### 6.1 Observed Boundaries

**Evidence**

| Boundary | Upstream Output | Downstream Input | Notes |
| --- | --- | --- | --- |
| Roadmap selection/preparation to Plan | `.agents/specs/*.md`, active epic context, `MilestoneSpecsReady` | Plan preflight input `.agents/specs/epic.md` | Current Roadmap stops before execution context generation |
| Plan to Main Execution | `.agents/plan.md`, `.agents/details.md`, `.agents/milestones/m*.md`, `.agents/operational_context.md` | Main execution loop | Plan preflight blocks if these outputs already exist |
| Main Execution slice to next slice | handoff, decisions history, operational context/delta, Git state | next loop iteration | Live handoff/decision artifacts are rotated |
| Main Execution to Completion Certification | milestone gate complete, execution evidence, review evidence | completion service or Roadmap completion transition | Completion is policy-routed |
| Completion Certification to Roadmap Context Update | completion evaluation, archive synthesis, route | `UpdateRoadmapCompletionContext` | Close routes update roadmap completion context |
| Roadmap Context Update to Next Selection | updated roadmap completion context | `SelectNextEpic` | Current state returns toward selection |
| Filesystem to SQLite | import/sync snapshot | SQLite-backed runtime stores | Only valid imported/canonical DB activates SQLite-backed stores |
| SQLite to Filesystem | export/sync snapshot | compatibility files | Export may be stale or conflicting |

### 6.2 Boundary Requirements

**Inference**

- Workflow boundary detection must identify both output presence and output usability.
- A boundary is not complete merely because a downstream file exists; lifecycle, manifest freshness, validation, and policy route can alter authority.
- Boundary detection must preserve whether the downstream workflow is allowed to start under current preflight rules.
- Boundary detection must not mutate boundary artifacts during discovery.

**Uncertainty**

- There is no single cross-workflow document declaring which workflow owns each boundary artifact at every phase.

## 7. Cross-Workflow Contracts

### 7.1 Contract Inventory

**Evidence**

| Contract | Producer | Consumer | Contract Evidence |
| --- | --- | --- | --- |
| Project Context | user/workspace `.agents/ctx` files | Roadmap projections and prompts | exactly nine canonical numbered files; extras/missing files block |
| Roadmap source | `.agents/roadmap/*.md` | `SelectNextEpic` | required input but advisory content in prompt semantics |
| Roadmap completion context | roadmap bootstrap/update | `SelectNextEpic`, completion updates | current strategic state input |
| Selection | `SelectNextEpic` | audit/create/split/realign/reimagine | selection provenance and decision parser |
| Active epic | promotion coordinator | milestone generation, completion evaluation | validated epic format and lifecycle |
| Milestone specs | Roadmap milestone deep dives | Plan workflow and completion evidence | materialized under `.agents/specs` |
| Plan | Plan workflow | Main execution | `.agents/plan.md`, revised after adversarial review |
| Details | Plan workflow | Main execution | `.agents/details.md` |
| Milestones | Plan workflow | Main execution and completion gate | `.agents/milestones/m*.md`, strict markdown checkboxes |
| Operational context | Plan and main execution | Main execution and decisions | seeded from plan, later updated via scoped operations |
| Decisions | Decision session | Main execution | live and numbered decisions files |
| Handoff | Main execution | next execution/decision turn | live and numbered handoffs |
| Execution evidence | Main execution/Roadmap bridge | completion evaluation | evidence paths and hashes |
| Completion evaluation | certification prompt | route mapper and roadmap context update | policy-validated status/drift/recommendation |
| Archive synthesis | completed epic archive | roadmap completion context update | archive index and synthesis path |
| Projection contracts | Roadmap prompt contract registry | Roadmap transitions | required inputs/outputs, parser, stale policy |

### 7.2 Contract Requirements

**Inference**

- Cross-workflow contracts must preserve required input paths, required output paths, lifecycle status, hashes, prompt identity, projection identity, and parser outcomes when available.
- Contract discovery must identify whether an artifact is canonical, compatibility export, legacy migrated input, or derived projection.
- Contract discovery must recognize that prompt completion is not necessarily artifact promotion. Promotion and validation can still fail after prompt output exists.
- Contract discovery must treat non-promotable or invalid outputs as evidence/blocker material, not as successful workflow completion.

**Uncertainty**

- Some contracts are enforced by prompts and parsers rather than a shared typed contract registry across all CLIs.

## 8. Workflow Progression Requirements

### 8.1 Current Progression Signals

**Evidence**

- Roadmap progression is persisted through state documents, transition journal entries, decision ledger entries, lifecycle updates, and projection manifests.
- Plan progression is sequential within a single run and verified by required artifact outputs.
- Main execution progression is based on real non-`.agents` Git changes, milestone checkbox reduction, and completion certification routes.
- Storage progression is based on validation, import/export/sync result categories, hashes, and markers.

### 8.2 Progression Requirements

**Inference**

- Progression discovery must answer:
  - what workflow appears active;
  - what stage is current;
  - what stage was last attempted;
  - what evidence was produced;
  - what required inputs are missing;
  - what outputs are stale, invalid, or blocked;
  - what user override, if any, is required;
  - what downstream workflow, if any, is eligible.
- Progression must distinguish:
  - completed;
  - paused by design;
  - blocked by evidence;
  - failed;
  - cancelled;
  - stale;
  - partially completed;
  - ambiguous.
- Progression must not treat Roadmap terminal pause states as failures.
- Progression must not treat Plan preflight blockers as completion without validating downstream contract needs.
- Progression must not treat milestone checkbox completion as epic closure without completion certification.

**Uncertainty**

- There is no current single source of truth for cross-workflow progression order.

## 9. Stage Completion Requirements

### 9.1 Roadmap Stage Completion

**Evidence**

- Roadmap transitions journal `TransitionStarted`, `TransitionCompleted`, `PromptCompleted`, `TransitionFailed`, and other events.
- Prompt transitions save input snapshots, output paths, prompt/projection identities, parser decisions, and result status.
- Projection cache validates projections and blocks stale projections under stale policy `Block`.
- Active epic promotion requires validation before active epic readiness.
- Milestone deep-dive generation records materialization evidence and saves `MilestoneSpecsReady`.

**Inference**

- A Roadmap stage is complete only when the corresponding transition evidence, required outputs, lifecycle changes, parser outcomes, and persisted state agree.
- A `PromptCompleted` transition must not be treated as final stage completion when promotion or validation remains.
- A Roadmap terminal pause is a successful stopping point only if required artifacts and state are valid for that pause.
- `EvidenceBlocked` completion means blocker evidence has been captured, not that the target business workflow completed.

### 9.2 Plan Stage Completion

**Evidence**

- Each Plan artifact operation verifies required outputs and changed artifacts.
- `ExtractMilestones` requires nonempty milestone files, at least one strict checkbox, and a changed plan.
- `CollectDetails` and `ExtractDetails` require `.agents/details.md`.
- `PlanSession` verifies `.agents/plan.md` exists and is non-whitespace after write/revise turns.

**Inference**

- A Plan stage is complete only if its required artifact verification has passed.
- Final Plan completion requires plan, details, operational context, and milestones to be present and usable for main execution.
- Partial Plan output must be treated as ambiguous unless there is evidence the whole pipeline succeeded.

### 9.3 Main Execution Stage Completion

**Evidence**

- Execution slice completion includes execution turn, handoff generation, post-execution review, artifact publication, commit/push handling, and stall evaluation.
- Completion path requires milestone gate, non-implementation review, completion certification, archive synthesis, and roadmap context update.
- `CommitGate` declares stalled after repeated no-progress loops.

**Inference**

- An execution slice is complete only when both implementation work and handoff/review/publish handling complete.
- Main execution completion requires successful completion certification route, not only milestone gate success.
- Completion-blocked and stalled outcomes are terminal for the process invocation but not equivalent to completed epic closure.

### 9.4 Storage Stage Completion

**Evidence**

- Storage commands return structured result categories and can detect validation failure, verification failure, stale export, and conflicts.
- `storage-verify` is read-only and can detect workflow transaction marker problems, unresolved references, stale/corrupt runtime state, and nondeterministic roundtrips.

**Inference**

- Storage stage completion requires a non-conflict result appropriate to the command.
- Verification failure must be treated as a discovery result that can block or qualify later workflow decisions.

## 10. Detection Inputs

### 10.1 Files and Directories

**Evidence**

Important artifact roots and files include:

- `.agents`
- `.agents/specs/epic.md`
- `.agents/specs/*.md`
- `.agents/plan.md`
- `.agents/details.md`
- `.agents/operational_context.md`
- `.agents/operational_delta.md`
- `.agents/deltas`
- `.agents/decisions/decisions.md`
- `.agents/decisions/history`
- `.agents/handoffs/handoff.md`
- `.agents/handoffs/history`
- `.agents/milestones/m*.md`
- `.agents/state.json`
- `.agents/state.md`
- `.agents/decision-ledger.json`
- `.agents/decision-ledger.md`
- `.agents/artifacts/lifecycle.json`
- `.agents/artifacts/lifecycle.md`
- `.agents/roadmap/*.md`
- `.agents/selection.md`
- `.agents/selection-provenance-manifest.json`
- `.agents/epic.md`
- `.agents/core/roadmap-completion-context.md`
- `.agents/projections/*.md`
- `.agents/projections/manifest.json`
- `.agents/projections/manifest.md`
- `.agents/contracts/prompt-contracts.md`
- `.agents/journal/transitions.jsonl`
- `.agents/evidence/**`
- `.agents/archive/epics/**`
- `.LoopRelay/persistence/looprelay.sqlite3`

### 10.2 Structured Stores

**Evidence**

SQLite tables include:

- `schema_metadata`
- `workspace_metadata`
- `sync_markers`
- `decision_ledger`
- `roadmap_state`
- `artifact_lifecycle`
- `split_families`
- `split_children`
- `split_dependency_order`
- `execution_preparation_manifest`
- `selection_provenance_manifest`
- `projection_manifest_entries`
- `transition_journal`
- `loop_history`
- `execution_evidence`
- `completed_epic_archives`
- `completed_epic_records`
- `workflow_transactions`
- `decision_session_resume`
- `session_telemetry_events`

### 10.3 Runtime and External Inputs

**Evidence**

Detection also depends on:

- current CLI command and arguments;
- current environment variables;
- Git working-tree status;
- `.agents` submodule state;
- Codex session resume metadata for decision sessions;
- prompt identities and source hashes;
- Project Context hashes;
- causal input hashes;
- projection trust/freshness;
- cancellation state during an invocation.

**Inference**

- Detection inputs must be captured with enough provenance to explain why a workflow or stage was identified.
- Detection must not depend on hidden in-memory state after process exit unless that state is persisted.

## 11. Signal Authority

### 11.1 Authority Relationships

**Evidence**

| Domain | Stronger Authority | Weaker or Supporting Signal |
| --- | --- | --- |
| Roadmap current stage | persisted Roadmap state plus validation | artifact existence alone |
| Roadmap prompt readiness | prompt contract, projection freshness, required input/output checks | current state alone |
| Active epic usability | validated artifact plus lifecycle | unvalidated markdown file |
| Main execution progress | non-`.agents` Git changes and milestone checkbox reduction | `.agents` submodule changes |
| Epic closure | completion certification policy and route | milestone gate alone |
| Decision resume | SQLite decision resume state when enabled and fresh | legacy JSON resume file |
| Storage canonicality | valid imported/canonical SQLite database | stale filesystem exports |
| Projection trust | projection manifest with matching identities and hashes | projection markdown file alone |

### 11.2 Authority Requirements

**Inference**

- Signal authority must be explicit in discovery output because multiple signals can disagree.
- Lower-authority signals can support diagnosis but must not override higher-authority signals without an explicit current override surface.
- When the code supports fail-open behavior, such as decision resume persistence errors, discovery must report degraded authority rather than hard completion.
- Lifecycle state must qualify artifact usability. Ready, Executing, and Completed can be usable in observed logic; Blocked, Archived, Superseded, Draft, or Missing can change eligibility.

**Uncertainty**

- The complete cross-workflow precedence order between SQLite and filesystem compatibility artifacts is not encoded as one shared authority table.

## 12. Progress Signals

### 12.1 Positive Progress

**Evidence**

- Roadmap:
  - completed transition journal entry;
  - state advance;
  - decision ledger entry;
  - lifecycle update;
  - valid projection manifest entry;
  - successful artifact promotion;
  - milestone specs materialized.
- Plan:
  - required outputs written and verified;
  - plan revised after adversarial review;
  - details and milestones extracted;
  - milestone files contain strict checkboxes.
- Main execution:
  - non-`.agents` Git changes;
  - reduction in unticked milestone items;
  - generated handoff;
  - accepted decision file;
  - commit/push of real changes;
  - completion certification route allowing closure.
- Storage:
  - successful initialization/import/export/sync;
  - verification without blocking findings.

### 12.2 Negative or Blocking Signals

**Evidence**

- Roadmap stale projection under stale policy `Block`.
- Missing Project Context canonical files or extra numbered Project Context files.
- Invalid active epic format.
- Non-promotable prompt output.
- EvidenceBlocked state.
- Storage conflict/stale export/unsupported schema/corrupt DB.
- Plan preflight output collision.
- Missing Plan input `.agents/specs/epic.md`.
- Main execution no-progress count above threshold.
- Completion policy invalid route.
- Completion review blocked.
- Partial completion archive/idempotency issues recorded in issue files.

### 12.3 Ambiguous Signals

**Inference**

- Existing Plan outputs without a successful whole-pipeline marker are ambiguous.
- Existing main execution artifacts without a structured execution state are ambiguous until combined with milestone and Git state.
- Filesystem state when SQLite is imported/canonical can be ambiguous if storage verification reports stale export or conflict.
- Completion archive files can be ambiguous if archive movement completed but synthesis or context update failed.

## 13. Override Requirements

### 13.1 Observed Override Surfaces

**Evidence**

- Roadmap CLI:
  - `unblock`
  - `--elevated REASON`
  - `--execution-elevated REASON`
  - `--domain DOMAIN`
  - `--force`
  - `--force-import`
  - `--force-export`
  - `--full-roundtrip`
- Environment:
  - `LoopRelay_DECISION_RESUME=0|false`
  - `LoopRelay_SESSION_LOG=0|false`
- Cancellation:
  - CLIs return `130` for cancellation paths.
- Storage:
  - force flags alter import/export behavior for stale/divergent states.

### 13.2 Override Requirements

**Inference**

- Override discovery must identify whether an override exists for the current block.
- Override handling must preserve the explicit reason for elevated execution when supplied.
- Force import/export must be treated as storage-domain override only, not as generic workflow approval.
- Disabling decision resume affects only resume-on-open behavior and must not be treated as clearing persisted runtime state.
- No Plan or main execution override flags were observed; discovery must not invent them.

**Uncertainty**

- Roadmap `unblock` supports specific recovery categories, not a general unblock for every failed or blocked condition.

## 14. Recovery Requirements

### 14.1 Observed Recovery Surfaces

**Evidence**

- Roadmap startup planner can resume active workflows from persisted state.
- Roadmap resume planner can recover from `Cancelled` using transition intent or last transition state.
- Roadmap `unblock` supports recovery categories including:
  - `RecoverToCoreReady`
  - `RecoverExecutionDisposition`
  - `RecoverCompletionCertification`
  - `RecoverExecutionRuntimeFailure`
- Roadmap failures can persist blocker evidence and failure journal entries.
- Decision session can resume from SQLite when enabled and projection freshness allows it.
- Main execution rotates handoffs/decisions/deltas, allowing later runs to infer continuation.
- Storage verification can identify corrupt, stale, conflicting, orphaned, duplicate, or unresolved runtime state.
- Workflow transaction markers can classify retryable partial transactions, corrupt markers, and completed/failed phases.

### 14.2 Recovery Requirements

**Inference**

- Recovery discovery must identify the last safe evidence point before suggesting a stage is recoverable.
- Recovery discovery must distinguish retryable partial state from corrupt or conflicting state.
- Recovery discovery must preserve transition correlation identifiers, input hashes, output paths, and blocker evidence when present.
- Recovery discovery must treat Roadmap `EvidenceBlocked` as a durable blocked state only when state and blocker evidence agree.
- Recovery discovery must treat Plan partial artifacts as unresolved unless a known downstream contract can prove usability.
- Recovery discovery must treat main execution resume as implicit/artifact-driven, with no explicit persisted execution state.

**Uncertainty**

- Completion archive movement is known to be non-transactional in current issue records.
- Rerunning main CLI after archive movement can miss the original milestone short-circuit and fall into execution behavior.
- Completed epic archive index calculation can collide after non-contiguous archive directories.

## 15. User Interaction Requirements

### 15.1 Observed User Interaction

**Evidence**

- Current CLIs are command-line oriented.
- Plan CLI and main execution CLI accept only a repository path.
- Roadmap CLI exposes status/run/unblock/storage commands and several flags.
- Permission policy settings include safe commands, safe tools, review-required commands, deny rules, and allow rules.
- HITL requests can be captured from planning, roadmap, and completion prompt flows.
- Scoped artifact operations restrict allowed reads/writes and verify required outputs.
- Elevation reason is required for Roadmap elevated execution flags.

### 15.2 Interaction Requirements

**Inference**

- User-facing workflow discovery must explain why a workflow is blocked using concrete evidence paths, not only a generic status.
- User-facing workflow discovery must distinguish:
  - user action required;
  - storage conflict;
  - stale projection;
  - missing input;
  - invalid artifact;
  - completion policy block;
  - HITL request;
  - permission/elevation need.
- User-triggered overrides must be explicit and scoped.
- Discovery must not silently convert advisory roadmap content into authoritative user instruction.

**Uncertainty**

- Current code does not expose one user interaction model across all workflow surfaces.
- Existing issue records identify permission and scoped-artifact-operation trust gaps that affect how much authority user-facing prompts should claim.

## 16. CLI Surface Inventory

### 16.1 Plan CLI

**Evidence**

```text
LoopRelay.Plan.Cli <REPO_DIR>
```

Observed behavior:

- Requires exactly one repository path.
- Loads CLI settings and permission policy.
- Runs the fixed Plan pipeline.
- Returns:
  - `0` completed;
  - `4` preflight blocked;
  - `130` cancelled;
  - `1` failed.

No observed Plan flags:

- no `status`;
- no `resume`;
- no `force`;
- no stage selection;
- no storage command.

### 16.2 Main Execution CLI

**Evidence**

```text
LoopRelay.Cli <REPO_DIR>
```

Observed behavior:

- Requires exactly one repository path.
- Loads CLI settings and permission policy.
- Runs main execution loop and completion path.
- Returns:
  - `0` epic completed;
  - `4` completion blocked;
  - `130` cancelled;
  - `3` stalled;
  - `1` failed.

No observed main execution flags:

- no `status`;
- no `resume`;
- no `force`;
- no explicit completion-only mode;
- no storage command.

### 16.3 Roadmap CLI

**Evidence**

```text
LoopRelay.Roadmap.Cli [status|run|unblock|storage-init|storage-import|storage-export|storage-sync|storage-verify] <REPO_DIR> [--elevated REASON] [--domain DOMAIN] [--force|--force-import|--force-export] [--full-roundtrip]
```

Observed commands:

- `status`
- `run`
- `unblock`
- `storage-init`
- `storage-import`
- `storage-export`
- `storage-sync`
- `storage-verify`

Observed flags:

- `--elevated REASON`
- `--execution-elevated REASON`
- `--domain DOMAIN`
- `--force`
- `--force-import`
- `--force-export`
- `--full-roundtrip`

Observed outcomes:

- completed or paused: `0`;
- preflight blocked: `4`;
- cancelled: `130`;
- failed/default: `1`.

### 16.4 Missing Unified Surface

**Uncertainty**

- No observed CLI command answers all of:
  - which workflow should run next;
  - whether Plan is complete;
  - whether execution is in progress;
  - whether roadmap state is stale;
  - whether completion certification has closed the epic;
  - whether storage is canonical, stale, or conflicting.

## 17. Ambiguity Inventory

**Uncertainty**

1. "Traditional Roadmap" and "Eval-Driven Roadmap" are semantic labels, not current command names or persisted workflow identities.
2. `CreateNewRoadmap` prompt exists, but no current Roadmap CLI registry entry or command path was observed for it.
3. README describes SQLite as canonical for structured runtime state, but Roadmap CLI falls back to filesystem stores unless SQLite is valid imported/canonical.
4. Plan workflow has no durable state document, status command, or resume command.
5. Main execution workflow has no durable state document or status command.
6. Existing Plan outputs can indicate either completed planning, partial planning, or preflight-blocking leftovers.
7. Existing execution artifacts can indicate continuation, failed partial work, or stale history depending on milestone/Git/review/certification state.
8. Roadmap legacy execution-preparation states exist but are not advanced by current Roadmap CLI.
9. Completion behavior appears both in main execution completion service and Roadmap completion transition.
10. Completion archive movement is not fully transactional according to existing issue records.
11. Rerunning after completion archive movement can be non-idempotent according to existing issue records.
12. Archive index selection can collide when archive directories are non-contiguous according to existing issue records.
13. Permission-policy parsing and scoped artifact operation certification have known unresolved trust issues in issue records.
14. Roadmap `unblock` does not cover every possible blocker or failure condition.
15. Storage sync authority can depend on markers, hashes, domain selection, and force flags; simple file existence is insufficient.

## 18. Authority Model

### 18.1 Roadmap Authority

**Evidence**

- Roadmap persisted state is the main startup source.
- Projection contracts define required inputs, outputs, parsers, and stale policy.
- Projection freshness checks prompt identity, source hash, project context hash, and causal inputs.
- Project Context requires exactly nine canonical source files.
- Prompt text states roadmap source is advisory, while projection desired state and roadmap completion context carry current-state authority.
- Epic preparation audit treats selected epic as a proposal and repository codebase as implementation reality.

**Inference**

- Roadmap authority is layered:
  1. storage validity and selected runtime store;
  2. persisted Roadmap state;
  3. transition journal and blocker evidence;
  4. prompt contract and projection freshness;
  5. lifecycle and artifact validation;
  6. advisory roadmap source content.

### 18.2 Plan Authority

**Evidence**

- Plan preflight enforces clean outputs and required epic input.
- Plan prompt writes `.agents/plan.md` and must produce nonempty plan output.
- Adversarial review is read-only and revises plan through the held-open session.
- Artifact operations verify required outputs and changes.

**Inference**

- Plan authority is pipeline-local and artifact-verification based.
- Existing artifacts have authority only after satisfying downstream expectations; otherwise they can be blockers.

### 18.3 Execution Authority

**Evidence**

- Main execution filters `.agents` from real Git progress.
- Milestone checkboxes are the epic-complete gate but not final closure authority.
- Completion certification policy validates status, drift, recommendation, and route.
- Decision session can persist/resume but clears resume state on close after failed transfer.

**Inference**

- Execution authority is layered:
  1. real non-`.agents` Git changes and milestone checkbox progress;
  2. current handoff/decision/operational context artifacts;
  3. non-implementation review;
  4. completion certification policy and route;
  5. archive and roadmap context update evidence.

### 18.4 Persistence Authority

**Evidence**

- SQLite schema contains workflow, journal, lifecycle, projection, evidence, archive, and telemetry tables.
- Roadmap CLI uses SQLite-backed stores only for valid imported/canonical DB.
- Storage verification detects stale exports, conflicts, corrupt runtime files, unsupported schema, and unresolved references.

**Inference**

- Persistence authority depends on database status, sync markers, content hashes, and export freshness.
- Filesystem compatibility artifacts must be interpreted relative to SQLite authority when SQLite is active.

**Uncertainty**

- A single global persistence precedence rule across all workflows is not currently encoded.

## 19. Invariants

**Evidence and Inference**

The following invariants are required by observed behavior:

1. Repository path resolution for persistence must not escape the repository.
2. `.agents` remains the primary workspace artifact root.
3. `.agents` submodule changes do not count as real main execution progress.
4. Main execution progress requires either real non-`.agents` Git changes or reduced unticked milestones.
5. Milestone completion requires at least one strict markdown checkbox across `.agents/milestones/m*.md`.
6. Milestone checkboxes inside fenced code blocks do not count.
7. Epic closure requires completion certification policy/route, not milestone gate alone.
8. Plan workflow fresh preflight requires no existing plan, details, operational context, or milestone outputs.
9. Plan workflow requires `.agents/specs/epic.md` as mandatory input.
10. Roadmap Project Context requires exactly the canonical numbered context files.
11. Roadmap projections require manifest-backed freshness and validation.
12. Every registered Roadmap projection must have a prompt contract.
13. Stale Roadmap projections under stale policy `Block` are blockers.
14. Active epic promotion requires validation; prompt output alone is insufficient.
15. Roadmap `MilestoneSpecsReady` is a terminal pause, not execution completion.
16. Roadmap source files are required for selection but advisory in prompt semantics.
17. Selection is a proposal until audited, promoted, or otherwise routed.
18. Repository codebase is authoritative for implementation reality during epic preparation audit.
19. SQLite decision resume is canonical over legacy JSON resume state after migration.
20. `LoopRelay_DECISION_RESUME=0|false` disables resume attempts but does not remove all decision-session persistence concerns.
21. Storage force flags are scoped to import/export conflict handling.
22. `storage-verify` is read-only and must not be treated as a repair.
23. Workflow transaction markers can indicate retryable partial, completed, failed, or corrupt phases.
24. Completed epic archive state must be checked before assuming rerun safety.
25. HITL requests are workflow evidence and cannot be ignored during discovery.

## 20. Architectural Constraints

This section captures constraints on any future orchestrator's observable behavior. It does not prescribe architecture or implementation shape.

**Evidence and Inference**

1. Workflow discovery must be non-mutating unless the user explicitly invokes an existing mutating command.
2. Discovery must report evidence paths and authority level for each conclusion.
3. Discovery must support multiple simultaneous relevant workflow signals.
4. Discovery must preserve ambiguity when current evidence is insufficient.
5. Discovery must not collapse Roadmap, Plan, Execution, Completion, and Storage into one status without retaining their individual states.
6. Discovery must not invent workflow commands, flags, or overrides that are not present.
7. Workflow selection must respect current CLI boundaries and preflight rules.
8. Stage completion must respect each workflow's own authority model.
9. Storage authority must be resolved before trusting SQLite-backed or filesystem-backed state.
10. Projection freshness and prompt contract validation must remain part of Roadmap stage readiness.
11. Git working-tree status must filter `.agents` when evaluating main execution implementation progress.
12. Completion certification must remain separate from raw milestone checkbox completion.
13. Prompt outputs must remain subject to parser, validation, lifecycle, and promotion outcomes.
14. Recovery decisions must account for transition journals, blocker evidence, workflow transaction markers, and known archive/idempotency issues.
15. User overrides must be explicit, scoped, and traceable to existing override surfaces.
16. Any user-facing workflow status must distinguish evidence, inference, and uncertainty.
17. Known issue records must qualify confidence where they describe unresolved behavior.
18. Legacy compatibility files must be handled as migration or export sources, not automatically as canonical state.
19. Environment flags affecting telemetry or decision resume must be included in runtime discovery.
20. Cancellation must be reported as cancellation, not generic failure.

## 21. Unknowns

**Uncertainty**

1. Whether "Traditional Roadmap" and "Eval-Driven Roadmap" should become explicit workflow identities or remain inferred labels is not defined in current code.
2. The owner and invocation path for `CreateNewRoadmap` is unknown.
3. A canonical cross-workflow precedence rule between SQLite state and filesystem exports is not centralized.
4. Plan partial-output recovery semantics are unknown.
5. Main execution partial-state recovery semantics are implicit and artifact-driven, with no explicit status command.
6. The exact handoff from Roadmap `MilestoneSpecsReady` to Plan workflow is not encoded as a single command.
7. The exact handoff from Plan completion to main execution is not encoded as a single command.
8. Completion certification appears in both main execution and Roadmap contexts; the single owner for orchestration-level completion status is not explicit.
9. Archive retry semantics are unresolved where archive movement succeeds but synthesis or context update fails.
10. Rerun behavior after completion archive movement is unresolved.
11. Archive index allocation after deleted or skipped archive directories is unresolved.
12. Scope and trust of scoped artifact operations are unresolved where live protocol certification is absent.
13. Permission policy edge cases involving redirection or force-push-style commands remain unresolved in issue records.
14. Whether storage verification should be mandatory before any future workflow decision is not specified.
15. Whether telemetry absence should affect workflow discovery is not specified.
16. Whether HITL requests should block every downstream workflow, or only the originating workflow, is not specified globally.
17. Whether Roadmap `unblock` should cover all `EvidenceBlocked` states is not specified.
18. Whether Plan adversarial review outputs should be part of a durable cross-workflow contract is not specified.
19. Whether completion review delete decisions should be separately discoverable as a stage is not specified.
20. Whether a workflow orchestrator may run storage sync automatically is not specified by current CLI contracts.

