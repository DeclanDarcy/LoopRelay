# Milestone M8 - Next Execution Flow

## Goal

Complete the repeatable execution loop.

## Backend Work

- [x] After successful push, close the active session and transition repository execution state to `Ready`.
- [x] Ensure `GetActiveAsync(repositoryId)` returns null after successful push.
- [x] Preserve completed session history for audit.
- [ ] Ensure context can be rebuilt for the next selected milestone.
- [ ] Keep automatic milestone progression out of scope.

## UI Work

- [x] After push success, show `Ready` and allow selecting another milestone.
- [x] Keep latest session summary accessible until user starts another execution or refreshes.
- [x] Refresh artifact inventory so the new current handoff and historical handoffs are visible.
- [x] Refresh Git status to clean or show remaining changes.

## Tests

- [ ] Full loop test with fake provider and fake Git:
  - [ ] Ready.
  - [ ] Start execution.
  - [ ] Stream output.
  - [ ] Complete with handoff.
  - [ ] Await acceptance.
  - [ ] Accept.
  - [ ] Commit.
  - [ ] Push.
  - [ ] Ready.
- [ ] Repeat loop twice for one repository and verify only one active session exists at a time.
- [ ] Verify handoff history increments across repeated executions.

## Exit Criteria

- [ ] Repository can repeatedly move through execution, acceptance, commit, push, and ready states.
- [ ] Command Center is ready to launch the next execution without manual artifact collection or manual Git flow.

## M8.1 Notes

- Added a bounded, newest-first `ExecutionHistory` projection backed by persisted `ExecutionSession` records.
- Kept `ExecutionSummary` as latest-session continuity while exposing `ExecutionHistory` as the audit list.
- Verified successful push returns the repository to `Ready`, clears active execution lookup, and preserves the completed session in history.
- Updated the workspace UI with a compact session history panel including milestone, state, duration, commit, and push metadata.
- Updated the post-push UI path to refresh repository artifact inventory and Git status.
