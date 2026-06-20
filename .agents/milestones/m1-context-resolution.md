# Milestone M1 - Execution Context Resolution

## Goal

Generate deterministic execution context packages for selected milestones.

## Backend Work

- [ ] Implement `ExecutionContextService`.
- [ ] Add `ExecutionContext`, `ExecutionContextArtifact`, `ExecutionContextDiagnostics`, and `ExecutionRepositorySnapshot`.
- [ ] Add `GitService.GetSnapshotAsync` for branch and status summary.
- [ ] Add `ExecutionContextSizePolicy` with warning and hard-limit thresholds.
- [ ] Validate repository availability, planning readiness, plan presence, and selected milestone presence.
- [ ] Include optional current handoff and current decisions when available.
- [ ] Return included artifacts, missing optional artifacts, size metrics, warning or hard-limit status, generated timestamp, branch, and dirty working-tree diagnostics.
- [ ] Add context preview endpoints.

## UI Work

- [ ] Add milestone selector sourced from existing workspace milestone inventory.
- [ ] Add `Build Execution Context` action.
- [ ] Display included artifacts, missing optional artifacts, repository snapshot, context size, generated timestamp, and validation results.
- [ ] Display context size warnings and hard-limit failures.
- [ ] Display pre-execution dirty-state diagnostics when local changes already exist.
- [ ] Keep launch unavailable until M2.

## Tests

- [ ] Context builds with plan and milestone.
- [ ] Missing handoff succeeds.
- [ ] Missing decisions succeeds.
- [ ] Missing plan fails.
- [ ] Missing selected milestone fails.
- [ ] Non-milestone path fails.
- [ ] Context warning threshold produces warning diagnostics without hard failure.
- [ ] Context hard limit produces launch-blocking diagnostics.
- [ ] Dirty repository state is captured and returned without blocking context preview.
- [ ] Repository snapshot captures branch and changed file counts with fake process runner.
- [ ] Context endpoint returns expected diagnostics.

## Exit Criteria

- [ ] A ready repository can produce a valid context package for a selected milestone.
- [ ] Invalid repositories or missing required artifacts produce clear validation errors.
- [ ] Oversized contexts are diagnosed, and hard-limit excess blocks future launch.
- [ ] Dirty repository state is visible and captured.
- [ ] User can inspect context before execution exists.
