# Milestone 0 MVP Adjustment Log

1. Milestone 1 remains first implementation work: add workflow shell commands, workflow TypeScript models/client/hooks, workflow panels, and retire `src/CommandCenter.UI/src/lib/executionWorkflow.ts`.
2. Milestone 1 should consume existing `WorkflowEndpoints.cs`; no new workflow backend authority is needed unless serialization tests expose a missing field.
3. Add `decisionSessionSummary` to frontend repository types before or during Milestone 2 so dashboard/workspace projections do not silently drop governance state.
4. Milestone 2 must add backend command endpoints for `IDecisionSessionTransferService.ExecuteAsync` and `IDecisionSessionRecoveryService.RecoverAsync`, then bridge them through shell/UI.
5. Milestone 3 should expose missing decision lifecycle verbs in `src/CommandCenter.UI/src/api/decisions.ts` and add a backend-owned lifecycle eligibility projection rather than letting React infer action availability.
6. Milestone 5 should expose execution prompt metadata/manifest and structured governed conflicts from backend projections, not from prompt text parsing.
7. Milestone 5 should change push failure handling so the user can see persisted retry state after a failed push.
8. Milestone 6 should extend reasoning reconstruction output with confidence rationale, missing evidence, and reconstruction scope before updating UI transparency.
9. Milestone 7 should make operational-context diffing identity-aware so modifications are not represented only as remove/add pairs.
10. Milestone 8 should introduce shared explainability presentation only after authoritative workflow, decision, execution, reasoning, and continuity fields are available.
