# Milestone M7 - Unified Execution Workspace

## Goal

Create the primary execution experience that combines context, execution stream, handoff review, Git status, and lifecycle controls.

## Backend Work

- [x] Ensure workspace projection includes complete execution summary.
- [x] Provide a single session detail endpoint sufficient for workspace hydration.
- [x] Keep dashboard projection lightweight.

## UI Work

- [x] Consolidate execution-specific panels into a coherent workspace:
  - [x] Current repository.
  - [x] Selected milestone.
  - [x] Execution state.
  - [x] Context diagnostics.
  - [x] Output stream.
  - [x] Handoff viewer.
  - [x] Git status.
  - [x] Acceptance, commit, and push controls.
- [x] Maintain existing artifact explorer and editor.
- [x] Ensure responsive layout works on desktop and narrow viewports.
- [x] Avoid nested card clutter; use full-width sections and compact panels.

## Tests

- [x] TypeScript build passes.
- [x] UI can navigate repository dashboard to execution workspace.
- [x] UI handles Ready, Executing, AwaitingAcceptance, AwaitingCommit, AwaitingPush, Failed, and Cancelled states.
- [x] Long paths, long filenames, and long output lines do not break layout.

## Exit Criteria

- [x] Execution workflow is usable from one primary workspace.
- [x] Users do not need external tools for context review, execution monitoring, handoff review, commit, or push.

## M7.2 Certification Notes

- Added deterministic dev mock repositories for `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.
- Browser-verified the dashboard-to-workspace path with the workspace certification mock.
- Browser-verified lifecycle rail projection for `Ready`, `Executing`, `AwaitingAcceptance`, `AwaitingCommit`, `AwaitingPush`, `Failed`, and `Cancelled`.
- Browser-verified the transition path `Ready -> AwaitingAcceptance -> AwaitingCommit -> AwaitingPush -> Ready` through context preview, execution start, handoff acceptance, commit, and push controls.
- Browser-verified narrow viewport behavior at 390px width with no horizontal document overflow.
- Corrected the lifecycle rail so a `Ready` repository without a session no longer marks handoff or commit as complete.
