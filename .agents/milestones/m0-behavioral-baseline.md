# Milestone 0: Behavioral Baseline and Freeze

Objective: lock down current observable behavior before architectural migration.

Production behavior change allowed: none.

## Work

- [ ] Create a baseline inventory under `docs/orchestration-baseline.md` covering current CLI commands, flags, exit codes, cancellation messages, storage commands, status/unblock behavior, publication, Git interactions, and `.agents` effects.
- [ ] Add or expand characterization tests without changing production behavior:
  - [ ] Roadmap: startup, resume, status, unblock, blocker reporting, selection, audit, create, split, realign, reimagine, milestone-spec pause, stale projections, invariant failures, cancellation, SQLite and file-backed stores.
  - [ ] Plan: clean preflight, write plan, adversarial review, revision, operational context seed, collect details, extract milestones, extract details, publication, cancellation, failure, existing-output preflight blocks.
  - [ ] Execute: first run, pending decisions skip path, handoff-driven decision path, decision transfer/continuation, execution turn, handoff generation, `.agents` publication, commit evaluation, stall, completion review, completion certification, cancellation salvage.
  - [ ] Completion: every certification route, policy validation, archive materialization, archive recovery, roadmap context update requirement, blocked and failed outcomes.
  - [ ] Storage: missing DB, valid empty DB, imported DB, canonical DB, stale export, conflict, corrupt DB, unsupported schema, partial transaction, read-only verification.
- [ ] Add a known-risk inventory under `docs/orchestration-known-risks.md` for partial archive materialization, reruns after live artifact archival, archive index collision, partially completed roadmap context updates, permission bypass risks, stale exports, and interrupted workflow transactions.
- [ ] Tag tests that document known defects so later failures distinguish accepted current behavior from refactor regressions.

## Detail Requirements

### Baseline Deliverable Shape

The baseline certification package must contain:

- behavioral contract inventory
- executable characterization coverage
- behavioral invariant inventory
- pre-unification state migration inventory
- known-defect inventory
- migration risk inventory
- baseline certification summary

### Behavioral Contract Inventory

The inventory should capture observable behavior for:

- CLI arguments, subcommands, flags, defaults, exit codes, console behavior, logging, and cancellation behavior
- Traditional Roadmap, Plan, Execution, Storage, Completion, and Decision Session workflows
- filesystem persistence, SQLite persistence, fallback selection, migration, export, import, sync, and verification
- resume, restart, rerun, repair, failure, cancellation, blockers, and recovery
- approval, review, decision files, and other human interaction paths

### Characterization Test Philosophy

Tests should verify observable behavior, not implementation structure. Avoid tests that lock in incidental class names, method boundaries, or internal sequencing unless those details are externally observable.

### Behavioral Invariants

Invariants must be categorized, observable, and testable. Useful categories include:

- workflow invariants, such as roadmap pausing at milestone specs, Plan requiring clean outputs, and execution requiring certified completion
- persistence invariants, such as storage authority, lifecycle guarantees, and migration guarantees
- execution invariants, such as `.agents` being excluded from progress, completion certification being required, and milestone semantics
- recovery invariants, such as blockers, evidence, and cancellation being preserved
- CLI invariants, such as exit codes, command semantics, and cancellation behavior

### Pre-Unification State Inventory Classification

Every pre-unification behavior should be classified as one of:

- required
- optional
- deprecated
- historical only

The inventory should cover filesystem migration, SQLite migration, pre-unification state readability, pre-unification exports, journals, lifecycle rows, execution states, roadmap state, and decision resume.

### Known-Defect Inventory Fields

Each known issue should record:

- current behavior
- expected behavior
- whether later milestones must preserve the behavior
- whether later milestones may eliminate the behavior

Likely issue areas include archive behavior, idempotency, permissions, storage, retry, completion, execution state, transition journals, SQLite, and Git.

### Migration Risk Inventory Fields

Each migration risk should include:

- risk
- reason
- observable contract
- regression consequence

Risk categories should include workflow resolution, stage resolution, persistence, recovery, completion, storage, prompt execution, decision sessions, publication, Git, and human interaction.

### Baseline Certification Questions

M0 should answer:

- Can future milestones determine whether a behavior changed?
- Can they distinguish intentional improvement from accidental regression?
- Can known defects be separated from new regressions?
- Can the future architecture be validated against observable behavior instead of implementation?

## Acceptance

- [ ] `dotnet test LoopRelay.slnx` passes.
- [ ] No production orchestration behavior changes.
- [ ] Every behavior that later migration may affect has either executable coverage or explicit inventory.
