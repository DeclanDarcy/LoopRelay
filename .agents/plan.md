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

(See ./milestones/m0-behavioral-baseline.md)

## Milestone 1: Canonical Contracts and Vocabulary

(See ./milestones/m1-canonical-contracts.md)

## Milestone 2: Canonical Transition Runtime

(See ./milestones/m2-transition-runtime.md)

## Milestone 3: Workflow and Stage Resolution

(See ./milestones/m3-workflow-resolution.md)

## Milestone 4: Workflow Chaining and Unified Gates

(See ./milestones/m4-workflow-chaining.md)

## Milestone 5: TraditionalRoadmap Migration

(See ./milestones/m5-traditional-roadmap.md)

## Milestone 6: Plan Migration

(See ./milestones/m6-plan-migration.md)

## Milestone 7: Execute Migration

(See ./milestones/m7-execute-migration.md)

## Milestone 8: EvalRoadmap Implementation

(See ./milestones/m8-eval-roadmap.md)

## Milestone 9: Unified CLI, Compatibility Retirement, and Certification

(See ./milestones/m9-unified-cli-certification.md)

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
