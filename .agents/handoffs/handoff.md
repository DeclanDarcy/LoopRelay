# Handoff

## Slice Summary

- Executed Epic 2 M6.1 read-only Git status and repository inspection.
- Added `RepositoryGitStatus` and expanded `RepositoryDirtyState` with `AddedPaths`.
- Extended `IGitService` and `GitService` with `GetStatusAsync`.
- `GitService` now parses `git status --porcelain=v1 --branch -z` for branch, ahead/behind counts, and staged/modified/added/deleted/renamed/untracked buckets.
- Added backend endpoint `GET /api/repositories/{repositoryId}/git/status`.
- Added Tauri command `get_git_status` as a thin HTTP bridge.
- Added React Git workflow status panel for `Ready`, `AwaitingCommit`, and `AwaitingPush`.
- Updated the dev Tauri mock so mock acceptance into `AwaitingCommit` shows dirty Git status.
- Rotated prior `.agents/handoffs/handoff.md` to `.agents/handoffs/handoff.0019.md`.

## Files Changed

- `.agents/milestones/m6-git-lifecycle.md`
- `.agents/handoffs/handoff.0019.md`
- `.agents/handoffs/handoff.md`
- `src/CommandCenter.Backend/Execution/GitService.cs`
- `src/CommandCenter.Backend/Execution/IGitService.cs`
- `src/CommandCenter.Backend/Execution/RepositoryDirtyState.cs`
- `src/CommandCenter.Backend/Execution/RepositoryGitStatus.cs`
- `src/CommandCenter.Backend/Program.cs`
- `src/CommandCenter.Shell/src/main.rs`
- `src/CommandCenter.UI/src/App.css`
- `src/CommandCenter.UI/src/App.tsx`
- `src/CommandCenter.UI/src/devTauriMock.ts`
- `tests/CommandCenter.Backend.Tests/ExecutionContextServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionMonitoringEndpointTests.cs`
- `tests/CommandCenter.Backend.Tests/ExecutionSessionServiceTests.cs`
- `tests/CommandCenter.Backend.Tests/GitServiceTests.cs`

## Verification

- `dotnet test tests/CommandCenter.Backend.Tests/CommandCenter.Backend.Tests.csproj` passed: 119 tests.
- `npm run build --prefix src/CommandCenter.UI` passed.
- `cargo build --manifest-path src/CommandCenter.Shell/Cargo.toml` passed.
- `dotnet build CommandCenter.slnx` passed.
- Browser smoke test with `http://127.0.0.1:5173/?mock=workspace-certification` verified:
  - Git workflow panel is visible in `Ready`;
  - mock acceptance transitions to `AwaitingCommit`;
  - Git workflow panel remains visible in `AwaitingCommit`;
  - modified and untracked mock paths are displayed.

## New State

- M6.1 read-only Git status is implemented and verified.
- M6 checklist now marks Git status endpoint, UI grouped status display, and parser bucket coverage complete.
- Commit preparation, selectable commit scope, commit mutation, push mutation, stale-scope rejection, and push/commit retry UI remain unimplemented.
- Pre-existing dirty-path marking is not implemented in this slice because the current UI summary does not carry the pre-execution snapshot; this should be returned explicitly by commit preparation scope items.
- Current worktree has M6.1 implementation, milestone checklist, and handoff rotation changes unstaged.

## Recommended Next Slice

- Execute M6.2 commit preparation.
- Add backend commit-preparation models with deterministic message, status snapshot id, selectable `CommitScopeItem`s, and pre-execution dirty comparison.
- Add `prepare_commit` endpoint and Tauri bridge.
- Replace the read-only Git status panel in `AwaitingCommit` with selectable commit review controls, but do not implement commit mutation until preparation and stale-scope validation are certified.
