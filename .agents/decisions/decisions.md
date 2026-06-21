# Decisions

## Newly Authorized

- Treat the first M3 slice as a valid movement toward a new authority boundary because `WorkspaceTab` owns presentation layout only.
- Preserve the current split where `App.tsx` owns drafts, workflow, readiness, mutations, and Workspace composition inputs while `WorkspaceTab` owns layout, placement, and presentation.
- Keep `WorkflowRail` display-only: it may render workflow steps, status, and progress from existing projections, but it must not decide, interpret, gate, or coordinate workflow state.
- Keep Workstream 3.2 open; the Workspace shell/grid is started but not complete until Execution Context, Live Activity, Milestones, Commit/Push, and Operational Context are placed through real backend-backed surfaces.
- Use Execution Context as the next M3 slice by moving it into a reusable Workspace slot while preserving the Execution tab and preserving launch/start-execution authority in `App.tsx`.
- Do not create a Workspace-specific state layer such as `WorkspaceStore`, `WorkspaceContext`, `WorkspaceEventBus`, or `WorkspaceDerivedState` unless a proven authority boundary requires it.
- Continue reusing backend projections, existing execution hooks, session hooks, and event hooks as the source of truth.
- Treat the central M3 extraction question as: what named authority is moving, not how much code leaves `App.tsx`.
- Current program state is M0 complete, M1 complete, M2 complete, M3 in progress, Workstream 3.1 complete, and Workstream 3.2 started.
