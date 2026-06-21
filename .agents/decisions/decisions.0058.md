# Decisions

## Newly Authorized

- Treat the M3 execution-context Workspace slice as a correct authority boundary: `App.tsx` retains milestone selection, context build, launch readiness, and start-execution authority while `ExecutionContextPanel` remains presentation-only.
- Treat Execution Context as an evidence surface that belongs in Workspace because it answers what will be executed, what context will be sent, which artifacts are involved, and whether size thresholds are approaching.
- Preserve Workstream 3.2 as active until Live Activity, Milestones, Commit/Push, and Operational Context are represented in Workspace through backend-backed surfaces.
- Use Live Activity as the next M3 slice.
- Implement Workspace Live Activity from the existing execution-event source only: `useExecutionEvents` to `selectedExecutionEvents` to `ExecutionEventFeed` or a display-only Workspace wrapper.
- Do not introduce a Workspace event store, Workspace activity state, second event model, or duplicate event authority.
- Certify the next slice by proving Workspace Activity Feed and Execution Activity Feed are semantically identical for the same execution session: same source, ordering, event count, and event identities.
- Continue the M3 separation where Workspace is the operational overview and specialized tabs remain operation-specific surfaces.
- Current program state remains M0 complete, M1 complete, M2 complete, M3 active, Workstream 3.1 complete, Workstream 3.2 active, and Workstream 3.3 complete.
