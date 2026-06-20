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
- [ ] Avoid nested card clutter; use full-width sections and compact panels.

## Tests

- [x] TypeScript build passes.
- [ ] UI can navigate repository dashboard to execution workspace.
- [ ] UI handles Ready, Executing, AwaitingAcceptance, AwaitingCommit, AwaitingPush, Failed, and Cancelled states.
- [x] Long paths, long filenames, and long output lines do not break layout.

## Exit Criteria

- [x] Execution workflow is usable from one primary workspace.
- [ ] Users do not need external tools for context review, execution monitoring, handoff review, commit, or push.
