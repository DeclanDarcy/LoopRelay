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

## Acceptance

- [ ] `dotnet test LoopRelay.slnx` passes.
- [ ] No production orchestration behavior changes.
- [ ] Every behavior that later migration may affect has either executable coverage or explicit inventory.
