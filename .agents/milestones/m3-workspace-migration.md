# Milestone 3: Workspace Migration

## Tracking

- [ ] Milestone complete
- [ ] Workstream 3.1: Workflow Rail
- [ ] Workstream 3.2: Workspace Layout
- [ ] Workstream 3.3: Execution Context Panel
- [ ] Workstream 3.4: Live Activity Panel
- [ ] Workstream 3.5: Milestones Panel
- [ ] Workstream 3.6: Inspector Rail
- [ ] Workstream 3.7: Workspace Cross-Links
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

Display from `ExecutionContextPreview`:

- [ ] Artifact role.
- [ ] Relative path.
- [ ] Byte count.
- [ ] Character count.
- [ ] Per-artifact warnings and hard-limit status.
- [ ] Aggregate bytes and characters.
- [ ] Warning/hard thresholds.
- [ ] Missing optional artifacts.
- [ ] Validation errors.
- [ ] Launch blocked status.
- [ ] Repository snapshot branch and dirty-state summary.

Rules:

- [ ] Do not recalculate aggregate totals or validation.
- [ ] Use backend diagnostics as the source of truth.

## Workstream 3.4: Live Activity Panel

Display the current execution stream:

- [ ] Timestamp.
- [ ] Event type.
- [ ] Provider/session context where projected.
- [ ] Message.

Rules:

- [ ] Reuse `useExecutionEvents`.
- [ ] Do not create a second event store.
- [ ] Workspace and Execution tab must see the same event data.

## Workstream 3.5: Milestones Panel

Display milestones from artifact inventory and planning projection if needed:

- [ ] Current selected milestone.
- [ ] Milestone file path/name.
- [ ] Status when projected.
- [ ] Progress only when projected.

Rules:

- [ ] Do not fabricate criteria counts or progress metrics.
- [ ] If only milestone files are available, show file names and selection state.

## Workstream 3.6: Inspector Rail

Required sections:

- [ ] Commit and push summary:
  - [ ] Current repository state.
  - [ ] Commit preparation status when available.
  - [ ] Selected/generated change scope when available.
  - [ ] Ahead/behind only when backed by git status.
  - [ ] Explicit commit/push actions only in valid workflow states.
- [ ] Operational context summary:
  - [ ] Revision count.
  - [ ] Stable decision count.
  - [ ] Open question count.
  - [ ] Active risk count.
  - [ ] Pending proposal status.
  - [ ] Link to Operational Context tab.
- [ ] Execution history:
  - [ ] Recent sessions.
  - [ ] Milestone.
  - [ ] State.
  - [ ] Duration.
  - [ ] Timestamp.
  - [ ] Commit/push summary when projected.

Rules:

- [ ] Inspector sections summarize and navigate.
- [ ] They do not own workflow state.

## Workstream 3.7: Workspace Cross-Links

Add the cross-workspace links introduced by the Workspace tab:

- [ ] Operational-context summary navigates to the Operational Context tab and proposal/current-understanding section.
- [ ] Continuity warning snippets, if shown in the inspector, navigate to the Continuity tab and warning section.
- [ ] Execution activity and execution history rows navigate to the Execution tab for the selected session.
- [ ] Milestone rows update selected milestone navigation state and can navigate to the execution context panel.
- [ ] Pending handoff, commit, and push summary states navigate to the corresponding Workspace inspector section only.

Rules:

- [ ] Links update navigation state and optional section anchors only.
- [ ] Links do not refresh projections, start execution, accept handoffs, commit, push, generate proposals, or promote proposals.

### Certification

- [ ] Execution context, activity, milestones, commit/push, operational context, and history are co-visible on desktop.
- [ ] Existing artifact editing, execution start, handoff review, commit, push, operational-context review, and continuity actions remain reachable.
- [ ] Workspace links navigate without backend mutation.
- [ ] The Workspace tab can answer what is planned, what is happening, what changed, what understanding exists, and what comes next without requiring tab hopping.
