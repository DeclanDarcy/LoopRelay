# Milestone M5 - Execution Acceptance Workflow

## Goal

Require explicit user review before execution is accepted.

## Backend Work

- [x] Add acceptance and rejection endpoints.
- [x] Implement acceptance transition:
  - [x] `AwaitingAcceptance` to user-accepted state.
  - [x] Then immediately compute Git status.
  - [x] If changes exist, transition to `AwaitingCommit`.
  - [x] If no changes exist, allow transition to `Ready`.
- [x] Implement rejection transition:
  - [x] `AwaitingAcceptance` to `Ready`.
  - [x] Preserve handoff and session metadata for audit.
- [x] Persist accepted/rejected timestamp and optional note.
- [x] Prevent accept/reject outside `AwaitingAcceptance`.

## UI Work

- [ ] Add `Accept Handoff` and `Reject Handoff` controls.
- [ ] Display execution status, duration, token usage when present, completion time, and handoff content.
- [ ] Require confirmation before rejection.
- [ ] After acceptance, load Git status and show commit preparation.

## Tests

- [x] Accept from `AwaitingAcceptance` succeeds.
- [x] Accept outside `AwaitingAcceptance` fails.
- [x] Reject from `AwaitingAcceptance` succeeds.
- [x] Acceptance with changed files transitions to `AwaitingCommit`.
- [x] Acceptance with clean working tree can transition to `Ready`.
- [x] Accepted state persists after restart.

## Exit Criteria

- [ ] Execution results are not accepted until user review.
- [ ] Accepted work proceeds into the Git workflow.
- [ ] Rejected work does not proceed to commit or push.
