# Handoff

## New State

- Completed Milestone 3 workflow gate catalog.
- Added first-class gate catalog types:
  `WorkflowGateStatus`, `WorkflowGate`, `WorkflowGateEvidence`, `WorkflowGateDiagnostics`,
  `WorkflowGateCatalogProjection`, and `WorkflowGateHistoryProjection`.
- Added `IWorkflowGateCatalogService` and `WorkflowGateCatalogService`.
- Added deterministic gate-to-command mapping:
  `WorkSelection -> explicit_human_work_selection`,
  `ExecutionAcceptance -> accept_execution_handoff | reject_execution_handoff`,
  `DecisionResolution -> resolve_decision_proposal`,
  `OperationalContextReview -> accept_operational_context_proposal | edit_operational_context_proposal | reject_operational_context_proposal`,
  `OperationalContextPromotion -> promote_operational_context_proposal`,
  `CommitApproval -> commit_execution`,
  `PushApproval -> push_execution`.
- Extended workflow projections with open gates, satisfied gates, gate history, and gate diagnostics.
- Added derived gate endpoints:
  `GET /api/repositories/{repositoryId}/workflow/gates`
  and `GET /api/repositories/{repositoryId}/workflow/gates/history`.
- Added timeline evidence events for satisfied gates:
  `ExecutionHandoffAccepted` and `OperationalContextReviewed`.
- Marked `.agents/milestones/m3-gate-catalog.md` complete.
- Rotated previous `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0003.md`.

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj --filter WorkflowProjectionServiceTests` passed: 22 tests.
- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed: 534 tests.
- `dotnet build CommandCenter.slnx` passed with 0 warnings and 0 errors.

## Notes

- Gate catalog is read-only. It derives open and satisfied gates from workflow projection and timeline evidence; it does not call or wrap domain commands.
- Gate history is currently a derived API projection with Markdown content, not persisted gate truth. If gate artifacts are later persisted, they must stay reconstructable evidence and never become mutable workflow state.
- `resolve_decision_proposal` is used as the current concrete command name because that is the command exposed by the Tauri bridge and UI.

## Next Slice

- Start Milestone 4 execution workflow integration by adding execution-specific workflow projection/service boundaries and endpoints, then prove execution running/completed/failed/awaiting-acceptance evidence feeds workflow without introducing execution authority.
