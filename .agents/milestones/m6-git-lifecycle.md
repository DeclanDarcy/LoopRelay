# Milestone M6 - Git Lifecycle Automation

## Goal

Replace manual status inspection, commit message drafting, commit, and push flow.

## Backend Work

- [ ] Implement full `GitService`.
- [x] Add Git status endpoint.
- [x] Add commit preparation endpoint that generates a deterministic proposed commit message.
- [x] Include pre-execution dirty snapshot comparison in commit preparation.
- [x] Build a selectable `CommitScopeItem` for every displayed changed path.
- [x] Assign a Git status snapshot id to the prepared commit scope.
- [x] Add commit endpoint:
  - [x] Validate session is `AwaitingCommit`.
  - [x] Refresh Git status.
  - [x] Validate selected repository-relative paths against the prepared and refreshed status snapshot.
  - [x] Reject stale, empty, unknown, absolute, or repository-escaping paths.
  - [x] Stage only selected changes.
  - [x] Commit with reviewed message.
  - [x] Store commit sha.
  - [x] Transition to `AwaitingPush`.
- [ ] Add push endpoint:
  - [ ] Validate session is `AwaitingPush`.
  - [ ] Push current branch.
  - [ ] Store push result.
  - [ ] Transition repository execution state to `Ready`.
- [ ] Return structured failures for commit and push errors.

## UI Work

- [x] Display Git status grouped by modified, added, deleted, renamed, untracked, and staged.
- [x] Mark paths that were already dirty before execution when known.
- [x] Render each changed file with an individual selection control.
- [x] Provide `Select All` and `Select None`.
- [x] Show editable deterministic proposed commit message.
- [x] Show the exact commit scope that will be staged.
- [x] Add commit action.
- [x] Show commit sha after success.
- [ ] Add push action.
- [ ] Show push success or failure.
- [ ] Keep retry controls available after commit or push failure.

## Tests

- [x] Status parser handles modified, added, deleted, renamed, untracked, and staged paths.
- [x] Commit message generation is deterministic and limited to milestone name plus changed-file count.
- [x] Commit preparation identifies pre-existing dirty paths when known.
- [x] Commit preparation returns one selectable item per changed path.
- [x] Commit action stages only selected paths.
- [x] Unselected paths are not staged.
- [x] Empty selected path set is rejected unless the workflow is explicitly marking a clean execution ready without commit.
- [x] Stale status snapshot is rejected.
- [x] Commit failure returns a visible error and leaves state retryable.
- [ ] Push failure records failure and leaves state retryable.
- [x] Successful commit transitions to `AwaitingPush`.
- [ ] Successful push transitions to `Ready`.
- [ ] No destructive Git commands are issued.

## Exit Criteria

- [ ] User can review status, edit commit message, commit, and push from Command Center.
- [ ] Commit and push failures are visible and retryable.
- [ ] Repository returns to `Ready` after successful push.
