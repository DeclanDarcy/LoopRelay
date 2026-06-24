# Handoff

## New State This Slice

- Current `.agents/handoffs/handoff.md` and `.agents/decisions/decisions.md` were absent at slice start; no handoff rotation was possible.
- Milestone 0 is complete and checked off in `.agents/milestones/m0-capability-verification.md`.
- Added Milestone 0 evidence artifacts:
  - `.agents/milestones/m0-capability-inventory.md`
  - `.agents/milestones/m0-capability-disposition-register.md`
  - `.agents/milestones/m0-authority-review.md`
  - `.agents/milestones/m0-mvp-adjustment-log.md`
- M0 found no need for a new backend authority before M1. Existing backend authorities are sufficient for the MVP sequence.

## Highest-Leverage Findings

- Milestone 1 should start by surfacing existing `WorkflowEndpoints.cs`; workflow backend routes already exist.
- React still derives workflow rail state from `RepositoryExecutionState` through `src/CommandCenter.UI/src/lib/executionWorkflow.ts`; this is the primary duplicate lifecycle model to retire.
- `src/CommandCenter.UI/src/api/workflow.ts` and `src/CommandCenter.UI/src/types/workflow.ts` are absent.
- `src/CommandCenter.Shell/src/main.rs` has no workflow command bridge.
- Middle projections include `RepositoryDecisionSessionSummary`, but `src/CommandCenter.UI/src/types/repositories.ts` does not expose `decisionSessionSummary`.

## Recommended Next Slice

Begin Milestone 1 with the narrow transport/model foundation:

1. Add workflow TypeScript models matching `CommandCenter.Workflow.Models`.
2. Add workflow API client methods for the M1 Core MVP read/action routes.
3. Add Tauri workflow commands using existing backend get/post helpers or equivalent local pattern.
4. Add initial workflow hooks for projection, gates, history, recovery, health, and certification.
5. Only after transport is covered, replace `getExecutionWorkflowSteps` consumers with authoritative workflow projection data.
