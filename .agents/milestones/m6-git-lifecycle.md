# Milestone M6 - Git Lifecycle Automation

## Goal

Replace manual status inspection, commit message drafting, commit, and push flow.

## Backend Work

- [ ] Implement full `GitService`.
- [ ] Add Git status endpoint.
- [ ] Add commit preparation endpoint that generates a deterministic proposed commit message.
- [ ] Include pre-execution dirty snapshot comparison in commit preparation.
- [ ] Build a selectable `CommitScopeItem` for every displayed changed path.
- [ ] Assign a Git status snapshot id to the prepared commit scope.
- [ ] Add commit endpoint:
  - [ ] Validate session is `Accepted` or `AwaitingCommit`.
  - [ ] Refresh Git status.
  - [ ] Validate selected repository-relative paths against the prepared or refreshed status snapshot.
  - [ ] Reject stale, empty, unknown, absolute, or repository-escaping paths.
  - [ ] Stage only selected changes.
  - [ ] Commit with reviewed message.
  - [ ] Store commit sha.
  - [ ] Transition to `AwaitingPush`.
- [ ] Add push endpoint:
  - [ ] Validate session is `AwaitingPush`.
  - [ ] Push current branch.
  - [ ] Store push result.
  - [ ] Transition repository execution state to `Ready`.
- [ ] Return structured failures for commit and push errors.

## UI Work

- [ ] Display Git status grouped by modified, added, deleted, renamed, untracked, and staged.
- [ ] Mark paths that were already dirty before execution when known.
- [ ] Render each changed file with an individual selection control.
- [ ] Provide `Select All` and `Select None`.
- [ ] Show editable deterministic proposed commit message.
- [ ] Show the exact commit scope that will be staged.
- [ ] Add commit action.
- [ ] Show commit sha after success.
- [ ] Add push action.
- [ ] Show push success or failure.
- [ ] Keep retry controls available after commit or push failure.

## Tests

- [ ] Status parser handles modified, added, deleted, renamed, untracked, and staged paths.
- [ ] Commit message generation is deterministic and limited to milestone name plus changed-file count.
- [ ] Commit preparation identifies pre-existing dirty paths when known.
- [ ] Commit preparation returns one selectable item per changed path.
- [ ] Commit action stages only selected paths.
- [ ] Unselected paths are not staged.
- [ ] Empty selected path set is rejected unless the workflow is explicitly marking a clean execution ready without commit.
- [ ] Stale status snapshot is rejected.
- [ ] Commit failure records failure and leaves state retryable.
- [ ] Push failure records failure and leaves state retryable.
- [ ] Successful commit transitions to `AwaitingPush`.
- [ ] Successful push transitions to `Ready`.
- [ ] No destructive Git commands are issued.

## Exit Criteria

- [ ] User can review status, edit commit message, commit, and push from Command Center.
- [ ] Commit and push failures are visible and retryable.
- [ ] Repository returns to `Ready` after successful push.
