# Decisions

## Newly Authorized Decisions

- M5 is complete and certified.
- Missing `rustfmt` is not architecturally significant and does not block M5 closure.
- M6 is the first milestone that mutates repository state after acceptance.
- M6 must establish `IGitService` as the sole authoritative Git abstraction boundary.
- Avoid exposing raw Git commands throughout the system.
- Retain existing repository workflow states for M6: `Accepted`, `AwaitingCommit`, `AwaitingPush`, and `Ready`.
- Do not introduce `Committing`, `Pushing`, `CommitFailed`, or `PushFailed` states unless asynchronous operations require them.
- Git operation progress must not redefine `RepositoryExecutionState`.
- Start M6 with four backend operations: status, commit preparation, commit, and push.
- `GetStatus(repositoryId)` returns modified files, added files, deleted files, renamed files, current branch, ahead/behind counts, and dirty flag.
- `PrepareCommit(repositoryId)` is a pure projection with no mutation.
- Commit preparation returns candidate files, diff statistics, and a suggested commit message.
- `Commit(repositoryId, selectedFiles, commitMessage)` transitions `AwaitingCommit` to `AwaitingPush`.
- `Push(repositoryId)` transitions `AwaitingPush` to `Ready`.
- Acceptance approves the repository state produced by execution.
- Commit preparation may allow file-level exclusions, but exclusions are exceptional and do not redefine what acceptance means.
- M6.1 is authorized as Git Status & Repository Inspection.
- M6.1 must implement `IGitService`, repository status model, backend endpoints, persistence integration, Tauri bridge, and a read-only React workflow surface.
- M6.1 must not perform commit mutation.
- Certify that `Ready`, `AwaitingCommit`, and `AwaitingPush` correctly project repository Git state before implementing commit mutation.

## Explicitly Deferred

- Do not implement commit mutation in M6.1.
- Do not add asynchronous Git workflow states unless a later implementation requires asynchronous operations.
