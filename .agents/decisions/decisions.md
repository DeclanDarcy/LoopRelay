# Decisions

## Newly Authorized Decisions

- M6.2 is accepted as landing on the correct abstraction boundary.
- Preserve the separation between `RepositoryGitStatus` and `CommitPreparation`.
- Treat `RepositoryGitStatus` as observational: it answers what exists.
- Treat `CommitPreparation` as proposed state: it answers what is proposed for commit.
- Treat future commit and push operations as mutation: they answer what becomes history.
- Persisting `CommitPreparation` on the session is the correct backend-authoritative design.
- M6.3 is authorized as commit mutation only; push behavior remains out of scope.
- M6.3 commit validation must require `RepositoryExecutionState.AwaitingCommit` only.
- M6.3 must reject commit requests whose reviewed snapshot id does not match the persisted preparation snapshot id.
- M6.3 must reject selected paths that are not a subset of persisted `CommitPreparation.ScopeItems`.
- M6.3 must reject empty selected path sets.
- M6.3 must stage reviewed paths only and must not use `git add .`.
- M6.3 should persist `CommitSha`, `CommittedAt`, `CommitMessage`, and `PreparationSnapshotId` on the session.
- M6.3 successful commit transition is `AwaitingCommit` to `AwaitingPush`.
- M6.3 commit failure must leave the repository in `AwaitingCommit`, not transition to `Failed`.

## Explicitly Deferred

- No push behavior in M6.3.
- M6.4 should handle push request, `git push`, push metadata persistence, and transition to `Ready`.
- M6.4 push validation should require `AwaitingPush` only.
- M6.4 push failure should leave the repository in `AwaitingPush`.
