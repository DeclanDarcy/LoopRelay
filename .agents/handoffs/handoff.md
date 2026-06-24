# Handoff

## New State This Slice

- Milestone 1 transport foundation is implemented and checked off in `.agents/milestones/m1-workflow-engine.md`.
- `src/CommandCenter.Shell/src/main.rs` now exposes every Core MVP workflow route listed in M1 through Tauri commands.
- The shell bridge uses shared `backend_get_value` and `backend_post_value` helpers returning `serde_json::Value`, preserving backend-owned response shapes and backend error messages.
- Added workflow frontend types, API client, and foundational hooks:
  - `src/CommandCenter.UI/src/types/workflow.ts`
  - `src/CommandCenter.UI/src/api/workflow.ts`
  - `useWorkflowProjection`, `useWorkflowHistory`, `useWorkflowGates`, `useWorkflowContinuation`, `useWorkflowPreparation`, `useWorkflowRecovery`, `useWorkflowHealth`, and `useWorkflowCertification`
- Exported workflow API, hooks, and types through existing index barrels.
- Fixed the frontend repository projection gap by adding `decisionSessionSummary` to repository dashboard/workspace TypeScript types and updating dev/test fixtures.
- Added frontend transport characterization for `getWorkflowProjection`.

## Verification

- `cargo check` passed in `src/CommandCenter.Shell`.
- `npm run build` passed in `src/CommandCenter.UI`.
- `npm run test -- --run src/test/characterization/transport.test.ts src/test/characterization/projectionHooks.test.tsx` passed with 22 tests.

## Still Open In Milestone 1

- Replace `getExecutionWorkflowSteps` consumers with authoritative workflow projection data.
- Adapt/move workflow rail UI into workflow panels that render stage, gates, required actions, continuation, recovery, health, and certification.
- Retire `src/CommandCenter.UI/src/lib/executionWorkflow.ts` only after all consumers are migrated.
- Add UI characterization coverage for visible workflow panels and the no-`RepositoryExecutionState` workflow-derivation regression.
