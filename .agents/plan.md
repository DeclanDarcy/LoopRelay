# Canonical Orchestration Architecture Implementation Plan

## Purpose

Implement a single orchestration architecture for LoopRelay that supports these workflow chains:

- `TraditionalRoadmap -> Plan -> Execute`
- `EvalRoadmap -> Plan -> Execute`

The final system has one public CLI, four explicit workflow identities, one repository resolver, one transition runtime, one workflow controller, uniform product gates between transitions and workflows, and automatic non-repairing storage verification before mutating orchestration.

## Non-Goals

- Do not implement an execution-to-roadmap loop.
- Do not implement concurrent transition execution.
- Do not implement telemetry-driven confidence scoring or confidence-based routing.
- Do not automatically repair storage, overwrite conflicts, discard state, or silently import/export data during verification.
- Do not redesign prompt substance unless a prompt cannot satisfy the declared product contract.
- Do not remove compatibility handling until the new path is behaviorally certified.

## Current Codebase Baseline

The solution is `LoopRelay.slnx` and currently has separate orchestration implementations:

- `src/LoopRelay.Roadmap.Cli`: roadmap state machine, roadmap prompt transitions, roadmap storage commands, SQLite/file export handling, lifecycle, projections, decision ledger, split lineage, completion-context update, and roadmap completion certification integration.
- `src/LoopRelay.Plan.Cli`: a fixed `PlanPipeline` that writes `.agents/plan.md`, performs adversarial review, revises the plan, seeds `.agents/operational_context.md`, runs scoped artifact operations, publishes `.agents`, and records the parent gitlink at the end.
- `src/LoopRelay.Cli`: the iterative implementation loop through `LoopRunner`, `DecisionSession`, `ExecutionStep`, `MilestoneGate`, `CommitGate`, non-implementation review, completion review, completion certification, `.agents` publication, and stall detection.
- `src/LoopRelay.Orchestration.Primitives`: shared decision, sandbox, artifact path, and non-implementation review primitives. This is the right project for workflow contracts and runtime primitives.
- `src/LoopRelay.Core`: shared artifacts, logical artifacts, persistence helpers, repository model, and prompt assets.
- `src/LoopRelay.Completion`: completion evaluation, policy, routing, archive, recovery, and archive materialization.
- `src/LoopRelay.Projections`: projection definitions, manifests, validation, prompt catalog, and project context loading.
- `src/LoopRelay.Permissions`: command parsing and permission evaluation.
- `src/LoopRelay.Infrastructure`: file-backed artifact stores, git helpers, console rendering, and runtime diagnostics.

The current repository-owned runtime state is split across `.agents/*` exports and `.LoopRelay/persistence/looprelay.sqlite3`. `LoopRelayWorkspaceDatabase` owns the shared SQLite path and schema. Roadmap-specific SQLite import/export and verification logic currently lives under `src/LoopRelay.Roadmap.Cli/Services/Persistence`.

The implementation should prefer extracting or adapting existing behavior into shared services before rewriting it.

## Target Public CLI Contract

Use `src/LoopRelay.Cli` as the final public executable. The CLI resolves the repository from the current working directory by default and accepts `--repo <path>` for explicit repository selection. Legacy positional repository arguments remain accepted during compatibility.

Required chained invocations:

- `looprelay`
- `looprelay --eval`
- `looprelay --traditional`

Required bounded workflow invocations:

- `looprelay eval`
- `looprelay traditional`
- `looprelay plan`
- `looprelay execute`

Compatibility commands to preserve under the unified CLI:

- `looprelay status`
- `looprelay unblock`
- `looprelay storage init`
- `looprelay storage import`
- `looprelay storage export`
- `looprelay storage sync`
- `looprelay storage verify`

Exit-code mapping:

- `0`: completed, valid pause, or waiting state that preserves current successful pause semantics.
- `1`: failed or invalid output.
- `2`: command-line usage error.
- `3`: stalled execution.
- `4`: blocked, unsafe storage authority, ambiguous state, preflight block, or completion certification block.
- `130`: cancelled.

## Workflow Identities

Define these identities as first-class values:

- `TraditionalRoadmap`
- `EvalRoadmap`
- `Plan`
- `Execute`

Every workflow definition declares:

- identity
- purpose
- entry products
- entry gate
- stages
- transition dependency topology
- exit products
- exit gate
- downstream workflow, when chained
- completion conditions
- blocker semantics
- recovery semantics

## Product Model

Products are semantic outputs. Files, SQLite rows, Git commits, evidence, and exports are storage representations.

Canonical products:

| Product | Current representation candidates | Primary consumers |
|---|---|---|
| Evaluation Intent | `.agents/evals/*.md` | EvalRoadmap |
| Roadmap Completion Context | `RoadmapArtifactPaths.RoadmapCompletionContext` | Roadmap selection and update |
| Strategic Initiative Selection | `RoadmapArtifactPaths.Selection` plus provenance | TraditionalRoadmap |
| Dependency Inventory | evaluation artifact plus evidence | EvalRoadmap |
| Hypothesis Inventory | evaluation artifact plus evidence | EvalRoadmap |
| Architectural Catalog | evaluation artifact plus evidence | EvalRoadmap |
| Dependency Graph | evaluation artifact plus evidence | EvalRoadmap |
| Epic Roadmap | evaluation artifact plus evidence | EvalRoadmap |
| Prepared Epic | `RoadmapArtifactPaths.ActiveEpic` | Plan |
| Milestone Specification Set | `OrchestrationArtifactPaths.SpecsDirectory` validated set | Plan |
| Executable Plan | `OrchestrationArtifactPaths.Plan` | Execute |
| Operational Context | `OrchestrationArtifactPaths.OperationalContext` | Execute and decision sessions |
| Execution Details | `OrchestrationArtifactPaths.Details` | Execute |
| Execution Milestone Set | `OrchestrationArtifactPaths.MilestonesDirectory` validated `m*.md` set | Execute |
| Execution Readiness | workflow state/evidence proving Plan exit gate | Execute |
| Decision Set | `OrchestrationArtifactPaths.Decisions` plus history | Execute |
| Implementation Slice | execution prompt result plus repository delta | Execute |
| Execution Handoff | `OrchestrationArtifactPaths.LiveHandoff` plus history | Execute |
| Operational Delta | `OrchestrationArtifactPaths.OperationalDelta` plus history | Execute |
| Repository Changes | Git commit/push evidence and working-tree delta | Execute |
| Completion Evidence | completion review and certification evidence | Execute |
| Certified Completion | completion result, archive record, closed workflow state | chain terminator |

Each product record must include:

- product identity
- producer workflow and transition
- intended consumers
- repository ownership
- authority
- storage representations
- content hash or causal identity
- freshness
- validation state
- lifecycle
- evidence locations

## Storage Authority Rules

Storage verification runs before every mutating orchestration invocation. Verification is read-only. It may block execution and explain authority, conflicts, stale exports, corruption, unsupported schema, unresolved references, partial workflow transactions, and ambiguity. It must never silently import, export, sync, repair, overwrite, or discard state.

Implementation direction:

- Extract shared database path/schema ownership from `LoopRelay.Core.Services.Persistence.LoopRelayWorkspaceDatabase`.
- Move generic verification result types and authority vocabulary into `LoopRelay.Orchestration.Primitives`.
- Keep roadmap export/import details behind adapters until the storage domains are generalized.
- Add schema migration support before introducing canonical workflow tables. Existing exact-version validation must become explicit migration or explicit unsupported-schema reporting.

Canonical workflow persistence needs durable rows for:

- workflow state
- stage state
- transition runs
- transition evidence
- product records
- gate evaluations
- effect records
- blockers
- recovery markers
- workflow-chain runs

The current `workflow_transactions` table remains useful for partial persistence markers, but it is not enough to represent workflow progress.

## Milestone 0: Behavioral Baseline and Freeze

Objective: lock down current observable behavior before architectural migration.

Production behavior change allowed: none.

Work:

1. Create a baseline inventory under `docs/orchestration-baseline.md` covering current CLI commands, flags, exit codes, cancellation messages, storage commands, status/unblock behavior, publication, Git interactions, and `.agents` effects.
2. Add or expand characterization tests without changing production behavior:
   - Roadmap: startup, resume, status, unblock, blocker reporting, selection, audit, create, split, realign, reimagine, milestone-spec pause, stale projections, invariant failures, cancellation, SQLite and file-backed stores.
   - Plan: clean preflight, write plan, adversarial review, revision, operational context seed, collect details, extract milestones, extract details, publication, cancellation, failure, existing-output preflight blocks.
   - Execute: first run, pending decisions skip path, handoff-driven decision path, decision transfer/continuation, execution turn, handoff generation, `.agents` publication, commit evaluation, stall, completion review, completion certification, cancellation salvage.
   - Completion: every certification route, policy validation, archive materialization, archive recovery, roadmap context update requirement, blocked and failed outcomes.
   - Storage: missing DB, valid empty DB, imported DB, canonical DB, stale export, conflict, corrupt DB, unsupported schema, partial transaction, read-only verification.
3. Add a known-risk inventory under `docs/orchestration-known-risks.md` for partial archive materialization, reruns after live artifact archival, archive index collision, partially completed roadmap context updates, permission bypass risks, stale exports, and interrupted workflow transactions.
4. Tag tests that document known defects so later failures distinguish accepted current behavior from refactor regressions.

Acceptance:

- `dotnet test LoopRelay.slnx` passes.
- No production orchestration behavior changes.
- Every behavior that later migration may affect has either executable coverage or explicit inventory.

## Milestone 1: Canonical Contracts and Vocabulary

Objective: add implementation-neutral contracts that can describe every workflow without changing execution.

Production behavior change allowed: none.

Work:

1. Add contract models under `src/LoopRelay.Orchestration.Primitives/Workflows`:
   - `WorkflowIdentity`
   - `WorkflowChainDefinition`
   - `WorkflowDefinition`
   - `WorkflowStageDefinition`
   - `WorkflowTransitionDefinition`
   - `TransitionDependency`
   - `ProductDefinition`
   - `ProductIdentity`
   - `ProductRequirement`
   - `GateDefinition`
   - `GateResult`
   - `GateRequirementResult`
   - `WorkflowOutcome`
   - `StageOutcome`
   - `TransitionOutcome`
   - `ExecutionPosture`
   - `EffectDefinition`
   - `BlockerDefinition`
   - `RecoveryDefinition`
2. Model gate outcomes as structured results: `Satisfied`, `Unsatisfied`, `Blocked`, `Waiting`, `Invalid`, and `Ambiguous`.
3. Model runtime outcomes as structured results: `Completed`, `Paused`, `Blocked`, `Failed`, `Cancelled`, `Waiting`, `Stalled`, and `Ambiguous`.
4. Model dependencies as required, optional, advisory, freshness-sensitive, or invalidating.
5. Add workflow definition validation that checks:
   - identity is explicit
   - stages reference known transitions
   - transition dependencies reference known products or transitions
   - products have producer/consumer metadata
   - entry and exit gates have explainable requirements
   - workflow definitions do not embed CLI or persistence implementation details
6. Add non-wired definition sketches for all four workflows to prove the contracts fit the domain. These sketches must not drive production execution yet.

Acceptance:

- New tests in `tests/LoopRelay.Orchestration.Primitives.Tests` validate contract invariants.
- All four workflow definitions can be represented through the same contract types.
- Existing CLI and workflow tests still pass.

## Milestone 2: Canonical Transition Runtime

Objective: implement one workflow-agnostic lifecycle for executing a prompt-driven transition.

Work:

1. Add runtime services under `src/LoopRelay.Orchestration.Primitives/Runtime`:
   - `TransitionRuntime`
   - `ITransitionDefinitionResolver`
   - `IProductResolver`
   - `IGateEvaluator`
   - `IPromptContextBuilder`
   - `IPromptRenderer`
   - `IPromptExecutor`
   - `IOutputInterpreter`
   - `IProductValidator`
   - `IEffectExecutor`
   - `ITransitionRunStore`
   - `ITransitionEvidenceStore`
2. Implement the lifecycle:
   - resolve transition definition
   - resolve required inputs
   - evaluate input gate
   - construct prompt context
   - render prompt
   - persist transition start
   - execute prompt
   - capture raw output
   - interpret output
   - validate declared outputs
   - apply effects
   - persist completion
   - resolve eligible successors
3. Add durable transition states:
   - not started
   - started
   - prompt completed
   - output interpreted
   - output validated
   - effects partially applied
   - effects applied
   - completed
   - blocked
   - failed
   - cancelled
4. Implement execution postures without workflow-specific runtime types:
   - one-shot agent prompt
   - persistent session
   - warm session
   - scoped artifact operation
   - decision session
   - read-only prompt
5. Extract reusable pieces from `RoadmapPromptTransitionRunner`:
   - input snapshot hashing
   - transition journal events
   - raw prompt output capture
   - failure persistence pattern
6. Keep output validation after prompt execution and before completion. A successful prompt response must not complete a transition unless required products validate.
7. Add effect execution with deterministic ordering and durable partial-failure evidence.
8. Add representative adapter coverage for one roadmap transition, preferably completion-context bootstrap, without making the Roadmap CLI use the runtime in production.

Acceptance:

- Transition runtime tests cover missing inputs, stale inputs, invalid inputs, malformed prompt output, missing output, invalid output, partial effect failure, cancellation, and persistence failure.
- The representative transition executes fully through the runtime in tests.
- No workflow migration has begun.

## Milestone 3: Workflow and Stage Resolution

Objective: determine repository state, selected workflow, current stage, eligible transitions, blockers, and ambiguity without mutating the repository.

Work:

1. Add repository observation under `src/LoopRelay.Orchestration.Primitives/Resolution`:
   - `RepositoryObservation`
   - `RepositoryObserver`
   - `StorageAuthoritySnapshot`
   - observed workflow states
   - observed products
   - observed lifecycle rows
   - observed evidence
   - observed transition runs
   - observed Git facts
   - observed human interaction requirements
2. Add `StorageVerificationResult` with authority, usable authority, stale exports, conflicts, corruption, unsupported schema, unresolved references, partial transactions, and blocking conditions.
3. Adapt current `WorkspaceVerificationService` into the resolution path. Keep all verification non-mutating.
4. Add invocation-mode resolution:
   - default chained mode
   - forced eval chain
   - forced traditional chain
   - bounded eval
   - bounded traditional
   - bounded plan
   - bounded execute
5. Implement default roadmap selection:
   - if one or more `.agents/evals/*.md` files exist, select `EvalRoadmap`
   - otherwise select `TraditionalRoadmap`
   Explicit flags override this rule.
6. Implement workflow state resolution for each identity:
   - absent
   - eligible to start
   - active
   - resumable
   - completed
   - blocked
   - waiting
   - cancelled
   - failed
   - invalid
   - ambiguous
7. Implement stage and transition eligibility from products, gate results, transition evidence, recovery state, and blockers. Artifact existence alone must never imply completion.
8. Add an explanation model that records selected workflow, selected stage, eligible transitions, satisfied gates, unsatisfied gates, blockers, evidence, authority, ignored evidence, conflicts, and uncertainty.

Acceptance:

- Resolution tests cover fresh, partial, blocked, cancelled, failed, completed, legacy, SQLite, filesystem, mixed, corrupt, and ambiguous repositories.
- Resolution is deterministic and non-mutating.
- Default eval/traditional selection is covered.

## Milestone 4: Workflow Chaining and Unified Gates

Objective: compose workflows through the same product/gate mechanism used by transitions.

Work:

1. Add chain definitions:
   - `TraditionalRoadmapChain`: `TraditionalRoadmap -> Plan -> Execute`
   - `EvalRoadmapChain`: `EvalRoadmap -> Plan -> Execute`
2. Add workflow boundary services:
   - `WorkflowEntryGateEvaluator`
   - `WorkflowExitGateEvaluator`
   - `ProductTransferEvaluator`
   - `WorkflowBoundaryEvidenceWriter`
3. Add `WorkflowController` and `WorkflowChainRunner`.
4. The controller owns workflow selection, stage selection, transition selection among eligible transitions, workflow completion checks, downstream eligibility, bounded stop conditions, and terminal outcome mapping.
5. The controller must not render prompts, execute prompts, validate products, apply effects, or write transition persistence directly. Those remain runtime responsibilities.
6. Add stopping conditions:
   - chain completed
   - bounded workflow completed
   - blocked
   - waiting
   - cancelled
   - failed
   - stalled
   - ambiguous
   - no eligible transition
7. Add explainability for why chaining occurred or stopped.
8. Use fake workflow definitions and compatibility adapters for tests. Do not migrate production workflows yet.

Acceptance:

- Chain progression tests prove workflow boundaries use validated products, not files.
- Bounded commands stop after one workflow.
- Default, forced eval, and forced traditional modes select the correct chain.
- No production workflow has been migrated yet.

## Milestone 5: TraditionalRoadmap Migration

Objective: migrate the existing roadmap workflow onto the canonical runtime and controller.

Work:

1. Add `TraditionalRoadmapWorkflowDefinition` under `src/LoopRelay.Roadmap.Cli/Services/Workflows`.
2. Define stages:
   - Roadmap Context
   - Strategic Initiative Selection
   - Epic Preparation
   - Milestone Specification
   - Workflow Completion
3. Define transitions:
   - Bootstrap Roadmap Completion Context
   - Update Roadmap Completion Context
   - Select Next Initiative
   - Audit Existing Epic
   - Create Epic
   - Split Epic
   - Realign Epic
   - Reimagine Epic
   - Retire Epic
   - Generate Milestone Deep Dives
   - Verify Workflow Exit Gate
4. Convert existing transition classes into runtime adapters:
   - prompt identity
   - product requirements
   - parser
   - output validator
   - effects
   - blocker and recovery metadata
5. Move orchestration responsibilities out of `RoadmapStateMachine`:
   - transition ordering
   - prompt execution sequencing
   - transition persistence sequencing
   - lifecycle advancement
   - next-transition decisions
6. Preserve current roadmap rigor:
   - projection freshness
   - prompt contract snapshots
   - input snapshots
   - selection provenance
   - artifact promotion validation
   - lifecycle state
   - decision ledger
   - split lineage
   - blocker evidence
   - recovery intent
7. Define the canonical downstream products:
   - `PreparedEpic`
   - `MilestoneSpecificationSet`
8. Treat legacy roadmap states related only to old execution handoff as compatibility states. Recognize and report them safely, but do not let them define active orchestration.
9. Keep `LoopRelay.Roadmap.Cli` as a compatibility adapter while `src/LoopRelay.Cli` becomes able to run `TraditionalRoadmap`.

Acceptance:

- TraditionalRoadmap runs through the canonical transition runtime.
- TraditionalRoadmap reaches a canonical workflow-complete state that satisfies Plan entry.
- Existing roadmap characterization tests pass.
- Legacy roadmap orchestration is no longer an active authority.

## Milestone 6: Plan Migration

Objective: migrate `PlanPipeline` into a first-class Plan workflow.

Work:

1. Add `PlanWorkflowDefinition` under `src/LoopRelay.Plan.Cli/Services/Workflows` or a shared workflow definitions location once dependencies allow it.
2. Define stages:
   - Planning
   - Plan Validation
   - Execution Preparation
   - Workflow Completion
3. Define transitions:
   - Write Executable Plan
   - Generate Adversarial Projection
   - Run Adversarial Review
   - Revise Plan
   - Generate Operational Context
   - Collect Details
   - Generate Execution Milestones
   - Refine Execution Details
   - Verify Execute Contract
4. Adapt current components:
   - `PlanSession` becomes a prompt executor using warm-session posture.
   - `ReviewStep` becomes a read-only prompt transition.
   - `PermissionedArtifactOperationStep` becomes scoped-operation posture.
   - `OneShotSteps` become transition definitions or transition-specific prompt context builders.
   - `AgentsSubmodulePublisher` and parent gitlink recording become ordered effects.
5. Add canonical Plan state that distinguishes:
   - not started
   - planning in progress
   - plan authored
   - validation in progress
   - validation complete
   - execution preparation in progress
   - partial execution products
   - execution-ready
   - blocked
   - cancelled
   - failed
   - completed
6. Define the canonical Execute entry product set:
   - `ExecutablePlan`
   - `OperationalContext`
   - `ExecutionDetails`
   - `ExecutionMilestoneSet`
   - `ExecutionReadiness`
7. Replace fresh-run preflight ambiguity with durable partial-state semantics. Existing outputs are products with producer evidence, validation state, and resume eligibility.
8. Keep `LoopRelay.Plan.Cli` as a compatibility adapter while `src/LoopRelay.Cli` becomes able to run `Plan`.

Acceptance:

- Plan runs through the canonical runtime.
- Plan can resume at the correct stage after interruption.
- Plan completion satisfies or fails Execute entry through the canonical gate.
- Current Plan pipeline tests pass or are intentionally updated to assert the new canonical behavior.

## Milestone 7: Execute Migration

Objective: migrate the implementation loop into a first-class iterative Execute workflow.

Work:

1. Add `ExecuteWorkflowDefinition` under `src/LoopRelay.Cli/Services/Workflows`.
2. Define stages:
   - Execution Readiness
   - Implementation Planning
   - Implementation
   - Execution Continuity
   - Completion
   - Workflow Completion
3. Define transitions:
   - Verify Execution Readiness
   - Generate Decision
   - Transfer Decision Session
   - Continue Decision Session
   - Execute Implementation Slice
   - Generate Handoff
   - Update Operational Context
   - Publish Repository State
   - Evaluate Commit
   - Evaluate Milestone Completion
   - Run Non-Implementation Review
   - Run Completion Certification
   - Interpret Completion Route
   - Verify Workflow Exit Gate
4. Model iteration explicitly:
   - readiness
   - planning
   - implementation
   - continuity
   - completion
   - continue to readiness or close
5. Adapt current components:
   - `MilestoneGate` becomes readiness/completion gate support.
   - `DecisionSession` becomes decision-session execution posture.
   - `ExecutionStep` becomes implementation-slice transition execution.
   - `LoopArtifacts` rotation methods become effects with evidence.
   - `AgentsSubmodulePublisher` becomes publish effect.
   - `CommitGate` becomes commit evaluation effect/gate support.
   - non-implementation post-execution review becomes a transition.
   - non-implementation completion review and completion certification become the canonical completion stage.
6. Establish a durable closed-state marker:
   - `CertifiedCompletion` product
   - completed Execute workflow state
   - archive record
   - completion evidence
   - product references that remain resolvable after live Plan/milestone artifacts are archived
7. Make completion authority singular. Compatibility callers may delegate to Execute completion, but no other active orchestration path may own completion closure.
8. Add recovery for interruption during:
   - completion review
   - completion evaluation
   - archive materialization
   - archive synthesis
   - roadmap completion-context update
   - final closed-state persistence
9. Preserve stall semantics with durable evidence.

Acceptance:

- Execute runs through the canonical runtime and controller.
- Execution stage resolves correctly after process restart.
- Already-closed execution is idempotently discoverable.
- Completion closure is singular and durable.
- Current execution tests pass or are intentionally updated to assert canonical state.

## Milestone 8: EvalRoadmap Implementation

Objective: implement EvalRoadmap as a first-class workflow that converges into the same Plan entry contract as TraditionalRoadmap.

Work:

1. Add `EvalRoadmapWorkflowDefinition`.
2. Add evaluation artifact path constants, preferably in a new `EvaluationArtifactPaths` class:
   - input directory `.agents/evals`
   - selected evaluation
   - dependency inventory
   - hypothesis inventory
   - architectural catalog
   - dependency graph
   - next epic roadmap
   - evaluation evidence directory
3. Add prompt assets under `src/LoopRelay.Core/Prompts/Evaluation`:
   - Interpret Next Evaluation
   - Generate Dependency Inventory
   - Generate Hypothesis Inventory
   - Generate Architectural Catalog
   - Generate Dependency DAG
   - Generate Next Epic Roadmap
   - Generate Prepared Epic
   - Generate Milestone Specifications, reusing existing milestone generation where possible
4. Define stages:
   - Evaluation Foundation
   - Dependency Analysis
   - Hypothesis Development
   - Architectural Organization
   - Roadmap Formation
   - Epic Preparation
   - Milestone Specification
   - Workflow Completion
5. Define transitions:
   - Interpret Next Evaluation
   - Generate Dependency Inventory
   - Generate Hypothesis Inventory
   - Generate Architectural Catalog
   - Generate Dependency DAG
   - Generate Next Epic Roadmap
   - Generate Prepared Epic
   - Generate Milestone Specifications
   - Verify Plan Entry Contract
6. Express dependencies declaratively. Execution remains serial, but the definition must support multiple eligible successors later.
7. Implement output gates for every evaluation knowledge product.
8. Ensure EvalRoadmap produces the exact same `PreparedEpic` and `MilestoneSpecificationSet` products as TraditionalRoadmap. Plan must not branch on which roadmap workflow produced them.
9. Add resolution support:
   - default invocation selects EvalRoadmap when `.agents/evals/*.md` exists
   - `--eval` forces EvalRoadmap chain
   - `looprelay eval` runs EvalRoadmap only

Acceptance:

- EvalRoadmap runs and resumes through the canonical runtime.
- EvalRoadmap product validation blocks downstream progression on missing or invalid outputs.
- Plan entry is identical for EvalRoadmap and TraditionalRoadmap.
- No Plan or Execute code branches on roadmap producer identity.

## Milestone 9: Unified CLI, Compatibility Retirement, and Certification

Objective: make the unified CLI the authoritative orchestration surface.

Work:

1. Replace `src/LoopRelay.Cli/Services/Cli/CliArguments.cs` with unified parsing that supports:
   - current-directory default repository
   - `--repo <path>`
   - `--eval`
   - `--traditional`
   - bounded workflow subcommands
   - status, unblock, and storage subcommands
   - legacy positional repository compatibility
2. Update `src/LoopRelay.Cli/Program.cs` to:
   - set UTF-8 output
   - parse unified invocation
   - run storage verification before mutating orchestration
   - create unified composition
   - execute `WorkflowChainRunner`
   - map canonical outcomes to exit codes
3. Add `UnifiedCliComposition` that wires:
   - repository observer
   - storage verifier
   - workflow resolver
   - transition runtime
   - workflow controller
   - workflow definitions
   - adapters for prompts, agents, permissions, projections, artifacts, completion, Git, and publication
4. Convert old CLI projects into thin compatibility surfaces:
   - `LoopRelay.Roadmap.Cli` translates legacy args and delegates to unified orchestration or storage compatibility.
   - `LoopRelay.Plan.Cli` delegates to bounded `Plan`.
   - Existing Execute invocation delegates to bounded `Execute`.
5. Update publish scripts:
   - `publish-cli.bat` publishes the unified executable.
   - `publish-plan-cli.bat` and `publish-roadmap-cli.bat` either publish compatibility adapters or are retired after a documented compatibility decision.
6. Add unified status output that explains:
   - invocation mode
   - selected chain
   - selected workflow
   - current stage
   - next eligible transition
   - satisfied gates
   - unsatisfied gates
   - blockers
   - storage authority
   - user action required, if any
7. Retire duplicate active authorities:
   - roadmap state-machine orchestration
   - plan pipeline sequencing
   - execution loop orchestration
   - duplicate completion ownership
   - CLI-to-CLI chaining
8. Keep compatibility readers for:
   - old roadmap state
   - partial Plan artifacts
   - old decision-session resume state
   - filesystem exports
   - imported/canonical SQLite states
   - legacy transition journals
   - legacy lifecycle rows
   - completion archives

Acceptance:

- Required invocations work:
  - `looprelay`
  - `looprelay --eval`
  - `looprelay --traditional`
  - `looprelay eval`
  - `looprelay traditional`
  - `looprelay plan`
  - `looprelay execute`
- Default invocation selects EvalRoadmap when `.agents/evals/*.md` exists and TraditionalRoadmap otherwise.
- Chained modes continue through Plan and Execute.
- Bounded commands stop after one workflow.
- Execute certified closure ends the chain.
- Automatic storage verification is present and non-repairing.
- Legacy entry points no longer own orchestration.
- `dotnet test LoopRelay.slnx` passes.

## Required Test Matrix

Add tests at the lowest layer that owns each behavior.

Transition runtime tests:

- missing required input blocks
- stale required input blocks when freshness is required
- prompt failure does not complete transition
- malformed output does not satisfy output gate
- missing output does not satisfy output gate
- invalid output remains evidence but not usable product
- partial effect failure is observable
- cancellation remains cancellation

Resolution tests:

- clean start resolves first stage
- completed transition resolves next stage
- partial transition resolves recovery or blocked state
- stale product does not advance
- completed workflow does not restart
- conflicting evidence reports ambiguity
- resolution is non-mutating

Workflow boundary tests:

- TraditionalRoadmap output satisfies Plan entry
- EvalRoadmap output satisfies the same Plan entry
- invalid roadmap output does not start Plan
- partial Plan does not start Execute
- completed Plan starts Execute
- Execute completion ends chain
- bounded commands do not auto-chain

Storage tests:

- missing database
- valid empty database
- imported database
- canonical database
- stale filesystem export
- database/export conflict
- corrupt database
- unsupported schema
- partial workflow transaction
- verification does not mutate filesystem or database

CLI tests:

| Invocation | Expected behavior |
|---|---|
| `looprelay` with eval files | EvalRoadmap -> Plan -> Execute |
| `looprelay` without eval files | TraditionalRoadmap -> Plan -> Execute |
| `looprelay --eval` | EvalRoadmap -> Plan -> Execute |
| `looprelay --traditional` | TraditionalRoadmap -> Plan -> Execute |
| `looprelay eval` | EvalRoadmap only |
| `looprelay traditional` | TraditionalRoadmap only |
| `looprelay plan` | Plan only |
| `looprelay execute` | Execute only |

Recovery tests:

- interruption before prompt
- interruption after prompt before output validation
- interruption after output validation before effects
- interruption during effects
- interruption after effects before transition completion persistence
- Plan partial-stage resume
- Execute interrupted handoff
- Execute interrupted publish
- completion interrupted during archive
- completion interrupted during context update
- already-closed invocation

## Implementation Order

Use this order:

1. Milestone 0
2. Milestone 1
3. Milestone 2
4. Milestone 3
5. Milestone 4
6. Milestone 5
7. Milestone 6
8. Milestone 7
9. Milestone 8
10. Milestone 9

EvalRoadmap prompt and product scaffolding may begin after Milestone 4 if it does not alter active orchestration. Full EvalRoadmap activation waits until the TraditionalRoadmap-to-Plan product contract is proven.

## Certification Criteria

The architecture is complete when:

- one public CLI is authoritative
- all four workflow identities are explicit
- both workflow chains are declared
- default EvalRoadmap selection follows `.agents/evals/*.md`
- `--eval` and `--traditional` force chained modes
- bounded workflow commands remain bounded
- storage verification is automatic and non-repairing
- every workflow uses the same workflow, stage, transition, gate, product, effect, blocker, recovery, and outcome contracts
- every transition runs through the canonical prompt-transition lifecycle
- prompt rendering is separated from persistence, validation, effects, and state advancement
- workflow boundaries use the same product/gate model as transition boundaries
- TraditionalRoadmap and EvalRoadmap satisfy the same Plan entry contract
- Plan satisfies one Execute entry contract
- Execute has durable stage and completion state
- certified closure is discoverable after archival
- workflow and stage resolution are explainable from repository-owned evidence
- partial progress, blockers, failures, cancellation, waiting, and ambiguity are distinguishable
- serial execution does not hard-code a linear-only topology
- duplicate orchestration and completion authorities are retired
- supported legacy repository states are migrated or interpreted safely
- the full behavioral, compatibility, failure, recovery, and CLI certification suite passes
