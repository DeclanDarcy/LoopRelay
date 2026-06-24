# Handoff

## New State This Slice

- Continued Milestone 1 by integrating authoritative workflow projection facts into the selected repository/dashboard summary.
- `SelectedRepositorySummary` now accepts `workflow: WorkflowInstance | null` and renders:
  - workflow stage
  - blocking workflow gate
  - required human action
  - workflow timeline event count
- `App.tsx` passes the existing `workflowProjection` from `useWorkflowProjection` into `SelectedRepositorySummary`; no dashboard-specific workflow model was added.
- Added a characterization test proving the selected repository summary renders workflow stage, gate, required action, and timeline count from `WorkflowInstance`.
- Added `.agents/milestones/m1-workflow-consumption-pattern.md` documenting workflow ownership and consumption boundaries for repository, execution, governance, decision, operational-context, health, and certification surfaces.
- Updated `.agents/milestones/m1-workflow-engine.md` to mark dashboard summary integration and documented workflow consumption pattern complete while leaving future governance and operational-context UI linkage open.
- Rotated the previous handoff to `.agents/handoffs/handoff.0004.md`.

## Verification

- `npm run build` passed in `src/CommandCenter.UI`.
- `npm run test -- --run src/test/characterization/selectedRepositorySummary.test.tsx src/test/characterization/workflowPanels.test.tsx src/test/characterization/executionWorkflowRail.test.tsx src/test/characterization/workflowAuthority.test.ts` passed with 11 tests.

## Still Open In Milestone 1

- Add backend endpoint tests for any workflow route not already covered.
- Add shell command tests where feasible.
- Decide whether remaining Milestone 1 governance and operational-context workflow-linkage bullets should be implemented now or treated as Milestone 2 and Milestone 7 entry criteria.
- Close the remaining exit criteria around proving no other workspace creates a parallel lifecycle timeline and confirming parallel client-side workflow derivation removal.
