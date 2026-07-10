# Orchestration Behavioral Baseline

This inventory records the pre-unification orchestration surface that the canonical migration must preserve or intentionally retire.

## Public CLI Surfaces

### `LoopRelay.Roadmap.Cli`

- Owns roadmap state-machine execution, roadmap storage commands, status and unblock behavior, blocker reporting, projection validation, decision ledger writes, transition journal writes, split lineage, lifecycle state, selection provenance, roadmap completion-context updates, and completion certification integration.
- Runs automatic workspace storage verification before mutating roadmap orchestration.
- Supports storage operations for init, import, export, sync, and verify through the roadmap persistence services.
- Blocks on unsafe storage authority, stale exports, conflicts, corruption, unsupported schema, unresolved references, partial workflow transactions, and ambiguous recovery markers.
- Cancellation is surfaced as a cancelled run and should preserve recoverable persisted evidence when a transition already wrote state.

### `LoopRelay.Plan.Cli`

- Runs a fixed `PlanPipeline`.
- Requires clean planning outputs before fresh execution.
- Writes `.agents/plan.md`.
- Generates and uses adversarial review material, revises the plan, seeds `.agents/operational_context.md`, collects details, extracts `.agents/milestones/m*.md`, refines details, publishes `.agents`, and records the parent gitlink after publication.
- Current partial-output semantics are preflight-blocking rather than durable workflow-state semantics.

### `LoopRelay.Cli`

- Requires a positional repository path in the current implementation.
- Runs the implementation loop through `LoopRunner`.
- Uses `MilestoneGate`, `DecisionSession`, `ExecutionStep`, `CommitGate`, non-implementation review, completion review, completion certification, `.agents` publication, and stall detection.
- Exit codes currently map cancellation to `130`, stall to `3`, completion-certification block to `4`, success to `0`, usage errors to `2`, and generic failure to `1`.
- Treats milestone checkbox progress inside `.agents/milestones/m*.md` as substantive progress even when parent repository files do not change.

## Repository-Owned State

- Live agent artifacts are under `.agents`.
- Shared SQLite state is under `.LoopRelay/persistence/looprelay.sqlite3`.
- Roadmap import/export and sync state can span SQLite and filesystem exports.
- Decision-session resume state is canonical in SQLite when the database is usable; legacy file-backed state can conflict with canonical state.
- Completion archives must remain discoverable after live planning and milestone artifacts are archived.

## Publication And Git Effects

- `.agents` publication is a first-class side effect in Plan and Execute behavior.
- Parent repository gitlink recording is part of Plan publication behavior.
- Execute commit evaluation distinguishes real repository changes from bookkeeping and from milestone checkbox progress.
- Git and publication failures must remain observable and must not be silently treated as workflow completion.

## Behavior To Preserve During Migration

- Prompt success is not workflow completion.
- Existing roadmap rigor around projection freshness, prompt contract snapshots, input snapshots, selection provenance, promotion validation, lifecycle state, decision ledger, split lineage, blocker evidence, and recovery intent remains required.
- Existing Plan warm-session and scoped-operation behavior remains required until replaced by equivalent canonical runtime postures.
- Existing Execute decision-session reuse/transfer, implementation slices, handoff generation, operational-context updates, publication, commit evaluation, stall behavior, non-implementation review, and completion certification remain required until replaced by equivalent canonical transitions.
- Storage verification remains read-only and non-repairing.

## Executable Coverage Map

- Roadmap behavior is covered by `LoopRelay.Roadmap.Cli.Tests` state-machine, transition-state, artifact-management, projection, persistence, split-lineage, lifecycle, decision-ledger, status, unblock, cancellation, and failure-persistence tests, plus canonical TraditionalRoadmap runtime tests in `LoopRelay.Cli.Tests`.
- Plan behavior is covered by `LoopRelay.Plan.Cli.Tests` pipeline, plan session, adversarial review, scoped artifact, publication, and preflight tests, plus canonical Plan runtime tests in `LoopRelay.Cli.Tests`.
- Execute behavior is covered by `LoopRelay.Cli.Tests` decision-session, execution-step, loop-runner, commit-gate, loop-artifact, publication, non-implementation review, completion, cancellation, stall, and canonical Execute runtime tests.
- Completion behavior is covered by `LoopRelay.Completion.Tests` certification, policy, routing, archive materialization, archive recovery, roadmap context update, blocked, and failed outcome tests, plus canonical Execute closure tests.
- Storage behavior is covered by roadmap persistence tests and orchestration primitive storage-observer/resolver tests for missing, file-backed, imported SQLite, canonical SQLite, stale export, conflict, corrupt database, unsupported schema, partial transaction, read-only verification, and ambiguous authority cases.
- Known-risk behavior is tagged with `Trait("Baseline", "KnownRisk")` where tests intentionally pin accepted risk behavior instead of ordinary success-path regression behavior.
