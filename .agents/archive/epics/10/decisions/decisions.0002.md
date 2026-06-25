# Decisions

## Newly Authorized

- The Milestone 1 infrastructure slice is accepted as on track and does not need revision before proceeding.
- The workflow layering remains authoritative backend projection, backend endpoints, Rust transport bridge, TypeScript API/hooks, and React rendering.
- Rust workflow commands should remain shape-neutral transport using `serde_json::Value`; Rust must not become a semantic workflow model owner.
- `decisionSessionSummary` should remain required in frontend repository projection types because the backend guarantees a default summary.
- The next implementation slice should migrate only workflow consumers first, specifically `App.tsx`, `WorkspaceRail`, and `ExecutionWorkflowRail`.
- Consumer migration should replace `RepositoryExecutionState` plus `getExecutionWorkflowSteps()` with `useWorkflowProjection()` while preserving current layout and UX for behavior parity.
- Deleting `executionWorkflow.ts`, removing obsolete imports/helpers, and adding the no-derived-workflow regression test should happen only after all consumers are migrated.
- Avoid UI-owned workflow view models. If an adapter becomes necessary, it must stay limited to presentation concerns such as formatting, grouping, and display.
