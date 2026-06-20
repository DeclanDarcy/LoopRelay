# Decisions

## Newly Authorized Decisions

- M6.3 is accepted as architecturally sound.
- Preserve the backend-authoritative flow from Git status to commit preparation to user review to persisted preparation to commit validation to mutation.
- Treat `Generate -> Persist -> Review -> Validate -> Mutate` as an emerging Command Center architectural principle.
- M6.4 is authorized as push mutation.
- M6.4 push validation must require `RepositoryExecutionState.AwaitingPush` only.
- Push failures must leave the repository in `AwaitingPush`, not transition to `Failed`.
- Push failures are retryable operational conditions, including remote rejection, network unavailability, expired authentication, and branch protection failures.
- M6.4 should minimally persist `PushAttemptedAt`, `PushedAt`, and `PushedCommitSha`.
- M6.4 may persist `RemoteName` and `BranchName` if available cheaply.
- M6.4 must refresh Git status after successful `git push` before transitioning the repository to `Ready`.
- M6.4 should not assume a successful `git push` means the repository is synchronized without re-reading status.
- M6.4 should include push request/result contracts, `git push` through `IGitService`/`IProcessRunner`, push metadata persistence, retryable failure behavior, UI push workflow, and certification coverage.

## Explicitly Deferred

- M7 Unified Execution Workspace remains the next focus after M6.4 lands cleanly.
