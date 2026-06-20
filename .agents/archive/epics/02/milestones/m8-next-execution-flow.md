# Milestone M8 - Next Execution Flow

## Goal

Complete the repeatable execution loop.

## Backend Work

- [x] After successful push, close the active session and transition repository execution state to `Ready`.
- [x] Ensure `GetActiveAsync(repositoryId)` returns null after successful push.
- [x] Preserve completed session history for audit.
- [x] Ensure context can be rebuilt for the next selected milestone.
- [x] Keep automatic milestone progression out of scope.

## UI Work

- [x] After push success, show `Ready` and allow selecting another milestone.
- [x] Keep latest session summary accessible until user starts another execution or refreshes.
- [x] Refresh artifact inventory so the new current handoff and historical handoffs are visible.
- [x] Refresh Git status to clean or show remaining changes.

## Tests

- [x] Full loop test with fake provider and fake Git:
  - [x] Ready.
  - [x] Start execution.
  - [x] Stream output.
  - [x] Complete with handoff.
  - [x] Await acceptance.
  - [x] Accept.
  - [x] Commit.
  - [x] Push.
  - [x] Ready.
- [x] Repeat loop twice for one repository and verify only one active session exists at a time.
- [x] Verify handoff history increments across repeated executions.

## Exit Criteria

- [x] Repository can repeatedly move through execution, acceptance, commit, push, and ready states.
- [x] Command Center is ready to launch the next execution without manual artifact collection or manual Git flow.

## M8.1 Notes

- Added a bounded, newest-first `ExecutionHistory` projection backed by persisted `ExecutionSession` records.
- Kept `ExecutionSummary` as latest-session continuity while exposing `ExecutionHistory` as the audit list.
- Verified successful push returns the repository to `Ready`, clears active execution lookup, and preserves the completed session in history.
- Updated the workspace UI with a compact session history panel including milestone, state, duration, commit, and push metadata.
- Updated the post-push UI path to refresh repository artifact inventory and Git status.

## M8.2 Notes

- Added repeatable execution certification covering two fake-provider/fake-Git loops against one repository.
- Certified provider output retention, handoff validation, acceptance, commit, push, and return to `Ready`.
- Certified duplicate execution launch remains blocked while a repository is executing.
- Certified restart-between-executions behavior by rebuilding the service from the persisted execution store after the first push.
- Certified context and prompt rebuild for a different selected milestone before the second execution.
- Certified handoff history increments from `handoff.0001.md` to `handoff.0002.md` while preserving the latest `handoff.md`.
