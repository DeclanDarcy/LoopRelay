# Milestone M1 - Execution Context Resolution

## Goal

Generate deterministic execution context packages for selected milestones.

## Backend Work

- [x] Implement `ExecutionContextService`.
- [x] Add `ExecutionContext`, `ExecutionContextArtifact`, `ExecutionContextDiagnostics`, and `ExecutionRepositorySnapshot`.
- [x] Add `GitService.GetSnapshotAsync` for branch and status summary.
- [x] Add `ExecutionContextSizePolicy` with warning and hard-limit thresholds.
- [x] Validate repository availability, planning readiness, plan presence, and selected milestone presence.
- [x] Include optional current handoff and current decisions when available.
- [x] Return included artifacts, missing optional artifacts, size metrics, warning or hard-limit status, generated timestamp, branch, and dirty working-tree diagnostics.
- [x] Add context preview endpoints.

## UI Work

- [x] Add milestone selector sourced from existing workspace milestone inventory.
- [x] Add `Build Execution Context` action.
- [x] Display included artifacts, missing optional artifacts, repository snapshot, context size, generated timestamp, and validation results.
- [x] Display context size warnings and hard-limit failures.
- [x] Display pre-execution dirty-state diagnostics when local changes already exist.
- [x] Keep launch unavailable until M2.

## Tests

- [x] Context builds with plan and milestone.
- [x] Missing handoff succeeds.
- [x] Missing decisions succeeds.
- [x] Missing plan fails.
- [x] Missing selected milestone fails.
- [x] Non-milestone path fails.
- [x] Context warning threshold produces warning diagnostics without hard failure.
- [x] Context hard limit produces launch-blocking diagnostics.
- [x] Dirty repository state is captured and returned without blocking context preview.
- [x] Repository snapshot captures branch and changed file counts with fake process runner.
- [x] Context endpoint returns expected diagnostics.

## Exit Criteria

- [x] A ready repository can produce a valid context package for a selected milestone.
- [x] Invalid repositories or missing required artifacts produce clear validation errors.
- [x] Oversized contexts are diagnosed, and hard-limit excess blocks future launch.
- [x] Dirty repository state is visible and captured.
- [x] User can inspect context before execution exists.
