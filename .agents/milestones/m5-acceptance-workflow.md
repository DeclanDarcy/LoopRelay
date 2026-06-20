# Milestone M5 - Execution Acceptance Workflow

## Goal

Require explicit user review before execution is accepted.

## Backend Work

- [ ] Add acceptance and rejection endpoints.
- [ ] Implement acceptance transition:
  - [ ] `AwaitingAcceptance` to `Accepted`.
  - [ ] Then immediately compute Git status.
  - [ ] If changes exist, transition to `AwaitingCommit`.
  - [ ] If no changes exist, allow transition to `Ready`.
- [ ] Implement rejection transition:
  - [ ] `AwaitingAcceptance` to `Ready` or `Failed` with rejection reason based on chosen UI behavior.
  - [ ] Preserve handoff and session metadata for audit.
- [ ] Persist accepted/rejected timestamp and optional note.
- [ ] Prevent accept/reject outside `AwaitingAcceptance`.

## UI Work

- [ ] Add `Accept Handoff` and `Reject Handoff` controls.
- [ ] Display execution status, duration, token usage when present, completion time, and handoff content.
- [ ] Require confirmation before rejection.
- [ ] After acceptance, load Git status and show commit preparation.

## Tests

- [ ] Accept from `AwaitingAcceptance` succeeds.
- [ ] Accept outside `AwaitingAcceptance` fails.
- [ ] Reject from `AwaitingAcceptance` succeeds.
- [ ] Acceptance with changed files transitions to `AwaitingCommit`.
- [ ] Acceptance with clean working tree can transition to `Ready`.
- [ ] Accepted state persists after restart.

## Exit Criteria

- [ ] Execution results are not accepted until user review.
- [ ] Accepted work proceeds into the Git workflow.
- [ ] Rejected work does not proceed to commit or push.
