# Milestone M7 - Unified Execution Workspace

## Goal

Create the primary execution experience that combines context, execution stream, handoff review, Git status, and lifecycle controls.

## Backend Work

- [ ] Ensure workspace projection includes complete execution summary.
- [ ] Provide a single session detail endpoint sufficient for workspace hydration.
- [ ] Keep dashboard projection lightweight.

## UI Work

- [ ] Consolidate execution-specific panels into a coherent workspace:
  - [ ] Current repository.
  - [ ] Selected milestone.
  - [ ] Execution state.
  - [ ] Context diagnostics.
  - [ ] Output stream.
  - [ ] Handoff viewer.
  - [ ] Git status.
  - [ ] Acceptance, commit, and push controls.
- [ ] Maintain existing artifact explorer and editor.
- [ ] Ensure responsive layout works on desktop and narrow viewports.
- [ ] Avoid nested card clutter; use full-width sections and compact panels.

## Tests

- [ ] TypeScript build passes.
- [ ] UI can navigate repository dashboard to execution workspace.
- [ ] UI handles Ready, Executing, AwaitingAcceptance, AwaitingCommit, AwaitingPush, Failed, and Cancelled states.
- [ ] Long paths, long filenames, and long output lines do not break layout.

## Exit Criteria

- [ ] Execution workflow is usable from one primary workspace.
- [ ] Users do not need external tools for context review, execution monitoring, handoff review, commit, or push.
