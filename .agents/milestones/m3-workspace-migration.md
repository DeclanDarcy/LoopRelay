# Milestone 3: Workspace Migration

## Tracking

- [ ] Milestone complete
- [x] Workstream 3.1: Workflow Rail
- [ ] Workstream 3.2: Workspace Layout
- [x] Workstream 3.3: Execution Context Panel
- [x] Workstream 3.4: Live Activity Panel
- [x] Workstream 3.5: Milestones Panel
- [x] Workstream 3.6: Inspector Rail
- [x] Workstream 3.7: Workspace Cross-Links
- [ ] Certification complete

Goal: make Workspace the primary operational surface with simultaneous visibility.

This is a major cognitive-flow migration, not a cosmetic recomposition. The current interface is workflow-navigation oriented; the target Workspace is operational-inspection oriented. Treat this as one of the largest milestones. Implement it in small vertical slices and certify each slice before adding the next.

Recommended internal order:

1. Workspace grid skeleton with preserved existing surfaces.
2. Workflow rail.
3. Execution context panel.
4. Live activity panel.
5. Milestones panel.
6. Inspector rail skeleton.
7. Commit/push summary.
8. Operational-context summary.
9. Execution history.
10. Artifact workspace integration and final density pass.

## Workstream 3.1: Workflow Rail

Implement `features/workspace/WorkflowRail.tsx`.

Display steps:

- [ ] Context.
- [ ] Execution.
- [ ] Handoff.
- [ ] Commit.
- [ ] Push.

Inputs:

- [ ] `RepositoryExecutionState`.
- [ ] Selected execution context presence.
- [ ] Existing execution summary/status where projected.

Rules:

- [ ] The rail is display-only.
- [ ] It must not trigger transitions.
- [ ] UI mapping may convert projected states into visual labels, but must not invent new workflow states.

## Workstream 3.2: Workspace Layout

Implement `features/workspace/WorkspaceTab.tsx`.

Status: started. `WorkspaceTab` now owns a desktop-first main column plus right inspector rail, with the existing repository summary, workflow rail, execution context panel, artifact workspace, and execution history slotted through real projections. Live activity, milestones, commit/push, and operational-context inspector placement still need to move into the workspace layout before this workstream is complete.

Target structure:

```text
WorkflowRail
WorkspaceGrid
  MainColumn
    ExecutionContextPanel
    LiveActivityPanel
    MilestonesPanel
    ArtifactWorkspace or preserved artifact editor placement
  InspectorRail
    CommitPushPanel
    OperationalContextSummary
    ExecutionHistory
```

Use a desktop-first grid with a right rail around 364px wide. Collapse cleanly on narrow viewports.

## Workstream 3.3: Execution Context Panel

Status: complete. Workspace now renders the execution context through a reusable display component backed by the existing execution context projection and App-owned action callbacks.

Display from `ExecutionContextPreview`:

- [x] Artifact role.
- [x] Relative path.
- [x] Byte count.
- [x] Character count.
- [x] Per-artifact warnings and hard-limit status.
- [x] Aggregate bytes and characters.
- [x] Warning/hard thresholds.
- [x] Missing optional artifacts.
- [x] Validation errors.
- [x] Launch blocked status.
- [x] Repository snapshot branch and dirty-state summary.

Rules:

- [x] Do not recalculate aggregate totals or validation.
- [x] Use backend diagnostics as the source of truth.

## Workstream 3.4: Live Activity Panel

Status: complete. Workspace now renders the current execution stream through a display-only live activity panel backed by the same `selectedExecutionEvents` array and `ExecutionEventFeed` row renderer used by the Execution tab.

Display the current execution stream:

- [x] Timestamp.
- [x] Event type.
- [x] Provider/session context where projected.
- [x] Message.

Rules:

- [x] Reuse `useExecutionEvents`.
- [x] Do not create a second event store.
- [x] Workspace and Execution tab must see the same event data.

## Workstream 3.5: Milestones Panel

Status: complete. Workspace now renders a display-only milestone panel backed by artifact inventory and selected milestone navigation state.

Display milestones from artifact inventory and planning projection if needed:

- [x] Current selected milestone.
- [x] Milestone file path/name.
- [x] Status when projected.
- [x] Progress only when projected.

Rules:

- [x] Do not fabricate criteria counts or progress metrics.
- [x] If only milestone files are available, show file names and selection state.

## Workstream 3.6: Inspector Rail

Status: complete. Workspace now renders a read-only inspector rail backed by existing git status, commit-preparation, operational-context, and execution-history projections. The inspector summarizes commit/push readiness evidence, operational-context counts and proposal status, and recent sessions without introducing commit/push orchestration or new readiness derivation.

Required sections:

- [x] Commit and push summary:
  - [x] Current repository state.
  - [x] Commit preparation status when available.
  - [x] Selected/generated change scope when available.
  - [x] Ahead/behind only when backed by git status.
  - [x] Explicit commit/push actions remain only in valid workflow states outside the inspector.
- [x] Operational context summary:
  - [x] Revision count.
  - [x] Stable decision count.
  - [x] Open question count.
  - [x] Active risk count.
  - [x] Pending proposal status.
  - [x] Link to Operational Context tab.
- [x] Execution history:
  - [x] Recent sessions.
  - [x] Milestone.
  - [x] State.
  - [x] Duration.
  - [x] Timestamp.
  - [x] Commit/push summary when projected.

Rules:

- [x] Inspector sections summarize and navigate.
- [x] They do not own workflow state.

## Workstream 3.7: Workspace Cross-Links

Status: complete. Workspace now provides navigation-only cross-links for operational context sections, continuity warning snippets when projected, live activity, execution history, and milestone-to-context navigation. Historic execution history rows navigate to the Execution workspace without loading alternate sessions, preserving the no-extra-backend-load rule for cross-links.

Add the cross-workspace links introduced by the Workspace tab:

- [x] Operational-context summary navigates to the Operational Context tab and proposal/current-understanding section.
- [x] Continuity warning snippets, if shown in the inspector, navigate to the Continuity tab and warning section.
- [x] Execution activity and execution history rows navigate to the Execution tab for the selected session.
- [x] Milestone rows update selected milestone navigation state and can navigate to the execution context panel.
- [x] Pending handoff, commit, and push summary states navigate to the corresponding Workspace inspector section only.

Rules:

- [x] Links update navigation state and optional section anchors only.
- [x] Links do not refresh projections, start execution, accept handoffs, commit, push, generate proposals, or promote proposals.

### Certification

- [x] Execution context, activity, milestones, commit/push, operational context, and history are co-visible on desktop.
- [x] Existing artifact editing, execution start, handoff review, commit, push, operational-context review, and continuity actions remain reachable.
- [x] Workspace links navigate without backend mutation.
- [x] The Workspace tab can answer what is planned, what is happening, what changed, what understanding exists, and what comes next without requiring tab hopping.
