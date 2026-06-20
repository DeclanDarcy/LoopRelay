# Milestone M8 - Next Execution Flow

## Goal

Complete the repeatable execution loop.

## Backend Work

- [ ] After successful push, close the active session and transition repository execution state to `Ready`.
- [ ] Ensure `GetActiveAsync(repositoryId)` returns null after successful push.
- [ ] Preserve completed session history for audit.
- [ ] Ensure context can be rebuilt for the next selected milestone.
- [ ] Keep automatic milestone progression out of scope.

## UI Work

- [ ] After push success, show `Ready` and allow selecting another milestone.
- [ ] Keep latest session summary accessible until user starts another execution or refreshes.
- [ ] Refresh artifact inventory so the new current handoff and historical handoffs are visible.
- [ ] Refresh Git status to clean or show remaining changes.

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
