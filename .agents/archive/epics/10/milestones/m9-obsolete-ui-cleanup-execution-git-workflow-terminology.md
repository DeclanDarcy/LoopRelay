# Milestone 9 Obsolete UI Cleanup: Execution Git Terminology

## Scope

- Audited remaining `RepositoryExecutionState` consumers around execution, workspace rails, navigation, and status badges.
- Confirmed the removed `executionWorkflow.ts` helper remains absent and guarded by `workflowAuthority.test.ts`.
- Kept `RepositoryExecutionState` where it displays execution-owned session, handoff, commit, and push state.
- Renamed visible commit/push surface terminology from `Git Workflow` to `Git Evidence` so it no longer reads as a competing workflow authority.
- Kept the existing `git-workflow` DOM/navigation anchor stable for deep-link compatibility.
- Strengthened selected-repository dashboard coverage to prove workflow stage/gate stay `Not loaded` when only execution state is available.
- Updated operational-context smoke-test navigation selectors to use implemented continuity cross-link controls instead of brittle discovery button assumptions.

## Verification

- `npm test -- selectedRepositorySummary.test.tsx app.smoke.test.tsx navigation.test.ts workflowAuthority.test.ts`

## Residual Risk

- The execution-owned commit/push anchor remains named `git-workflow` for compatibility. It is intentionally retained as an anchor id, not as visible authority terminology.
